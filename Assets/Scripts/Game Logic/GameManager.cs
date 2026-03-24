using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    [Header("Pooling")]
    public GameObject PreFab;
    public Transform Group;
    public GameObject E_PreFab;
    public Transform E_Group;
    public List<Balls> pool_ball = new List<Balls>();
    public List<ParticleSystem> pool_eff = new List<ParticleSystem>();
    [Range(1, 30)] public int pool_size = 16;
    public int poolCursor;
    public Balls lastball;

    [Header("Session State")]
    public int score;
    public int spawn_level;
    public int max_level;
    public bool is_play;
    public bool is_over;

    public event Action<int> OnScoreChanged;
    public event Action<int> OnBestChanged;
    public event Action<bool> OnGameOver;
    public event Action<GameModeType> OnModeApplied;

    // Balls가 참조하는 기존 플래그 유지 (구조 최대한 보존)
    public bool isEX;
    public bool isHard;

    [Header("Mode")]
    public GameModeType currentMode = GameModeType.Normal;
    private IGameModeStrategy strategy;

    [Header("UI (Game Scene)")]
    public InGameUI inGameUI;
    public GameObject line;
    public GameObject plane;

    public DataManager dataManager;

    protected virtual void Awake()
    {
        Application.targetFrameRate = 60;

        // 풀 생성
        for (int i = 0; i < pool_size; i++) Allocate_ball();

        // Main 씬에서 선택한 모드 적용
        currentMode = (AppFacade.I != null) ? AppFacade.I.PendingGameMode : GameModeType.Normal;
        ApplyMode(currentMode);
    }

    protected virtual void Start()
    {
        StartSession();
        
        // 세션 시작 시 UI 초기 밀어주기
        OnModeApplied?.Invoke(currentMode);
        OnScoreChanged?.Invoke(score);
        OnBestChanged?.Invoke(GetLocalBestScore());
    }

    // ===== Mode Apply =====
    protected void ApplyMode(GameModeType mode)
    {
        strategy = GameModeStrategyFactory.Create(mode);

        isEX = strategy.IsEX;
        isHard = strategy.IsHard;
    }

    public int ModeIndex(GameModeType mode) => mode switch
    {
        GameModeType.Normal => 0,
        GameModeType.Ex => 1,
        GameModeType.NormalHard => 2,
        GameModeType.ExHard => 3,
        _ => 0
    };

    // ===== Session Start =====
    protected void StartSession()
    {
        line.SetActive(true);
        plane.SetActive(true);

        score = 0;
        max_level = 0;
        spawn_level = 0;
        is_play = true;
        is_over = false;

        Invoke(nameof(next), 1.5f);
        StopCoroutine(nameof(UnlockWatcher));
        StartCoroutine(nameof(UnlockWatcher));
    }

    // ===== Pooling =====
    protected Balls Allocate_ball()
    {
        GameObject effObj = Instantiate(E_PreFab, E_Group);
        effObj.name = "Effect " + pool_eff.Count;
        var eff = effObj.GetComponent<ParticleSystem>();
        pool_eff.Add(eff);

        GameObject ballObj = Instantiate(PreFab, Group);
        ballObj.name = "Ball " + pool_ball.Count;
        var b = ballObj.GetComponent<Balls>();
        b.manager = this;
        b.effect = eff;

        ballObj.SetActive(false);
        pool_ball.Add(b);

        return b;
    }

    protected Balls get_ball()
    {
        for (int i = 0; i < pool_ball.Count; i++)
        {
            poolCursor = (poolCursor + 1) % pool_ball.Count;
            if (!pool_ball[poolCursor].gameObject.activeSelf)
                return pool_ball[poolCursor];
        }
        return Allocate_ball();
    }

    protected virtual void next()
    {
        if (is_over) return;

        lastball = get_ball();
        lastball.level = UnityEngine.Random.Range(0, spawn_level);
        lastball.gameObject.SetActive(true);
        lastball.transform.position = new Vector3(0, 6f, 0);

        AppFacade.I.Audio.PlayOneShot(AudioService.sfx.Next);

        StartCoroutine(next_waiting());
    }

    protected IEnumerator next_waiting()
    {
        while (lastball != null) yield return null;
        yield return new WaitForSeconds(2.5f);
        next();
    }

    public virtual Vector3 GetDragWorldPos(Vector3 screenPos, float ballRadiusWorld)
    {
        Vector3 movePos = Camera.main.ScreenToWorldPoint(screenPos);
        float left  = -5f + ballRadiusWorld * 2f;
        float right =  5f - ballRadiusWorld * 2f;

        movePos.x = Mathf.Clamp(movePos.x, left, right);
        movePos.y = 6f;
        movePos.z = 0;
        return movePos;
    }

    // ===== Input Hook =====
    public void Touchdown()
    {
        if (lastball == null) return;
        lastball.Drag();
    }

    public void Touchup()
    {
        if (lastball == null) return;
        lastball.Drop();
        lastball = null;
    }

    // ===== Rules helpers (Balls가 호출) =====
    public int MergeMaxLevelExclusive => strategy?.MergeMaxLevelExclusive ?? 10;

    public void ApplySpawnClamp()
    {
        if (strategy == null) return;
        if (spawn_level > strategy.MaxSpawnLevel) spawn_level = strategy.MaxSpawnLevel;
    }

    // ===== Dead / End =====
    public virtual void Dead()
    {
        if (is_over) return;
        is_over = true;
        StartCoroutine(Dead_R());
    }

    protected IEnumerator Dead_R()
    {
        Balls[] balls = FindObjectsOfType<Balls>();
        for (int i = 0; i < balls.Length; i++) balls[i].rgbd.simulated = false;

        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Hide(Vector3.up * 1000);
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(1);

        is_play = false;

        int idx = ModeIndex(currentMode);
        if (dataManager != null && dataManager.saveFile != null)
        {
            dataManager.Save(dataManager.saveFile);
        }

        int prevBest = GetLocalBestScore();

        if (score > prevBest)
        {
            dataManager.saveFile.highScores[idx] = score;
            dataManager.Save(dataManager.saveFile);
        }

        int best = GetLocalBestScore();

        OnBestChanged?.Invoke(best);
        OnGameOver?.Invoke(score > prevBest);

        // UI off/on
        plane?.SetActive(false);
        line?.SetActive(false);
        //inGameUI.endGroup.SetActive(true);
    }

    // “최대 레벨 도달 시 EX/Hard/HardEX 개방”
    private IEnumerator UnlockWatcher()
    {
        while (is_play && !is_over)
        {   
            // Normal 계열에서 10레벨 이상 도달
            if (!isEX && max_level > strategy?.MergeMaxLevelExclusive) 
            {
                if (!isHard)
                {
                    dataManager.saveFile.unlockEX = true;
                    dataManager.saveFile.unlockHard = true;
                }
                else
                {
                    dataManager.saveFile.unlockHardEX = true;
                }

                dataManager.Save(dataManager.saveFile);
                yield break;
            }

            yield return null;
        }
    }

    // ===== Reset (Game 씬 내 재시작만) =====
    public void Reset()
    {
        SceneManager.LoadScene("Game");
    }

    public int GetLocalBestScore()
    {
        if (dataManager?.saveFile?.highScores == null) return 0;
        int idx = Mathf.Clamp(ModeIndex(currentMode), 0, dataManager.saveFile.highScores.Length - 1);
        return dataManager.saveFile.highScores[idx];
    }

    public void AddScore(int delta)
    {
        score += delta;
        OnScoreChanged?.Invoke(score);
    }

    private void SaveBestIfNeededAndNotifyGameOver()
    {
        int prevBest = GetLocalBestScore();

        // (여기서 dataManager.saveFile.highScores[idx] 갱신 + dataManager.Save 호출)
        // ...

        int best = GetLocalBestScore();
        OnBestChanged?.Invoke(best);

        bool isNewBest = score > prevBest;
        OnGameOver?.Invoke(isNewBest);
    }
}
