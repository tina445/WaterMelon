using System;
using System.Threading.Tasks;
using UnityEngine;

public class RewardService : MonoBehaviour
{
    [Header("Solo Reward Tuning")]
    [SerializeField] private int soloBaseDivisor = 10;
    [SerializeField] private int newBestBonus = 50;

    [Header("Versus Reward Tuning")]
    [SerializeField] private int winBase = 200;
    [SerializeField] private int loseBase = 50;
    [SerializeField] private int drawBase = 100;

    public event Action<int, string> OnPointsGranted;

    private bool _soloGranted;
    private bool _versusGranted;

    private GameManager _boundSolo;
    private VersusMatchManager _boundVersus;

    private bool _watchVersusEnd;

    public void BindSolo(GameManager gm)
    {
        UnbindSolo();

        _boundSolo = gm;
        _soloGranted = false;

        if (_boundSolo != null)
            _boundSolo.OnGameOver += OnSoloGameOver;
    }

    public void UnbindSolo()
    {
        if (_boundSolo != null)
            _boundSolo.OnGameOver -= OnSoloGameOver;

        _boundSolo = null;
        _soloGranted = false;
    }

    public void BindVersus(VersusMatchManager mm)
    {
        UnbindVersus();

        _boundVersus = mm;
        _versusGranted = false;
        _watchVersusEnd = (mm != null);
    }

    public void UnbindVersus()
    {
        _boundVersus = null;
        _versusGranted = false;
        _watchVersusEnd = false;
    }

    private void Update()
    {
        if (!_watchVersusEnd) return;
        if (_versusGranted) return;
        if (_boundVersus == null) return;

        if (!_boundVersus.HasSpawned) return;

        if (_boundVersus.MatchEnded)
        {
            _versusGranted = true;
            var r = _boundVersus.GetLocalResult();
            _ = GrantVersusAsync(_boundVersus, r);
        }
    }

    // =========================
    // Solo
    // =========================
    private void OnSoloGameOver(bool isNewBest)
    {
        Debug.Log($"[Reward] OnSoloGameOver newBest={isNewBest} inst={GetInstanceID()}");
        Debug.Log($"[Reward] OnSoloGameOver frame={Time.frameCount} score={_boundSolo?.score}");

        if (_soloGranted) return;
        _soloGranted = true;

        _ = GrantSoloAsync(_boundSolo, isNewBest);
    }

    private async Task GrantSoloAsync(GameManager gm, bool isNewBest)
    {
        if (gm == null) return;

        int score = gm.score;
        int reward = Mathf.Max(0, score / Mathf.Max(1, soloBaseDivisor));

        reward = Mathf.RoundToInt(reward);
        if (isNewBest) reward += newBestBonus;
        if (reward <= 0) return;

        Debug.Log($"[Reward] Invoke OnPointsGranted reward={reward} inst={GetInstanceID()}");
        OnPointsGranted?.Invoke(reward, isNewBest ? "NEW_BEST" : "SOLO");

        if (AppFacade.I == null || AppFacade.I.UserData == null) return;
        await AppFacade.I.UserData.EnsureSessionAsync(AppFacade.I.LocalUid);
        await AppFacade.I.UserData.AddPointsAsync(reward);
    }

    // =========================
    // Versus
    // =========================
    private async Task GrantVersusAsync(VersusMatchManager mm, VersusResult result)
    {
        if (mm == null) return;
        if (AppFacade.I == null || AppFacade.I.UserData == null) return;

        bool iAmHost = mm.Runner != null && mm.Runner.IsRunning && mm.Runner.IsServer;

        int myScore = iAmHost ? mm.HostScore : mm.ClientScore;
        int opScore = iAmHost ? mm.ClientScore : mm.HostScore;

        int reward = result switch
        {
            VersusResult.Win => winBase + Mathf.Max(0, myScore / 20),
            VersusResult.Lose => loseBase + Mathf.Max(0, myScore / 40),
            VersusResult.Draw => drawBase + Mathf.Max(0, myScore / 30),
            _ => 0
        };

        int diff = Mathf.Abs(myScore - opScore);
        reward += Mathf.Clamp(diff / 50, 0, 50);

        if (reward <= 0) return;

        await AppFacade.I.UserData.AddPointsAsync(reward);
        OnPointsGranted?.Invoke(reward, result.ToString().ToUpperInvariant());
    }
}
