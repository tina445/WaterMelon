using System.Threading.Tasks;
using UnityEngine;

public class AppFacade : MonoBehaviour
{
    public static AppFacade I { get; private set; }

    private const string LOCAL_UID = "local_guest";

    [Header("Audio (optional)")]
    [SerializeField] private bool playMainBgmOnStart = true;
    [SerializeField] private AudioClip mainBgm;

    public AuthManager Auth { get; private set; }
    public UserDataService UserData { get; private set; }
    public AudioService Audio { get; private set; }
    public SkinService Skin { get; private set; }
    public RewardService reward { get; private set; }
    public GameModeType PendingGameMode { get; set; } = GameModeType.Normal;


#if UNITY_WEBGL && !UNITY_EDITOR
    private WebGLFirebaseProxy _webglProxy;
#endif

    private IUserDataBackend _localBackend;
    private IUserDataBackend _cloudBackend;

    public bool IsUserReady => UserData != null && UserData.CurrentProfile != null;
    public bool IsCloudUser => IsUserReady && UserData.CurrentProfile.uid != LOCAL_UID;
    public string LocalUid => LOCAL_UID;

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // ----- Audio -----
        Audio = GetComponent<AudioService>() ?? gameObject.AddComponent<AudioService>();
        Audio.InitializeFromPrefs();

        // ----- Auth/UserData wiring -----
        Auth = GetComponent<AuthManager>() ?? gameObject.AddComponent<AuthManager>();
        Skin = GetComponent<SkinService>() ?? gameObject.AddComponent<SkinService>();
        reward = GetComponent<RewardService>() ?? gameObject.AddComponent<RewardService>();

#if UNITY_WEBGL && !UNITY_EDITOR
        _webglProxy = GetComponent<WebGLFirebaseProxy>() ?? gameObject.AddComponent<WebGLFirebaseProxy>();
        _webglProxy.Init();

        Auth.Initialize(new WebGLAuthService(_webglProxy));
        _cloudBackend = new WebGLUserDataBackend(_webglProxy);
#else
        Auth.Initialize(new DummyEditorAuthService());
        _cloudBackend = new DummyEditorUserDataBackend();
#endif

        // 로컬 저장은 기존 DataManager(JSON) 사용
        var dm = GetComponent<DataManager>() ?? gameObject.AddComponent<DataManager>();
        _localBackend = new UserDataBackend(dm, LOCAL_UID);

