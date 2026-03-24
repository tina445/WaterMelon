using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using UnityEngine;

public enum VersusResult { None, Win, Lose, Draw }

public class VersusMatchManager : NetworkBehaviour
{
    [Header("Sim Boards (Physics)")]
    [SerializeField] private NetworkGameManager hostSimBoard;
    [SerializeField] private NetworkGameManager clientSimBoard;

    [Header("Render Roots (NetworkBalls parents)")]
    [SerializeField] private Transform hostRenderRoot;
    [SerializeField] private Transform clientRenderRoot;

    [Header("NetworkBalls Prefab")]
    [SerializeField] private NetworkBalls networkBallPrefab;

    [Header("Pool Size Per Side")]
    [SerializeField, Range(8, 64)] private int poolSizePerSide = 24;

    [Header("Send Interval")]
    [SerializeField] private float sendInterval = 0.06f;

    public bool HasSpawned { get; private set; }

    [Networked] public int HostScore { get; private set; }
    [Networked] public int ClientScore { get; private set; }
    [Networked] public NetworkBool HostDead { get; private set; }
    [Networked] public NetworkBool ClientDead { get; private set; }

    [Networked] public NetworkString<_16> HostSkinId { get; private set; }
    [Networked] public NetworkString<_16> ClientSkinId { get; private set; }

    public bool MatchEnded => HostDead || ClientDead;

    private float _timer;

    private NetworkBalls[] _hostPool;
    private NetworkBalls[] _clientPool;

    private PlayerRef _hostRef;
    private PlayerRef _clientRef;

    public override void Spawned()
    {
        Debug.Log($"[VMM] Spawned running={Runner.IsRunning} isServer={Runner.IsServer} local={Runner.LocalPlayer}");
        HasSpawned = true;

        AutoBindBoards();
        AutoBindRoots();
        
        if (Runner.IsServer)
        {
            CachePlayerRefs();
            ServerCreatePoolsOnce();
        }

        var mySkin = AppFacade.I.UserData.CurrentProfile.selectedSkinId;

        if (Runner.LocalPlayer == _hostRef)
            HostSkinId = mySkin;
        else
            ClientSkinId = mySkin;

        HideLocalGhostPool();

        ApplyRemoteViewFlags();
    }

    private void Update()
    {        
        if (Runner == null || !Runner.IsRunning) return;
        if (!HasSpawned) return;

        // 서버는 FixedUpdateNetwork에서만 보냄
        if (Runner.IsServer) return;

        var myBoard = GetMySimBoard();
        if (myBoard == null) return;

        _timer += Time.deltaTime;
        if (_timer < sendInterval) return;
        _timer = 0f;

        Debug.Log($"[SendSnapshot] IsServer=False local={Runner.LocalPlayer}");

        var bytes = BuildSnapshotBytes(myBoard, poolSizePerSide, Runner);
        RPC_SubmitSnapshot(bytes);
    }
    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;
        if (!HasSpawned || Runner == null || !Runner.IsRunning) return;

        var myBoard = GetMySimBoard();
        if (myBoard == null) return;

        _timer += Runner.DeltaTime;
        if (_timer < sendInterval) return;
        _timer = 0f;

        Debug.Log($"[SendSnapshot] IsServer={Runner.IsServer} board={(GetMySimBoard()!=null)}");

