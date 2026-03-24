using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class MainMenuUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DataManager dataManager;

    [Header("UI")]
    [SerializeField] private Text warnText;   // 기존 WarnText와 동일하게 연결
    [SerializeField] private Button normalBtn;
    [SerializeField] private Button exBtn;
    [SerializeField] private Button hardBtn;
    [SerializeField] private Button hardExBtn;
    [SerializeField] private Button modeSwapBtn;
    [SerializeField] private Button optionBtn;
    [SerializeField] private Button mainMenuBtn;
    [SerializeField] private Button versusMatchBtn;
    [SerializeField] private Button cancelMatchBtn;
    [SerializeField] private Button shopBtn;
    [SerializeField] private Button closeShopBtn;
    [SerializeField] private Button googleLoginBtn;

    [Header("Best Score Texts")]
    [SerializeField] private Text bestNormal;
    [SerializeField] private Text bestEx;
    [SerializeField] private Text bestHard;
    [SerializeField] private Text bestHardEx;

    [Header("Panels")]
    [SerializeField] private GameObject[] mainPanel;
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private GameObject matchingGroup;
    [SerializeField] private GameObject shopGroup;

    [Header("Option Audio UI")]
    [SerializeField] private Slider optionBgmSlider;
    [SerializeField] private Slider optionSfxSlider;
    [SerializeField] private Toggle optionMuteBgmToggle;
    [SerializeField] private Toggle optionMuteSfxToggle;

    [Header("Matching UI")]
    
    [SerializeField] private Text matchingText;

    private const string KEY_MUTE_BGM = "IsMuted_bgm";
    private const string KEY_MUTE_SFX = "IsMuted_sfx";
    private const string KEY_VOL_BGM  = "Volume_BGM";
    private const string KEY_VOL_SFX  = "Volume_SFX";
    private bool _optionAudioBound;

    private void Awake()
    {
        if (!dataManager) dataManager = FindObjectOfType<DataManager>();
    }

    private async void Start()
    {
        HookButtons();
        BindOptionAudioOnce();

        if (AppFacade.I != null)
            await AppFacade.I.InitializeSessionAsync();

        RefreshBestScores();
    }

    private void HookButtons()
    {
        if (normalBtn) normalBtn.onClick.AddListener(() => TryStart(GameModeType.Normal));
        if (exBtn)     exBtn.onClick.AddListener(() => TryStart(GameModeType.Ex));
        if (hardBtn)   hardBtn.onClick.AddListener(() => TryStart(GameModeType.NormalHard));
        if (hardExBtn) hardExBtn.onClick.AddListener(() => TryStart(GameModeType.ExHard));
        if (modeSwapBtn) modeSwapBtn.onClick.AddListener(() => SwapMode());
        if (optionBtn) optionBtn.onClick.AddListener(() => SwapOption());
        if (mainMenuBtn) mainMenuBtn.onClick.AddListener(() => SwapOption());
        if (shopBtn) shopBtn.onClick.AddListener(() => OpenShop());
        if (closeShopBtn) closeShopBtn.onClick.AddListener(() => OpenShop());
        if (versusMatchBtn)
        {
            versusMatchBtn.onClick.AddListener(async () =>
            {
                if (PhotonNetworkManager.I == null)
                {
                    SetMatching(true, "NetworkManager missing");
                    return;
                }

                SetMatching(true, "Finding match...");
                await PhotonNetworkManager.I.StartRandomMatchAsync();

                // 방 참가/생성 후에도 계속 "매칭중"으로 두고,
                // 2명 모이면 PhotonNetworkManager가 Versus 씬으로 넘어가면서 UI는 자연히 사라짐(씬 전환)
                SetMatching(true, "Waiting opponent...");
            });
        }

        if (cancelMatchBtn)
        {
            cancelMatchBtn.onClick.AddListener(async () =>
            {
                if (PhotonNetworkManager.I != null)
                    await PhotonNetworkManager.I.CancelMatchAsync();

                SetMatching(false, "");
            });
        }

        if (googleLoginBtn)
        {
            googleLoginBtn.onClick.AddListener(async () =>
            {
                try
                {
                    await AppFacade.I.SignInWithGoogleAndSyncAsync();
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }
    }

    public void SwapMode()
    {
        if (mainPanel[1].activeSelf)
        {
            mainPanel[1].SetActive(false);
            mainPanel[2].SetActive(true);
        }
        else
        {
            mainPanel[1].SetActive(true);
            mainPanel[2].SetActive(false);
        }
    }

    public void SwapOption()
    {
        if (mainPanel[0].activeSelf)
        {
            mainPanel[0].SetActive(false);
            optionPanel.SetActive(true);

            RefreshOptionAudioUI();
            AppFacade.I?.Audio?.ApplyFromPrefs();
        }
        else
        {
            mainPanel[0].SetActive(true);
            optionPanel.SetActive(false);
        }
    }

    public void OpenShop()
    {
        if (mainPanel[0].activeSelf)
        {
            mainPanel[0].SetActive(false);
            shopGroup.SetActive(true);
        }
        else
        {
            mainPanel[0].SetActive(true);
            shopGroup.SetActive(false);
        }
    }

    private void RefreshBestScores()
    {
        if (dataManager == null || dataManager.saveFile == null) return;

        var hs = dataManager.saveFile.highScores;
        if (bestNormal && hs.Length > 0) bestNormal.text = hs[0].ToString();
        if (bestEx && hs.Length > 1)     bestEx.text     = hs[1].ToString();
        if (bestHard && hs.Length > 2)   bestHard.text   = hs[2].ToString();
        if (bestHardEx && hs.Length > 3) bestHardEx.text = hs[3].ToString();
    }

    private void TryStart(GameModeType mode)
    {
        if (!IsModeUnlocked(mode))
        {
            StartCoroutine(messege());
            return;
        }

        AppFacade.I.PendingGameMode = mode;
        SceneManager.LoadScene("Game");
    }

    private bool IsModeUnlocked(GameModeType mode)
    {
        var d = dataManager.saveFile;
        if (d == null) return false;

        return mode switch
        {
            GameModeType.Normal => true,
            GameModeType.Ex => d.unlockEX,
            GameModeType.NormalHard => d.unlockHard,
            GameModeType.ExHard => d.unlockHard && d.unlockHardEX,
            _ => true
        };
    }

    private IEnumerator messege()
    {
        if (!warnText) yield break;

        warnText.color = new Color(0, 0.22f, 0.44f, 1);
        yield return new WaitForSeconds(0.5f);

        for (float f = 1; f >= 0; f -= 0.032f)
        {
            warnText.color = new Color(0, 0.22f, 0.44f, f);
            yield return null;
        }
    }

    private void BindOptionAudioOnce()
    {
        if (_optionAudioBound) return;
        _optionAudioBound = true;

        if (optionBgmSlider)
            optionBgmSlider.onValueChanged.AddListener(v => AppFacade.I?.Audio.SetVolumeBgm(v));
        if (optionSfxSlider)
            optionSfxSlider.onValueChanged.AddListener(v => AppFacade.I?.Audio.SetVolumeSfx(v));

        if (optionMuteBgmToggle)
            optionMuteBgmToggle.onValueChanged.AddListener(isOn => AppFacade.I?.Audio.SetMuteBgm(isOn));
        if (optionMuteSfxToggle)
            optionMuteSfxToggle.onValueChanged.AddListener(isOn => AppFacade.I?.Audio.SetMuteSfx(isOn));

        RefreshOptionAudioUI(); // 최초 1회
    }

    private void RefreshOptionAudioUI()
    {
        // AudioService에 getter가 없으니 PlayerPrefs에서 읽어서 UI 갱신
        float bgm = PlayerPrefs.GetFloat(KEY_VOL_BGM, 0.3f);
        float sfx = PlayerPrefs.GetFloat(KEY_VOL_SFX, 1.0f);
        bool muteBgm = PlayerPrefs.GetInt(KEY_MUTE_BGM, 0) == 1;
        bool muteSfx = PlayerPrefs.GetInt(KEY_MUTE_SFX, 0) == 1;

        optionBgmSlider?.SetValueWithoutNotify(bgm);
        optionSfxSlider?.SetValueWithoutNotify(sfx);
        optionMuteBgmToggle?.SetIsOnWithoutNotify(muteBgm);
        optionMuteSfxToggle?.SetIsOnWithoutNotify(muteSfx);
    }

    private void SetMatching(bool on, string msg)
    {
        if (matchingGroup) matchingGroup.SetActive(on);
        if (matchingText) matchingText.text = msg;
    }

}
