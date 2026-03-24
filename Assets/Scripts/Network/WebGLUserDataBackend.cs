using System.Threading.Tasks;
using UnityEngine;

public class WebGLUserDataBackend : IUserDataBackend
{
    private readonly WebGLFirebaseProxy _proxy;

    public WebGLUserDataBackend(WebGLFirebaseProxy proxy) => _proxy = proxy;

    public Task<UserProfile> LoadAsync(string uid)
    {
        var tcs = new TaskCompletionSource<UserProfile>();

        void Missing()
        {
            Cleanup();
            tcs.TrySetResult(UserProfile.CreateDefault(uid));
        }

        void OkJson(string json)
        {
            Cleanup();
            var p = JsonUtility.FromJson<UserProfile>(json);
            if (string.IsNullOrEmpty(p.uid)) p.uid = uid;
            if (p.highScores == null || p.highScores.Length != 4) p.highScores = new int[4] { 0, 0, 0, 0 };
            if (p.ownedSkins == null) p.ownedSkins = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(p.selectedSkinId)) p.selectedSkinId = "default";
            if (!p.ownedSkins.Contains("default")) p.ownedSkins.Add("default");
            tcs.TrySetResult(p);
        }

        void Err(string e)
        {
            Cleanup();
            tcs.TrySetException(new System.Exception(e));
        }

        void Cleanup()
        {
            _proxy.ProfileMissing -= Missing;
            _proxy.ProfileJson -= OkJson;
            _proxy.FirestoreError -= Err;
        }

        _proxy.ProfileMissing += Missing;
        _proxy.ProfileJson += OkJson;
        _proxy.FirestoreError += Err;

        _proxy.GetProfile(uid);
        return tcs.Task;
    }

    public Task SaveAsync(UserProfile profile)
    {
        var tcs = new TaskCompletionSource<bool>();

        void Ok()
        {
            Cleanup();
            tcs.TrySetResult(true);
        }

        void Err(string e)
        {
            Cleanup();
            tcs.TrySetException(new System.Exception(e));
        }

        void Cleanup()
        {
            _proxy.SaveOk -= Ok;
            _proxy.FirestoreError -= Err;
        }

        _proxy.SaveOk += Ok;
        _proxy.FirestoreError += Err;

        // Firestore doc id는 uid로 고정 → json에 uid를 반드시 포함
        profile.uid ??= "";
        string json = JsonUtility.ToJson(profile);
        _proxy.SaveProfile(profile.uid, json);

        return tcs.Task;
    }

    public async Task UpdateBestScoreAsync(string uid, int modeIndex, int newScore)
    {
        var p = await LoadAsync(uid);
        if (modeIndex >= 0 && modeIndex < p.highScores.Length)
        {
            if (newScore > p.highScores[modeIndex]) p.highScores[modeIndex] = newScore;
        }
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
        p.selectedSkinId = skinId;
        if (!p.ownedSkins.Contains(skinId)) p.ownedSkins.Add(skinId);
        await SaveAsync(p);
    }
}