        var bytes = BuildSnapshotBytes(myBoard, poolSizePerSide, Runner);
        RPC_SubmitSnapshot(bytes);
    }

    private void AutoBindBoards()
    {
        if (hostSimBoard != null && clientSimBoard != null) return;

        var boards = FindObjectsOfType<NetworkGameManager>(true);
        for (int i = 0; i < boards.Length; i++)
        {
            if (boards[i].Role == BoardRole.Host) hostSimBoard = boards[i];
            else if (boards[i].Role == BoardRole.Client) clientSimBoard = boards[i];
        }
    }

    private void AutoBindRoots()
    {
        if (hostRenderRoot == null)
            hostRenderRoot = GameObject.Find("HostRenderRoot")?.transform;

        if (clientRenderRoot == null)
            clientRenderRoot = GameObject.Find("ClientRenderRoot")?.transform;
    }

    private void ApplySkinsToNetworkBalls()
    {
        var balls = FindObjectsOfType<NetworkBalls>();

        foreach (var b in balls)
        {
            b.SetSkin(HostSkinId.ToString(), ClientSkinId.ToString());
        }
    }

    private void CachePlayerRefs()
    {
        if (!Runner.IsServer) return;

        var players = Runner.ActivePlayers.ToArray();
        if (players.Length >= 1) _hostRef = players[0];
        if (players.Length >= 2) _clientRef = players[1];

        if (_hostRef == default) _hostRef = Runner.LocalPlayer;
    }

    private void ApplyRemoteViewFlags()
    {
        if (Runner == null || !Runner.IsRunning) return;
        if (hostSimBoard == null || clientSimBoard == null) return;

        bool iAmHost = Runner.IsServer;

        if (iAmHost)
        {
            hostSimBoard.SetRemoteView(false);
            clientSimBoard.SetRemoteView(true);
        }
        else
        {
            hostSimBoard.SetRemoteView(true);
            clientSimBoard.SetRemoteView(false);
        }
    }

    public NetworkGameManager GetMySimBoard()
    {
        bool iAmHost = Runner.IsServer;
        return iAmHost ? hostSimBoard : clientSimBoard;
    }

    private void ServerCreatePoolsOnce()
    {
        if (!Runner.IsServer) return;
        if (_hostPool != null && _clientPool != null) return;

        if (networkBallPrefab == null) return;
        if (hostRenderRoot == null || clientRenderRoot == null) return;

        _hostPool = new NetworkBalls[poolSizePerSide];
        _clientPool = new NetworkBalls[poolSizePerSide];

        for (int i = 0; i < poolSizePerSide; i++)
        {
            _hostPool[i] = SpawnNetBall(0, (ushort)i, hostRenderRoot);
            _clientPool[i] = SpawnNetBall(1, (ushort)i, clientRenderRoot);
        }
    }

    private NetworkBalls SpawnNetBall(byte slot, ushort index, Transform parent)
    {
        var nb = Runner.Spawn(networkBallPrefab, Vector3.zero, Quaternion.identity, default);
        nb.transform.SetParent(parent, true);
        nb.ServerInit(slot, index);
        nb.ServerApply(false, 0, Vector2.zero, 1f, 0f);
        return nb;
    }

    private byte GetLocalSlot()
    {
        // ClientServer: 서버(호스트)=0, 클라=1
        return (byte)(Runner != null && Runner.IsRunning && Runner.IsServer ? 0 : 1);
    }

    private void HideLocalGhostPool()
    {
        byte localSlot = GetLocalSlot();
        var pool = (localSlot == 0) ? _hostPool : _clientPool;
        if (pool == null) return;

        for (int i = 0; i < pool.Length; i++)
            pool[i].SetVisible(false);
    }

    private static byte[] BuildSnapshotBytes(NetworkGameManager sim, int maxCount, NetworkRunner runner)
    {
        using var ms = new MemoryStream(512);
        using var bw = new BinaryWriter(ms);

        // slot을 payload에 직접 넣는다 (0=Host, 1=Client)
        byte mySlot = (byte)((runner != null && runner.IsRunning && runner.IsServer) ? 0 : 1);
        bw.Write(mySlot);

        bw.Write(sim.score);
        bw.Write((byte)(sim.is_over ? 1 : 0));

        int activeCount = 0;
        for (int i = 0; i < sim.pool_ball.Count; i++)
        {
            var b = sim.pool_ball[i];
            if (b != null && b.gameObject.activeSelf) activeCount++;
        }

        int count = Mathf.Min(activeCount, maxCount);
        bw.Write((ushort)count);

        int written = 0;
        for (int i = 0; i < sim.pool_ball.Count && written < count; i++)
        {
            var b = sim.pool_ball[i];
            if (b == null || !b.gameObject.activeSelf) continue;

            Vector3 lp = sim.transform.InverseTransformPoint(b.transform.position);

            bw.Write((ushort)written);
            bw.Write(lp.x);
            bw.Write(lp.y);
            bw.Write(b.transform.eulerAngles.z);
            bw.Write((byte)Mathf.Clamp(b.level, 0, 255));
            bw.Write(b.transform.localScale.x);

            written++;
        }

        return ms.ToArray();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitSnapshot(byte[] bytes, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;
        if (bytes == null || bytes.Length < 8) return;

        ServerCreatePoolsOnce();
        CachePlayerRefs();

        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        byte slot = br.ReadByte();
        int score = br.ReadInt32();
        bool dead = br.ReadByte() == 1;

        if (slot == 0) { HostScore = score; HostDead = dead; }
        else { ClientScore = score; ClientDead = dead; }

        int count = br.ReadUInt16();
        
        if (slot == 0) { HostScore = score; HostDead = dead; }
        else           { ClientScore = score; ClientDead = dead; }

        var pool = (slot == 0) ? _hostPool : _clientPool;
        var board = (slot == 0) ? hostSimBoard : clientSimBoard;

        for (int i = 0; i < pool.Length; i++)
            pool[i].ServerApply(false, 0, Vector2.zero, 1f, 0f);

        for (int k = 0; k < count; k++)
        {
            if (ms.Position + (2 + 4 + 4 + 4 + 1 + 4) > ms.Length) break;

            ushort poolIndex = br.ReadUInt16();
            float lx = br.ReadSingle();
            float ly = br.ReadSingle();
            float rotZ = br.ReadSingle();
            int level = br.ReadByte();
            float scale = br.ReadSingle();

            if (poolIndex >= pool.Length) continue;

            Vector3 world = board.transform.TransformPoint(new Vector3(lx, ly, 0f));
            pool[poolIndex].ServerApply(true, level, new Vector2(world.x, world.y), scale, rotZ);
        }

        Debug.Log($"[RPC_SubmitSnapshot] src={info.Source} score={score} dead={dead} count={count}");
        Debug.Log($"[Pools] hostPool={_hostPool?.Length} clientPool={_clientPool?.Length}");

    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_GiveUp(byte slot)
    {
        ServerApplyGiveUp(slot);
    }

    private void ServerApplyGiveUp(byte slot)
    {
        if (!Runner.IsServer) return;

        if (slot == 0)
            HostDead = true;
        else
            ClientDead = true;

        // 로컬 보드도 즉시 종료
        if (slot == 0) hostSimBoard?.Dead();
        else           clientSimBoard?.Dead();

        Debug.Log($"[GiveUp] slot={slot} applied");
    }

    private byte ResolveSlot(PlayerRef src)
    {
        // 호스트가 서버 권한으로 RPC를 보낼 때 src가 None으로 오는 케이스 보정
        if (src == default)
            return 0; // Host
        if (_hostRef == default) _hostRef = Runner.LocalPlayer;

        if (_clientRef == default)
        {
            foreach (var p in Runner.ActivePlayers)
                if (p != _hostRef) { _clientRef = p; break; }
        }

        if (src == _hostRef) return 0;
        return 1;
    }

    public VersusResult GetLocalResult()
    {
        bool iAmHost = Runner != null && Runner.IsRunning && Runner.IsServer;

        int myScore = iAmHost ? HostScore : ClientScore;
        int opScore = iAmHost ? ClientScore : HostScore;

        bool myDead = iAmHost ? HostDead : ClientDead;
        bool opDead = iAmHost ? ClientDead : HostDead;

        if (myDead && !opDead) return VersusResult.Lose;
        if (!myDead && opDead) return VersusResult.Win;

        if (myDead && opDead)
        {
            if (myScore > opScore) return VersusResult.Win;
            if (myScore < opScore) return VersusResult.Lose;
            return VersusResult.Draw;
        }

        if (myScore > opScore) return VersusResult.Win;
        if (myScore < opScore) return VersusResult.Lose;
        return VersusResult.Draw;
    }

    public int GetLocalScore()
    {
        bool iAmHost = Runner != null && Runner.IsRunning && Runner.IsServer;
        return iAmHost ? HostScore : ClientScore;
    }

    public int GetRemoteScore()
    {
        bool iAmHost = Runner != null && Runner.IsRunning && Runner.IsServer;
        return iAmHost ? ClientScore : HostScore;
    }

    public void GiveUp()
    {
        if (Runner == null || !Runner.IsRunning) return;

        byte mySlot = (byte)(Runner.IsServer ? 0 : 1);

        // 서버면 바로 처리, 클라는 RPC
        if (Runner.IsServer)
        {
            ServerApplyGiveUp(mySlot);
        }
        else
        {
            RPC_GiveUp(mySlot);
        }
    }
}