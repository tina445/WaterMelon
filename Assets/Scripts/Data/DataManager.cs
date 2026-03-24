using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public string path;
    public Data saveFile;

    private void Awake()
    {
        path = Application.persistentDataPath + "/";
        InitializeData(path);
    }

    public void InitializeData(string path, bool resetData = false)
    {
        // playerprefs initialization
        string[] playerprefKeys = { "IsMuted_bgm", "IsMuted_sfx", "IsMuted_attach" };

        foreach (var elem in playerprefKeys)
        {
            if (!PlayerPrefs.HasKey(elem) || resetData)
                PlayerPrefs.SetInt(elem, 0);
        }

        // 볼륨 기본값
        PlayerPrefs.SetFloat("volumeBGM", 0.3f);
        PlayerPrefs.SetFloat("volumeSFX", 1f);

        // json file initialization
        if (!File.Exists(path + "data") || resetData)
        {
            Debug.Log("Initialization Activated");

            Data playerData = Data.CreateDefault();
            Save(playerData);
        }

        saveFile = Load();
        Save(saveFile);
    }

    public void Save(Data data)
    {
        string jsonData = JsonUtility.ToJson(data);
        File.WriteAllText(path + "data", jsonData);
    }

    public Data Load()
    {
        string data = File.ReadAllText(path + "data");
        var loaded = JsonUtility.FromJson<Data>(data);
        if (loaded == null) loaded = Data.CreateDefault();
        return loaded;
    }
}

[System.Serializable]
public class Data
{
    // ===== 기존 필드 =====
    public int[] highScores;
    public bool unlockEX;
    public bool unlockHard;
    public bool unlockHardEX;

    public int points;
    public string nickname;
    public List<string> ownedSkins;
    public string selectedSkinId;

    public static Data CreateDefault()
    {
        return new Data
        {
            highScores = new int[] { 0, 0, 0, 0 },
            unlockEX = false,
            unlockHard = false,
            unlockHardEX = false,

            points = 0,
            nickname = "Guest",
            ownedSkins = new List<string> { "default" },
            selectedSkinId = "default"
        };
    }
}
