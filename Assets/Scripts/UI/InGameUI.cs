using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RewardService rewardService;

    [Header("Texts")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text bestText;
    [SerializeField] private Text modeText;

    [Header("End UI")]
    public GameObject endGroup;
    [SerializeField] private Text endScoreText;
    [SerializeField] private Text endBestText;
    [SerializeField] private GameObject newBestObj;

    [Header("Points Earned UI")]
    [SerializeField] private Text earnedPointsText; // 코인 옆 숫자(Text)에 연결

    [Header("Pause UI")]
    [SerializeField] private GameObject pauseGroup;

    [Header("Buttons")]
    [SerializeField] private Button pauseBtn;
    [SerializeField] private Button resumeBtn;
    [SerializeField] private Button retryBtn;
    [SerializeField] private Button backToMainBtn;
    [SerializeField] private Button giveUpBtn;

    [Header("Pause Option Audio UI")]
    [SerializeField] private Slider pauseBgmSlider;
    [SerializeField] private Slider pauseSfxSlider;
    [SerializeField] private Toggle pauseMuteBgmToggle;
    [SerializeField] private Toggle pauseMuteSfxToggle;

    private const string KEY_MUTE_BGM = "IsMuted_bgm";
    private const string KEY_MUTE_SFX = "IsMuted_sfx";
    private const string KEY_VOL_BGM = "Volume_BGM";
    private const string KEY_VOL_SFX = "Volume_SFX";

    private bool _pauseAudioBound;
    private bool isPaused;

    private int _earnedPoints;
    private bool _rewardBound;

    private void Awake()
    {
        if (!gameManager) gameManager = FindObjectOfType<GameManager>(true);

        // AppFacade에 RewardService 붙여놨으면 여기서 바로 잡힘
        if (!rewardService)
            rewardService = AppFacade.I.reward;

        _earnedPoints = 0;
        RefreshEarnedPointsUI();
    }

    private void Start()
    {
        endGroup?.SetActive(false);
        pauseGroup?.SetActive(false);
        if (newBestObj) newBestObj.SetActive(false);

        gameManager.OnScoreChanged += SetScore;
        gameManager.OnBestChanged += SetBest;
        gameManager.OnGameOver += ShowEnd;
        gameManager.OnModeApplied += SetModeLabel;

        pauseBtn?.onClick.AddListener(Pause);
        resumeBtn?.onClick.AddListener(Resume);
        retryBtn?.onClick.AddListener(Retry);
        backToMainBtn?.onClick.AddListener(BackToMain);
        giveUpBtn?.onClick.AddListener(GiveUp);

        SetModeLabel(gameManager.currentMode);
        SetScore(gameManager.score);
        SetBest(gameManager.GetLocalBestScore());

        BindPauseAudioOnce();

        BindRewardOnce();

        Debug.Log($"[UI] SubscribeReward rewardInst={rewardService.GetInstanceID()}");
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnScoreChanged -= SetScore;
            gameManager.OnBestChanged -= SetBest;
            gameManager.OnGameOver -= ShowEnd;
            gameManager.OnModeApplied -= SetModeLabel;
        }

        UnbindReward();
    }

    private void BindRewardOnce()
    {
        if (_rewardBound) return;
        if (rewardService == null || gameManager == null) return;

        rewardService.OnPointsGranted -= OnPointsGranted;
        rewardService.BindSolo(gameManager);
        rewardService.OnPointsGranted += OnPointsGranted;

        _rewardBound = true;
    }

    private void UnbindReward()
    {
        if (!_rewardBound) return;

        if (rewardService != null)
        {
            rewardService.OnPointsGranted -= OnPointsGranted;
            rewardService.UnbindSolo();
        }

        _rewardBound = false;
    }

    private void OnPointsGranted(int delta, string reason)
    {
        if (delta <= 0) return;
        Debug.Log($"[UI] OnPointsGranted +{delta} reason={reason}");
        _earnedPoints += delta;
        RefreshEarnedPointsUI();
    }

    private void RefreshEarnedPointsUI()
    {
        if (!earnedPointsText) return;
        earnedPointsText.text = _earnedPoints.ToString();
    }

    private void SetScore(int s)
    {
        if (scoreText) scoreText.text = s.ToString();
    }

    private void SetBest(int best)
    {
        if (bestText) bestText.text = best.ToString();
    }

    private void SetModeLabel(GameModeType mode)
    {
        if (!modeText) return;

        modeText.gameObject.SetActive(mode != GameModeType.Normal);
        modeText.text = mode switch
        {
            GameModeType.Ex => "EXTRA",
            GameModeType.NormalHard => "HARD",
            GameModeType.ExHard => "INSANE",
            _ => ""
        };
    }

    private void ShowEnd(bool isNewBest)
    {
        AppFacade.I.Audio.PlayOneShot(AudioService.sfx.End);

        if (newBestObj) newBestObj.SetActive(isNewBest);
        if (endGroup) endGroup.SetActive(true);

        if (endScoreText) endScoreText.text = gameManager.score.ToString();
        if (endBestText) endBestText.text = gameManager.GetLocalBestScore().ToString();

        // 끝화면 열릴 때도 현재 누적값 반영(이후 보상 이벤트 오면 다시 갱신됨)
        RefreshEarnedPointsUI();
    }

    public void OnPointerDown() => gameManager.Touchdown();
    public void OnPointerUp() => gameManager.Touchup();

    private void BindPauseAudioOnce()
    {
        if (_pauseAudioBound) return;
        _pauseAudioBound = true;

        if (pauseBgmSlider)
            pauseBgmSlider.onValueChanged.AddListener(v => AppFacade.I?.Audio.SetVolumeBgm(v));
        if (pauseSfxSlider)
            pauseSfxSlider.onValueChanged.AddListener(v => AppFacade.I?.Audio.SetVolumeSfx(v));

        if (pauseMuteBgmToggle)
            pauseMuteBgmToggle.onValueChanged.AddListener(isOn => AppFacade.I?.Audio.SetMuteBgm(isOn));
        if (pauseMuteSfxToggle)
            pauseMuteSfxToggle.onValueChanged.AddListener(isOn => AppFacade.I?.Audio.SetMuteSfx(isOn));

        RefreshPauseAudioUI();
    }

    private void RefreshPauseAudioUI()
    {
        float bgm = PlayerPrefs.GetFloat(KEY_VOL_BGM, 0.3f);
        float sfx = PlayerPrefs.GetFloat(KEY_VOL_SFX, 1.0f);
        bool muteBgm = PlayerPrefs.GetInt(KEY_MUTE_BGM, 0) == 1;
        bool muteSfx = PlayerPrefs.GetInt(KEY_MUTE_SFX, 0) == 1;

        pauseBgmSlider?.SetValueWithoutNotify(bgm);
        pauseSfxSlider?.SetValueWithoutNotify(sfx);
        pauseMuteBgmToggle?.SetIsOnWithoutNotify(muteBgm);
        pauseMuteSfxToggle?.SetIsOnWithoutNotify(muteSfx);
    }

    private void Pause()
    {
        if (isPaused) return;

        isPaused = true;
        pauseGroup?.SetActive(true);

        RefreshPauseAudioUI();
        AppFacade.I?.Audio?.ApplyFromPrefs();

        Time.timeScale = 0f;
    }

    private void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        pauseGroup?.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Game");
    }

    private void BackToMain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    private void GiveUp()
    {
        pauseGroup?.SetActive(false);
        Time.timeScale = 1f;
        gameManager.Dead();
    }
}
