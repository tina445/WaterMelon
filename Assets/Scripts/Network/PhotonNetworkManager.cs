using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static PhotonNetworkManager I { get; private set; }

    [Header("Runner")]
    [SerializeField] private NetworkRunner runnerPrefab;     // 비워도 됨(런타임 생성)
    [SerializeField] private NetworkObject versusMatchManagerPrefab;
    private NetworkObject _spawnedVmm;
    [SerializeField] private bool dontDestroy = true;

    [Header("Match (Fill-Room 1v1)")]
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private int queueId = 1;

    [Tooltip("WebGL에서는 보통 ClientServer Lobby가 가장 무난합니다.")]
    [SerializeField] private SessionLobby lobby = SessionLobby.ClientServer;

    [Header("Timing (ms)")]
    [SerializeField] private int sessionListWaitMs = 1200;
    [SerializeField] private int recheckAfterJitterMs = 450;
    [SerializeField] private int createJitterMinMs = 120;
    [SerializeField] private int createJitterMaxMs = 450;
    [SerializeField] private int retryCount = 12;

    [Header("Scenes")]
    [SerializeField] private int mainSceneIndex = 0;
    [SerializeField] private int versusSceneIndex = 2;

    public event Action<string> OnStatus;

    public NetworkRunner Runner => _runner;
    public bool IsRunning => _runner != null && _runner.IsRunning;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneMgr;

    // session list
    private readonly List<SessionInfo> _sessions = new();
    private TaskCompletionSource<bool> _sessionListTcs;

    // match loop
    private CancellationTokenSource _matchCts;
    private Task _matchTask;

    private enum MatchState { Idle, JoiningLobby, Matching, InRoom, Cancelling }
    private MatchState _state = MatchState.Idle;

    // -------------------------
    // Unity lifecycle
    // -------------------------

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        if (dontDestroy) DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    // -------------------------
    // Public API
    // -------------------------

    public Task StartRandomMatchAsync()
    {
        // 이미 매칭 루프가 돌고 있으면 재시작하지 않음(중복 호출 방지)
        if (_matchTask != null && !_matchTask.IsCompleted)
        {
            Debug.Log($"[Match] Start ignored. state={_state}");
            return _matchTask;
        }

        _matchCts?.Cancel();
        _matchCts = new CancellationTokenSource();

        _matchTask = RunMatchLoopAsync(_matchCts.Token);
        return _matchTask;
    }

    public async Task CancelMatchAsync()
    {
        Debug.Log($"[Match] Cancel requested. state={_state}");
        SetState(MatchState.Cancelling, "Canceling...");

        // 1) 루프에 취소 전달
        _matchCts?.Cancel();

        // 2) 루프가 끝나길 잠깐 기다림(무한 대기 방지)
        if (_matchTask != null && !_matchTask.IsCompleted)
        {
            await WhenAnySafe(_matchTask, DelayMs(1500, CancellationToken.None));
        }

        // 3) Runner는 확실히 내린다 (WebGL에서 꼬임 방지)
        await ShutdownRunnerSafeAsync();

        _matchTask = null;
        _matchCts = null;

        SetState(MatchState.Idle, "Canceled.");
    }

    // -------------------------
    // Core match loop
    // -------------------------

    private async Task RunMatchLoopAsync(CancellationToken ct)
    {
        try
        {
            Debug.Log("[Match] 1) RunMatchLoopAsync entered");

            await ResetRunnerAsync(ct);

            SetState(MatchState.JoiningLobby, "Connecting lobby...");
            var lobbyRes = await _runner.JoinSessionLobby(lobby);
            Debug.Log($"[Match] 2) JoinLobby({lobby}) ok={lobbyRes.Ok} reason={lobbyRes.ShutdownReason}");

            if (!lobbyRes.Ok)
            {
                SetState(MatchState.Idle, $"Lobby failed: {lobbyRes.ShutdownReason}");
                await ShutdownRunnerSafeAsync();
                return;
            }

            SetState(MatchState.Matching, "Finding match...");
            string prefix = GetQueuePrefix(queueId);

            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                Debug.Log($"[Match] 3) attempt={attempt} wait session list...");
                await WaitSessionListOnceAsync(sessionListWaitMs, ct);
                Debug.Log($"[Match] 4) session list count={_sessions.Count}");

                // A) join 가능한 방 있으면 join (1/2 우선)
                var target = PickJoinable(prefix, maxPlayers);
                if (target != null)
                {
                    SetState(MatchState.Matching, "Joining room...");
                    Debug.Log($"[Match] 5) Try Join '{target.Name}' {target.PlayerCount}/{target.MaxPlayers}");

                    var join = await _runner.StartGame(new StartGameArgs
                    {
                        GameMode = GameMode.Client,
                        SessionName = target.Name,
                        SceneManager = _sceneMgr
                    });

                    Debug.Log($"[Match] 6) Join result ok={join.Ok} reason={join.ShutdownReason}");

                    if (join.Ok)
                    {
                        SetState(MatchState.InRoom, "Waiting opponent...");
                        return; // 상대 들어오면 OnPlayerJoined에서 처리
                    }

                    // join 실패 → runner 재사용 금지/상태 꼬임 방지: 무조건 리셋
                    await ResetRunnerAndRelobbyAsync(ct);
                    await DelayMs(200, ct);
                    continue;
                }

                // B) 없으면 지터 후 재확인(레이스 완화)
                int jitter = UnityEngine.Random.Range(createJitterMinMs, createJitterMaxMs + 1);
                Debug.Log($"[Match] 7) No room. jitter={jitter}ms then recheck...");
                await DelayMs(jitter, ct); // ★ WebGL-safe delay

                await WaitSessionListOnceAsync(recheckAfterJitterMs, ct);
                target = PickJoinable(prefix, maxPlayers);
                if (target != null)
                {
                    Debug.Log("[Match] 8) Found room after jitter. loop back to join.");
                    attempt--;
                    continue;
                }

                // C) 그래도 없으면 create
                string sessionName = MakeRandomSessionName(prefix);
                SetState(MatchState.Matching, "Creating room...");
                Debug.Log($"[Match] 9) Try Create '{sessionName}'");

                var host = await _runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = sessionName,
                    PlayerCount = maxPlayers,
                    SceneManager = _sceneMgr
                });

                Debug.Log($"[Match] 10) Create result ok={host.Ok} reason={host.ShutdownReason}");

                if (host.Ok)
                {
                    SetState(MatchState.InRoom, "Waiting opponent...");
                    return; // 상대 들어오면 OnPlayerJoined에서 처리
                }

                // create 실패 → runner 망가질 수 있으니 리셋
                await ResetRunnerAndRelobbyAsync(ct);
                await DelayMs(300, ct);
            }

            SetState(MatchState.Idle, "Match failed. Retry.");
            await ShutdownRunnerSafeAsync();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[Match] Canceled (OperationCanceledException).");
            SetState(MatchState.Idle, "Match canceled.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Match] Fatal error: {e}");
            SetState(MatchState.Idle, "Match error.");
            await ShutdownRunnerSafeAsync();
        }
        finally
        {
            // 루프가 끝나면 다음 Start를 막지 않도록 정리
            _matchTask = null;
        }
    }

    // -------------------------
    // Runner lifecycle
    // -------------------------

    private void EnsureRunner()
    {
        if (_runner != null) return;

        _runner = runnerPrefab != null
            ? Instantiate(runnerPrefab)
            : new GameObject("NetworkRunner").AddComponent<NetworkRunner>();

        if (dontDestroy) DontDestroyOnLoad(_runner.gameObject);

        _sceneMgr = _runner.GetComponent<NetworkSceneManagerDefault>();
        if (_sceneMgr == null) _sceneMgr = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        Debug.Log("[Fusion] Runner created");
    }

    private async Task ResetRunnerAsync(CancellationToken ct)
    {
        await ShutdownRunnerSafeAsync();
        ct.ThrowIfCancellationRequested();

        EnsureRunner();
        _sessions.Clear();
        _sessionListTcs = null;
    }

    private async Task ResetRunnerAndRelobbyAsync(CancellationToken ct)
    {
        await ResetRunnerAsync(ct);
        ct.ThrowIfCancellationRequested();

        var res = await _runner.JoinSessionLobby(lobby);
        Debug.Log($"[Match] ReJoinLobby({lobby}) ok={res.Ok} reason={res.ShutdownReason}");
    }

    private async Task ShutdownRunnerSafeAsync()
    {
        if (_runner == null) return;

        try
        {
            // Shutdown이 WebGL에서 가끔 길어질 수 있어 타임아웃 처리
            var shutdownTask = _runner.Shutdown();
            await WhenAnySafe(shutdownTask, DelayMs(2500, CancellationToken.None));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Fusion] Shutdown exception: {e.Message}");
        }

        try { _runner.RemoveCallbacks(this); } catch { }
        try { Destroy(_runner.gameObject); } catch { }

        _runner = null;
        _sceneMgr = null;
        _sessions.Clear();
        _sessionListTcs = null;
    }

    // -------------------------
    // WebGL-safe delay helpers
    // -------------------------

    private Task DelayMs(int ms, CancellationToken ct)
    {
        // Task.Delay 대신 Unity Coroutine 기반으로 지연(재개 보장)
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(DelayRoutine(ms, ct, tcs));
        return tcs.Task;
    }

    private IEnumerator DelayRoutine(int ms, CancellationToken ct, TaskCompletionSource<bool> tcs)
    {
        float end = Time.realtimeSinceStartup + (ms / 1000f);
        while (Time.realtimeSinceStartup < end)
        {
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                yield break;
            }
            yield return null;
        }
        tcs.TrySetResult(true);
    }

    private static async Task WhenAnySafe(Task a, Task b)
    {
        // WebGL에서 Task.WhenAny는 문제 없지만, 명시적으로 분리해 둠
        await Task.WhenAny(a, b);
    }

    // -------------------------
    // Session list helpers
    // -------------------------

    private async Task WaitSessionListOnceAsync(int waitMs, CancellationToken ct)
    {
        _sessionListTcs = new TaskCompletionSource<bool>();

        // 이미 리스트가 있으면 즉시 완료
        if (_sessions.Count > 0)
            _sessionListTcs.TrySetResult(true);

        var delay = DelayMs(waitMs, ct);
        await WhenAnySafe(_sessionListTcs.Task, delay);
    }

    private static string GetQueuePrefix(int qid) => $"q{qid}_";
    private static string MakeRandomSessionName(string prefix)
        => prefix + Guid.NewGuid().ToString("N").Substring(0, 8);

    private SessionInfo PickJoinable(string prefix, int maxP)
    {
        return _sessions
            .Where(s =>
                !string.IsNullOrEmpty(s.Name) &&
                s.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                s.MaxPlayers == maxP &&
                s.PlayerCount < s.MaxPlayers
            )
            .OrderByDescending(s => s.PlayerCount) // 1/2 우선
            .FirstOrDefault();
    }

    private void SetState(MatchState s, string uiMsg)
    {
        _state = s;
        if (!string.IsNullOrEmpty(uiMsg))
            OnStatus?.Invoke(uiMsg);

        Debug.Log($"[Match] state -> {_state} msg='{uiMsg}'");
    }

    // -------------------------
    // INetworkRunnerCallbacks
    // -------------------------

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        _sessions.Clear();
        if (sessionList != null) _sessions.AddRange(sessionList);

        Debug.Log($"[Fusion] SessionListUpdated lobby={lobby} count={_sessions.Count}");
        _sessionListTcs?.TrySetResult(true);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] PlayerJoined: {player} IsServer={runner.IsServer} Active={runner.ActivePlayers.Count()}");

        // Host에서만 2명 되면 Versus로 이동
        if (!runner.IsServer) return;

        if (runner.ActivePlayers.Count() >= maxPlayers)
        {
            Debug.Log("[Fusion] Match ready -> Load Versus scene");
            runner.LoadScene(SceneRef.FromIndex(versusSceneIndex));
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Fusion] PlayerLeft: {player}");
        // 간단 처리: 메인으로 복귀
        if (SceneManager.GetActiveScene().buildIndex != mainSceneIndex)
            SceneManager.LoadScene(mainSceneIndex);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[Fusion] OnShutdown reason={shutdownReason}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner)
    {
        Debug.Log("[Fusion] OnDisconnectedFromServer");
        OnStatus?.Invoke("Disconnected.");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[Fusion] OnDisconnectedFromServer reason={reason}");
        OnStatus?.Invoke($"Disconnected: {reason}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"[Fusion] ConnectFailed: {reason}");
        OnStatus?.Invoke($"Connect failed: {reason}");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner == null) return;

        // Versus 씬이 아니면 스킵
        if (SceneManager.GetActiveScene().buildIndex != versusSceneIndex)
        {
            _spawnedVmm = null;
            return;
        }

        // 서버만 스폰
        if (!runner.IsServer) return;

        // 이미 스폰된 경우 중복 방지
        if (_spawnedVmm != null && _spawnedVmm.IsValid)
            return;

        // 혹시 씬에 남아있는 VMM(SceneObject) 방어
        if (FindObjectOfType<VersusMatchManager>(true) != null)
        {
            Debug.Log("[PhotonManager] VMM already exists in scene. Skip spawn.");
            return;
        }

        if (versusMatchManagerPrefab == null)
        {
            Debug.LogError("[PhotonManager] VersusMatchManager prefab is NULL");
            return;
        }

        _spawnedVmm = runner.Spawn(
            versusMatchManagerPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        Debug.Log("[PhotonManager] Spawned VersusMatchManager (VMM)");
    }


    // ---- unused ----
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
