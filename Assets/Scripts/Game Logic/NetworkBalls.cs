using Fusion;
using UnityEngine;

public class NetworkBalls : NetworkBehaviour
{
    [Header("Skin")]
    [SerializeField] private SkinCatalogSO skinCatalog;

    private string _hostSkinId = "default";
    private string _clientSkinId = "default";

    [Networked] public byte OwnerSlot { get; private set; }
    [Networked] public ushort PoolIndex { get; private set; }

    [Networked] public NetworkBool NetActive { get; private set; }
    [Networked] public byte NetLevel { get; private set; }
    [Networked] public Vector2 NetPos { get; private set; }
    [Networked] public float NetScale { get; private set; }
    [Networked] public float NetRotZ { get; private set; }

    private Renderer[] _renderers;
    private Animator[] _animators;
    private int _lastLevel = -1;
    private bool _lastVisible;

    private static readonly int ANIM_LEVEL = Animator.StringToHash("level");

    public override void Spawned()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _animators = GetComponentsInChildren<Animator>(true);

        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.simulated = false;
        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        // 클라에서도 루트에 붙이기 (부모는 네트워크로 안 올 수 있음)
        var hostRoot = GameObject.Find("HostRenderRoot")?.transform;
        var clientRoot = GameObject.Find("ClientRenderRoot")?.transform;

        if (OwnerSlot == 0 && hostRoot != null)
            transform.SetParent(hostRoot, true);
        else if (OwnerSlot == 1 && clientRoot != null)
            transform.SetParent(clientRoot, true);

        SetVisible(false);
    }

    public override void Render()
    {
        if (Runner != null && Runner.IsRunning)
        {
            byte localSlot = (byte)(Runner.IsServer ? 0 : 1);
            if (OwnerSlot == localSlot)
            {
                SetVisible(false);   // 잔상 방지
                return;
            }
        }

        bool visible = NetActive;

        if (visible != _lastVisible)
        {
            SetVisible(visible);
            _lastVisible = visible;
        }

        if (!visible) return;

        transform.position = new Vector3(NetPos.x, NetPos.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, NetRotZ);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, NetScale);

        if (_lastLevel != NetLevel)
        {
            _lastLevel = NetLevel;

            ApplySkinSprite(NetLevel);
            
            if (_animators != null)
            {
                for (int i = 0; i < _animators.Length; i++)
                {
                    var a = _animators[i];
                    if (!a) continue;
                    a.SetInteger(ANIM_LEVEL, NetLevel);
                }
            }
        }
    }

    public void SetSkin(string hostSkinId, string clientSkinId)
    {
        _hostSkinId = string.IsNullOrEmpty(hostSkinId) ? "default" : hostSkinId;
        _clientSkinId = string.IsNullOrEmpty(clientSkinId) ? "default" : clientSkinId;

        _lastLevel = -1; // 강제 갱신
    }

    private void ApplySkinSprite(int level)
    {
        if (skinCatalog == null) return;

        string skinId = OwnerSlot == 0 ? _hostSkinId : _clientSkinId;

        var sprite = skinCatalog.ResolveSprite(
            skinId,
            false,        // EX 여부는 NetworkBalls에서는 필요 없음 (애니메이터가 처리)
            level
        );

        if (sprite == null) return;

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var sr = _renderers[i] as SpriteRenderer;
                if (sr != null)
                    sr.sprite = sprite;
            }
        }
    }


    public void SetVisible(bool v)
    {
        if (_renderers != null)
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i]) _renderers[i].enabled = v;

        if (_animators != null)
            for (int i = 0; i < _animators.Length; i++)
                if (_animators[i]) _animators[i].enabled = v;
    }

    public void ServerInit(byte ownerSlot, ushort poolIndex)
    {
        OwnerSlot = ownerSlot;
        PoolIndex = poolIndex;

        NetActive = false;
        NetLevel = 0;
        NetPos = Vector2.zero;
        NetScale = 1f;
        NetRotZ = 0f;
    }

    public void ServerApply(bool active, int level, Vector2 pos, float scale, float rotZ)
    {
        NetActive = active;
        NetLevel = (byte)Mathf.Clamp(level, 0, 255);
        NetPos = pos;
        NetScale = Mathf.Clamp(scale, 0.01f, 10f);
        NetRotZ = rotZ;
    }
}