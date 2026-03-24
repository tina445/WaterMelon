using System;
using UnityEngine;

public class SkinService : MonoBehaviour
{
    [SerializeField] private SkinCatalogSO catalog;

    public event Action<string> OnSkinChanged;

    public SkinCatalogSO Catalog => catalog;
    public string CurrentSkinId { get; private set; } = "default";

    public void InitializeFromProfile(UserProfile p)
    {
        if (p == null) return;
        SetCurrentSkin(p.selectedSkinId);
    }

    public void SetCurrentSkin(string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) skinId = "default";

        // 카탈로그에 없으면 default로 강제
        if (catalog != null && !catalog.Contains(skinId))
            skinId = "default";

        if (CurrentSkinId == skinId) return;

        CurrentSkinId = skinId;
        OnSkinChanged?.Invoke(CurrentSkinId);
    }

    public Sprite GetSprite(bool isEX, int level)
    {
        if (catalog == null) return null;
        return catalog.ResolveSprite(CurrentSkinId, isEX, level);
    }

    // Balls/NetworkGameManager가 직접 호출해도 됨
    public void ApplySprite(Balls b, bool isEX)
    {
        if (b == null || b.spr == null) return;
        var sp = GetSprite(isEX, b.level);
        if (sp != null) b.spr.sprite = sp;
    }
}
