using System;
using System.Threading.Tasks;
using UnityEngine;

public interface IUserDataBackend
{
    Task<UserProfile> LoadAsync(string uid);
    Task SaveAsync(UserProfile profile);
    Task UpdateBestScoreAsync(string uid, int modeIndex, int newScore);
    Task AddPointsAsync(string uid, int delta);
    Task SetSelectedSkinAsync(string uid, string skinId);
}

public class UserDataService
{
    private IUserDataBackend _backend;

    public string CurrentUid { get; private set; }
    public UserProfile CurrentProfile { get; private set; }
    public event Action<UserProfile> OnProfileChanged;
    public UserDataService(IUserDataBackend backend) => _backend = backend;

    public void SetBackend(IUserDataBackend backend, bool clearCache = true)
    {
        _backend = backend;
        if (clearCache)
        {
            CurrentUid = null;
            CurrentProfile = null;
        }
    }

    public async Task<UserProfile> LoadUserProfileAsync(string uid)
    {
        RequireBackend();
        CurrentUid = uid;
        CurrentProfile = await _backend.LoadAsync(uid);
        return CurrentProfile;
    }

    public async Task SaveAsync()
    {
        RequireBackend();
        if (CurrentProfile == null) return;
        await _backend.SaveAsync(CurrentProfile);
        OnProfileChanged?.Invoke(CurrentProfile);

    }

    // 추가: 세션이 없으면 로컬 게스트로 자동 세션 구성
    public async Task EnsureSessionAsync(string fallbackUid)
    {
        RequireBackend();

        if (!string.IsNullOrEmpty(CurrentUid) && CurrentProfile != null)
            return;

        if (string.IsNullOrEmpty(fallbackUid))
            throw new InvalidOperationException("fallbackUid is empty.");

        CurrentUid = fallbackUid;
        CurrentProfile = await _backend.LoadAsync(CurrentUid);
    }

    public async Task UpdateBestScoreAsync(int modeIndex, int score)
    {
        RequireSignedIn();
        await _backend.UpdateBestScoreAsync(CurrentUid, modeIndex, score);
        CurrentProfile = await _backend.LoadAsync(CurrentUid);
        OnProfileChanged?.Invoke(CurrentProfile);
    }

    public async Task AddPointsAsync(int delta)
    {
        RequireSignedIn();
        await _backend.AddPointsAsync(CurrentUid, delta);

        // 로컬 백엔드는 이미 Save까지 끝냈고(_dm.saveFile도 갱신),
        // cloud 백엔드도 보통 서버에 반영함. 여기서는 메모리만 동기화.
        if (CurrentProfile != null)
            CurrentProfile.points = Mathf.Max(0, CurrentProfile.points + delta);
        OnProfileChanged?.Invoke(CurrentProfile);
    }

    public async Task SetSelectedSkinAsync(string skinId)
    {
        RequireSignedIn();
        await _backend.SetSelectedSkinAsync(CurrentUid, skinId);
        CurrentProfile = await _backend.LoadAsync(CurrentUid);
        OnProfileChanged?.Invoke(CurrentProfile);
    }

    private void RequireSignedIn()
    {
        RequireBackend();
        if (string.IsNullOrEmpty(CurrentUid))
            throw new InvalidOperationException("No active user session. Call LoadUserProfileAsync first.");
    }

    private void RequireBackend()
    {
        if (_backend == null)
            throw new InvalidOperationException("No backend configured.");
    }
}
