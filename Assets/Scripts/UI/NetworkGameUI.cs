using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkGameUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RewardService rewardService;
    [SerializeField] private VersusMatchManager match;

    private GameManager _myManager;
    private bool _bound;

    [Header("Score UI")]
    [SerializeField] private Text leftScoreText;
    [SerializeField] private Text rightScoreText;

    [Header("End UI")]
    [SerializeField] private GameObject endGroup;
    [SerializeField] private Text endLeftScoreText;
    [SerializeField] private Text endRightScoreText;
    [SerializeField] private Text endResultText;

    [Header("Points Earned UI")]
    [SerializeField] private Text earnedPointsText;
    private int _earnedPoints;

    [Header("Buttons")]
    [SerializeField] private Button backToMainBtn;
    [SerializeField] private Button giveUpBtn;

    private bool _resultShown;

    private void Awake()
    {
        if (!rewardService) rewardService = FindObjectOfType<RewardService>(true);
        if (!match) match = FindObjectOfType<VersusMatchManager>(true);

        _earnedPoints = 0;
        RefreshEarnedPointsUI();
    }

    private void Start()
    {
        if (endGroup) endGroup.SetActive(false);

        if (backToMainBtn) backToMainBtn.onClick.AddListener(BackToMain);
        if (giveUpBtn) giveUpBtn.onClick.AddListener(GiveUp);

    }

    private void OnDestroy()
    {
        UnbindReward();
    }

    private void Update()
    {
        if (match == null)
        {
            match = FindObjectOfType<VersusMatchManager>(true);
            if (match == null) return;
        }

        // 아직 바인딩 안 됐는데 match가 늦게 생기는 경우 대비
        if (!rewardService)
            rewardService = (AppFacade.I != null && AppFacade.I.reward != null)
                ? AppFacade.I.reward
                : FindObjectOfType<RewardService>(true);

        if (!match.HasSpawned) return;

        if (_myManager == null)
        {
            var b = match.GetMySimBoard();
            if (b != null) _myManager = b;
        }

        if (leftScoreText) leftScoreText.text = match.HostScore.ToString();
        if (rightScoreText) rightScoreText.text = match.ClientScore.ToString();

        if (!_resultShown && match.MatchEnded)
        {
            _resultShown = true;
            ShowResult(match.GetLocalResult());
        }
    }

    // EventTrigger 연결용
    public void OnPointerDown()
    {
        if (_resultShown) return;
        _myManager?.Touchdown();
    }

    public void OnPointerUp()
    {
        if (_resultShown) return;
        _myManager?.Touchup();
    }

    private void ShowResult(VersusResult r)
    {
        if (endGroup) endGroup.SetActive(true);

        int host = match.HostScore;
        int client = match.ClientScore;

        if (endLeftScoreText) endLeftScoreText.text = host.ToString();
        if (endRightScoreText) endRightScoreText.text = client.ToString();

        if (endResultText)
        {
            endResultText.text = r switch
            {
                VersusResult.Win => "WIN",
                VersusResult.Lose => "LOSE",
                VersusResult.Draw => "DRAW",
                _ => ""
            };
        }
    }

    // =========================
    // Points (지속 표시)
    // =========================
    private void OnPointsGranted(int amount, string reason)
    {
        if (amount <= 0) return;

        _earnedPoints += amount;
        RefreshEarnedPointsUI();
    }

    private void RefreshEarnedPointsUI()
    {
        if (!earnedPointsText) return;
        earnedPointsText.text = _earnedPoints.ToString();
    }

    private void TryBindReward()
    {
        if (_bound) return;
        if (rewardService == null) return;
        if (match == null) return;

        // 중복 방지
        rewardService.OnPointsGranted -= OnPointsGranted;

        // 대전 세션 연결 (RewardService가 match.MatchEnded 감시/지급)
        rewardService.BindVersus(match);
        rewardService.OnPointsGranted += OnPointsGranted;

        _bound = true;
    }

    private void UnbindReward()
    {
        if (!_bound) return;

        if (rewardService != null)
        {
            rewardService.OnPointsGranted -= OnPointsGranted;
            rewardService.UnbindVersus();
        }

        _bound = false;
    }

    private void BackToMain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    private void GiveUp()
    {
        Time.timeScale = 1f;
        match?.GiveUp();
    }
}
