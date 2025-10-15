using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public void InitializeData(string path, bool resetData = false)
    {
        //playerprefs initialization
        string[] playerprefKeys = { "IsMuted_bgm", "IsMuted_sfx", "IsMuted_attach", "currGameMod" };

        foreach (var elem in playerprefKeys)
        {
            if (!PlayerPrefs.HasKey(elem) || resetData)
            {
                PlayerPrefs.SetInt(elem, 0);
            }
        }

        PlayerPrefs.SetFloat("volumeBGM", 0.3f);
        PlayerPrefs.SetFloat("volumeSFX", 1f);

        //json file initialization
        if (!File.Exists(path + "data") || resetData)
        {
            Debug.Log("Initialization Activated");

            Data playerData = new Data()
            {
                highScores = new int[] { 0, 0, 0, 0 },
                unlockEX = false,
                unlockHard = false,
                unlockHardEX = false
            };

            Save(playerData);
        }

        saveFile = Load();
    }

    public void Save(Data data)
    {
        string jsonData = JsonUtility.ToJson(data);
        File.WriteAllText(path + "data", jsonData);
    }

    public Data Load()
    {
        string data = File.ReadAllText(path + "data");
        return JsonUtility.FromJson<Data>(data);
    }

    void Awake()
    {
        path = Application.persistentDataPath + "/";
        InitializeData(path);
    }

    public string path;
    public Data saveFile;
}

public class Data
{
    public int[] highScores;
    public bool unlockEX;
    public bool unlockHard;
    public bool unlockHardEX;
}