        // UserDataService는 인스턴스를 유지하고 backend만 바꿔 끼우는 방식이 안전
        UserData = new UserDataService(_localBackend);
    }

    /// <summary>
    /// 메인 씬 시작 시 호출: "기기 로그인 기억" 기반으로 cloud/local 세션을 자동 구성.
    /// - 로그인 되어 있으면 Cloud
    /// - 아니면 Local Guest
    /// </summary>
    public async Task InitializeSessionAsync()
    {
        if (IsUserReady) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        // 로그인 상태 기억: currentUser uid 확인
        string uid = await CheckAuthUidAsync();
        if (!string.IsNullOrEmpty(uid))
        {
            UserData.SetBackend(_cloudBackend);
            await UserData.LoadUserProfileAsync(uid);
            Skin.InitializeFromProfile(UserData.CurrentProfile);
            return;
        }
#endif
        // 로그인 상태 아니면 로컬 게스트
        UserData.SetBackend(_localBackend);
        await UserData.LoadUserProfileAsync(LOCAL_UID);
        Skin.InitializeFromProfile(UserData.CurrentProfile);
    }

    /// <summary>
    /// 구글 로그인(이때만 Firebase/Firestore 사용) + 로컬 게스트 데이터 병합(점수/해금/포인트/스킨).
    /// </summary>
    public async Task SignInWithGoogleAndSyncAsync()
    {
        if (!IsUserReady)
        {
            UserData.SetBackend(_localBackend);
            await UserData.LoadUserProfileAsync(LOCAL_UID);
        }
        var local = UserData.CurrentProfile;

        var auth = await Auth.SignInWithGoogleAsync();

        UserData.SetBackend(_cloudBackend);
        var cloud = await UserData.LoadUserProfileAsync(auth.uid);

        MergeLocalIntoCloud(local, cloud);
        await UserData.SaveAsync();

        // 저장 후 최신 프로필 기준으로 적용
        await UserData.LoadUserProfileAsync(auth.uid);
        Skin.InitializeFromProfile(UserData.CurrentProfile);
    }

    /// <summary>
    /// 로그아웃 후 로컬 게스트로 전환.
    /// </summary>
    public async Task SignOutToLocalGuestAsync()
    {
        await Auth.SignOutAsync();

        UserData.SetBackend(_localBackend);
        await UserData.LoadUserProfileAsync(LOCAL_UID);

        Skin.InitializeFromProfile(UserData.CurrentProfile);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// WebGL에서 Firebase Auth currentUser uid 확인(로그인 기억 핵심).
    /// WebGLFirebaseProxy에 FB_CheckAuth + OnAuthState(uidOrEmpty) 콜백이 있어야 함.
    /// </summary>
    private Task<string> CheckAuthUidAsync()
    {
        var tcs = new TaskCompletionSource<string>();

        void Handler(string uidOrEmpty)
        {
            _webglProxy.AuthStateUid -= Handler;
            tcs.TrySetResult(uidOrEmpty ?? "");
        }

        _webglProxy.AuthStateUid += Handler;
        _webglProxy.CheckAuthState();

        return tcs.Task;
    }
#endif

    private static void MergeLocalIntoCloud(UserProfile local, UserProfile cloud)
    {
        if (local == null || cloud == null) return;

        // highScores: mode별 max
        if (local.highScores != null && cloud.highScores != null)
        {
            int n = Mathf.Min(local.highScores.Length, cloud.highScores.Length);
            for (int i = 0; i < n; i++)
                cloud.highScores[i] = Mathf.Max(cloud.highScores[i], local.highScores[i]);
        }

        // unlock: OR
        cloud.unlockEX = cloud.unlockEX || local.unlockEX;
        cloud.unlockHard = cloud.unlockHard || local.unlockHard;
        cloud.unlockHardEX = cloud.unlockHardEX || local.unlockHardEX;

        // points: 합산(정책 변경 가능)
        cloud.points = Mathf.Max(0, cloud.points + Mathf.Max(0, local.points));

        // skins: 합집합
        if (cloud.ownedSkins == null) cloud.ownedSkins = new System.Collections.Generic.List<string>();
        if (local.ownedSkins != null)
        {
            foreach (var s in local.ownedSkins)
                if (!cloud.ownedSkins.Contains(s)) cloud.ownedSkins.Add(s);
        }

        // selectedSkin: 로컬 우선(원하면 cloud 우선으로 변경)
        if (!string.IsNullOrEmpty(local.selectedSkinId))
            cloud.selectedSkinId = local.selectedSkinId;
    }

    // ---------- Editor fallback ----------
    private class DummyEditorAuthService : IAuthService
    {
        public Task<AuthResult> SignInGuestAsync() => Task.FromResult(new AuthResult { uid = "editor_guest", isGuest = true });
        public Task<AuthResult> SignInWithGoogleAsync() => Task.FromResult(new AuthResult { uid = "editor_google", isGuest = false });
        public Task SignOutAsync() => Task.CompletedTask;
    }

    private class DummyEditorUserDataBackend : IUserDataBackend
    {
        private UserProfile _p = UserProfile.CreateDefault("editor_google");

        public Task<UserProfile> LoadAsync(string uid) { _p.uid = uid; return Task.FromResult(_p); }
        public Task SaveAsync(UserProfile profile) { _p = profile; return Task.CompletedTask; }

        public Task UpdateBestScoreAsync(string uid, int modeIndex, int newScore)
        {
            _p.highScores[modeIndex] = Mathf.Max(_p.highScores[modeIndex], newScore);
            return Task.CompletedTask;
        }

        public Task AddPointsAsync(string uid, int delta)
        {
            _p.points = Mathf.Max(0, _p.points + delta);
            return Task.CompletedTask;
        }

        public Task SetSelectedSkinAsync(string uid, string skinId)
        {
            _p.selectedSkinId = skinId;
            if (!_p.ownedSkins.Contains(skinId)) _p.ownedSkins.Add(skinId);
            return Task.CompletedTask;
        }
    }
}
