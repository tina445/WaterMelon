using System.Threading.Tasks;
using UnityEngine;

public class UserDataBackend : IUserDataBackend
{
    private readonly DataManager _dm;
    private readonly string _localUid;

    public UserDataBackend(DataManager dm, string localUid = "local_guest")
    {
        _dm = dm;
        _localUid = localUid;
    }

    public Task<UserProfile> LoadAsync(string uid)
    {
        var d = _dm.saveFile;
        if (d == null) d = Data.CreateDefault();

        // Data -> UserProfile
        var p = UserProfile.CreateDefault(_localUid);

        // 기본 프로필들
        p.nickname = string.IsNullOrEmpty(d.nickname) ? "Guest" : d.nickname;
        p.points = Mathf.Max(0, d.points);

        // 점수/해금
        p.highScores = (int[])d.highScores.Clone();
        p.unlockEX = d.unlockEX;
        p.unlockHard = d.unlockHard;
        p.unlockHardEX = d.unlockHardEX;

        // 스킨
        p.ownedSkins = d.ownedSkins != null
            ? new System.Collections.Generic.List<string>(d.ownedSkins)
            : new System.Collections.Generic.List<string>();

        p.selectedSkinId = string.IsNullOrEmpty(d.selectedSkinId) ? "default" : d.selectedSkinId;

        // 일관성 보정
        if (!p.ownedSkins.Contains("default")) p.ownedSkins.Add("default");
        if (!p.ownedSkins.Contains(p.selectedSkinId)) p.ownedSkins.Add(p.selectedSkinId);

        return Task.FromResult(p);
    }

    public Task SaveAsync(UserProfile profile)
    {
        // UserProfile -> Data
        var d = _dm.saveFile;
        if (d == null) d = Data.CreateDefault();

        d.nickname = string.IsNullOrEmpty(profile.nickname) ? "Guest" : profile.nickname;
        d.points = Mathf.Max(0, profile.points);

        if (profile.highScores != null && profile.highScores.Length == 4)
            d.highScores = (int[])profile.highScores.Clone();

        d.unlockEX = profile.unlockEX;
        d.unlockHard = profile.unlockHard;
        d.unlockHardEX = profile.unlockHardEX;

        d.selectedSkinId = string.IsNullOrEmpty(profile.selectedSkinId) ? "default" : profile.selectedSkinId;

        d.ownedSkins = profile.ownedSkins != null
            ? new System.Collections.Generic.List<string>(profile.ownedSkins)
            : new System.Collections.Generic.List<string>();

        _dm.Save(d);
        _dm.saveFile = d;

        return Task.CompletedTask;
    }

    public async Task UpdateBestScoreAsync(string uid, int modeIndex, int newScore)
    {
        var p = await LoadAsync(uid);
        if (modeIndex >= 0 && modeIndex < p.highScores.Length)
            p.highScores[modeIndex] = Mathf.Max(p.highScores[modeIndex], newScore);
        await SaveAsync(p);
    }

    public async Task AddPointsAsync(string uid, int delta)
    {
        var p = await LoadAsync(uid);
        p.points = Mathf.Max(0, p.points + delta);
        await SaveAsync(p);
    }

    public async Task SetSelectedSkinAsync(string uid, string skinId)
    {
        var p = await LoadAsync(uid);

        skinId = string.IsNullOrEmpty(skinId) ? "default" : skinId;
        p.selectedSkinId = skinId;

        if (p.ownedSkins == null) p.ownedSkins = new System.Collections.Generic.List<string>();
        if (!p.ownedSkins.Contains(skinId)) p.ownedSkins.Add(skinId);

        await SaveAsync(p);
    }
}
