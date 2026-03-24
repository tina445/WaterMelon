using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("Currency UI")]
    [SerializeField] private Text pointsText;

    [Header("Skin List UI")]
    [SerializeField] private Transform skinListRoot;
    [SerializeField] private ShopSkinItemView skinItemPrefab;

    [Header("Catalog Source")]
    [SerializeField] private SkinCatalogSO catalog;

    [Header("Entries (for list build)")]
    [SerializeField] private List<SkinListEntry> entries = new List<SkinListEntry>();

    [Header("Optional")]
    [SerializeField] private SkinService skinService;

    private readonly List<ShopSkinItemView> _spawned = new List<ShopSkinItemView>();
    private bool _busy;

    [System.Serializable]
    public class SkinListEntry
    {
        public string id = "default";
        public string displayName = "Default";
        public int price = 0;
        public Sprite preview;
    }

    private void Awake()
    {
        if (skinService == null) skinService = FindObjectOfType<SkinService>(true);
    }

    private void OnEnable()
    {
        if (AppFacade.I != null && AppFacade.I.UserData != null)
            AppFacade.I.UserData.OnProfileChanged += OnProfileChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (AppFacade.I != null && AppFacade.I.UserData != null)
            AppFacade.I.UserData.OnProfileChanged -= OnProfileChanged;
    }

    private void OnProfileChanged(UserProfile p)
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshCurrencyUI();
        RebuildSkinListUI();
    }

    private void RefreshCurrencyUI()
    {
        var p = AppFacade.I != null ? AppFacade.I.UserData?.CurrentProfile : null;
        if (pointsText) pointsText.text = (p != null ? p.points : 0).ToString();
    }

    private void RebuildSkinListUI()
    {
        if (skinListRoot == null || skinItemPrefab == null)
        {
            Debug.LogWarning("[ShopUI] skinListRoot or skinItemPrefab is not assigned.");
            return;
        }

        var ud = AppFacade.I != null ? AppFacade.I.UserData : null;
        var profile = ud != null ? ud.CurrentProfile : null;
        if (profile == null)
        {
            Debug.LogWarning("[ShopUI] CurrentProfile is null. (Session not initialized?)");
            return;
        }

        if (catalog == null && skinService != null) catalog = skinService.Catalog;

        ClearSpawned();

        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[ShopUI] entries is empty. Add SkinListEntry items in Inspector.");
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            string id = string.IsNullOrEmpty(e.id) ? "default" : e.id;

            if (catalog != null && !catalog.Contains(id))
                continue;

            var view = Instantiate(skinItemPrefab, skinListRoot);
            view.gameObject.name = $"SkinItem_{id}";
            _spawned.Add(view);

            bool owned = profile.ownedSkins != null && profile.ownedSkins.Contains(id);
            bool selected = profile.selectedSkinId == id;

            Sprite preview = e.preview;
            if (preview == null && catalog != null)
            {
                preview = catalog.ResolveSprite(id, false, 0);
                if (preview == null)
                    preview = catalog.ResolveSprite(id, true, 0);
            }

            view.Bind(
                skinId: id,
                displayName: string.IsNullOrEmpty(e.displayName) ? id : e.displayName,
                preview: preview,
                price: Mathf.Max(0, e.price),
                owned: owned,
                selected: selected,
                onClick: OnClickSkin
            );
        }

        LayoutRebuild();
    }

    private void LayoutRebuild()
    {
        var rt = skinListRoot as RectTransform;
        if (rt == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private async void OnClickSkin(string skinId)
    {
        if (_busy) return;
        _busy = true;

        try
        {
            var ud = AppFacade.I.UserData;
            var p = ud.CurrentProfile;
            if (p == null) return;

            bool owned = p.ownedSkins != null && p.ownedSkins.Contains(skinId);
            bool selected = p.selectedSkinId == skinId;

            if (selected)
                return;

            int price = GetPrice(skinId);

            if (!owned)
            {
                if (p.points < price)
                {
                    Debug.Log("[ShopUI] Not enough points.");
                    return;
                }

                await ud.AddPointsAsync(-price);
            }

            await ud.SetSelectedSkinAsync(skinId);

            if (skinService != null)
                skinService.SetCurrentSkin(skinId);

            RefreshAll();
        }
        finally
        {
            _busy = false;
        }
    }

    private int GetPrice(string skinId)
    {
        if (entries == null) return 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.id == skinId) return Mathf.Max(0, e.price);
        }
        return 0;
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();
    }
}
