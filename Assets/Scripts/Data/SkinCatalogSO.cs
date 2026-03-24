using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SuBakGame/Skin Catalog", fileName = "SkinCatalog")]
public class SkinCatalogSO : ScriptableObject
{
    [Serializable]
    public class SkinEntry
    {
        [Header("Identity")]
        public string id = "default";
        public string displayName = "Default";
        public int price = 0;

        [Header("Sprites By Level (0..MaxLevel)")]
        public Sprite[] normalSprites; // Normal/Hard에서 사용
        public Sprite[] exSprites;     // EX/HardEX에서 사용 (없으면 normalSprites fallback)
    }

    [SerializeField] private List<SkinEntry> skins = new();

    public SkinEntry Get(string id)
    {
        if (string.IsNullOrEmpty(id)) id = "default";
        for (int i = 0; i < skins.Count; i++)
            if (skins[i] != null && skins[i].id == id) return skins[i];

        // 못 찾으면 default 탐색
        for (int i = 0; i < skins.Count; i++)
            if (skins[i] != null && skins[i].id == "default") return skins[i];

        return null;
    }

    public Sprite ResolveSprite(string skinId, bool isEX, int level)
    {
        var entry = Get(skinId);
        if (entry == null) return null;

        Sprite[] arr = null;

        if (isEX && entry.exSprites != null && entry.exSprites.Length > 0) arr = entry.exSprites;
        else arr = entry.normalSprites;

        if (arr == null || arr.Length == 0) return null;

        int idx = Mathf.Clamp(level, 0, arr.Length - 1);
        return arr[idx];
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (var s in skins)
            if (s != null && s.id == id) return true;
        return false;
    }
}
