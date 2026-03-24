using System;
using System.Collections.Generic;

[Serializable]
public class UserProfile
{
    public string uid;
    public string nickname = "Guest";
    public int points = 0;

    // 0: Normal, 1: EX, 2: Hard, 3: Insane
    public int[] highScores = new int[4];

    public List<string> ownedSkins = new List<string>();
    public string selectedSkinId = "default";

    public bool unlockEX, unlockHard, unlockHardEX;

    public static UserProfile CreateDefault(string uid)
    {
        return new UserProfile
        {
            uid = uid,
            nickname = "Guest",
            points = 0,
            highScores = new int[4] { 0, 0, 0, 0 },
            ownedSkins = new List<string> { "default" },
            selectedSkinId = "default",
            unlockEX = false, 
            unlockHard = false, 
            unlockHardEX = false
        };
    }
}
