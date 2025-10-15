using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("----------[object pooling]")]
    public GameObject PreFab;
    public Transform Group;
    public GameObject E_PreFab;
    public Transform E_Group;
    public List<Balls> pool_ball;
    public List<ParticleSystem> pool_eff;
    [Range(1, 30)]
    public int pool_size;
    public int poolCursor;
    public Balls lastball;

    [Header("----------[core]")]
    public int score;
    public int spawn_level;
    public int max_level;
    public bool is_started;
    public int currentMode;
    public bool isEX;
    public bool isHard;
    public bool is_play;
    public bool is_over;
    public bool play_attach_sound;
    DataManager dataManager;

    [Header("----------[audio]")]
    public AudioSource bgmPlayer;
    public AudioSource[] sfxPlayer;
    public AudioClip[] sfxClip;
    public enum sfx { LevelUp, Next, Attach, Tada, Click, End };
    public float Volume_BGM;
    public float Volume_SFX;
    int sfxCursor;

    [Header("object group")]
    [Header("----------[UI]")]

    public GameObject end_group;
    public GameObject[] start_group;
    public GameObject eff_group;
    public GameObject pause_group;
    public GameObject option_group;
    public GameObject warning_group;
    public GameObject quit_group;

    [Header("button")]
    public Button pause;
    public Button BgmMute_on;
    public Button BgmMute_off;
    public Button pause_BgmMute_on;
    public Button pause_BgmMute_off;
    public Button SfxMute_on;
    public Button SfxMute_off;
    public Button pause_SfxMute_on;
    public Button pause_SfxMute_off;
    public Button AttachMute_on;
    public Button AttachMute_off;
    public Button pause_AttachMute_on;
    public Button pause_AttachMute_off;
    public Button changeButton;

    [Header("slider")]
    public Slider Bgm_slider;
    public Slider pause_Bgm_slider;
    public Slider Sfx_slider;
    public Slider pause_Sfx_slider;

    [Header("texts")]
    public Text extra_text;
    public Text current_score;
    public Text[] max_score;
    public Text mode;
    public Text subtext;
    public Text WarnText;
    public Text new_best;

    [Header("----------[etc.]")]
    public GameObject Line;
    public GameObject Plane;
    public Sprite normalImage;
    public Sprite hardImage;
    public bool detLine;

    Balls Allocate_ball()
    {
        GameObject instant_effobj = Instantiate(E_PreFab, E_Group);
        instant_effobj.name = "Effect " + pool_eff.Count;
        ParticleSystem instant_eff = instant_effobj.GetComponent<ParticleSystem>();
        pool_eff.Add(instant_eff);

        GameObject instant_ballobj = Instantiate(PreFab, Group);
        instant_ballobj.name = "Ball " + pool_ball.Count;
        Balls instant_ball = instant_ballobj.GetComponent<Balls>();
        instant_ball.manager = this;
        instant_ball.effect = instant_eff;
        pool_ball.Add(instant_ball);

        return instant_ball;
    }

    Balls get_ball()
    {
        for (int i = 0; i < pool_ball.Count; i++)
        {
            poolCursor = (poolCursor + 1) % pool_ball.Count;
            if (!pool_ball[poolCursor].gameObject.activeSelf)
            {
                return pool_ball[poolCursor];
            }
        }
        return Allocate_ball();
    }

    void next()
    {
        if (is_over)
        {
            return;
        }
        lastball = get_ball();
        lastball.level = UnityEngine.Random.Range(0, spawn_level);
        lastball.gameObject.SetActive(true);
        lastball.transform.position = new Vector3(0, 6.75f, 0);
        sfxPlay(sfx.Next);

        StartCoroutine(next_waiting());
    }

    IEnumerator next_waiting()
    {
        while (lastball != null)
        {
            yield return null;
        }
        yield return new WaitForSeconds(2.5f);

        next();
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        pool_ball = new List<Balls>();
        pool_eff = new List<ParticleSystem>();

        for (int i = 0; i < pool_size; i++)
        {
            Allocate_ball();
        }

        dataManager = gameObject.GetComponent<DataManager>();
        changeButton.image = GetComponent<Image>();
    }

    void Start()
    {
        Debug.Log(PlayerPrefs.GetInt("currGameMod"));
        Time.timeScale = 1.25f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        StartCoroutine(EnableIcon_bgm());
        StartCoroutine(EnableIcon_sfx());
        StartCoroutine(EnableIcon_attach());

        max_score[0].text = dataManager.saveFile.highScores[0].ToString();
        max_score[1].text = dataManager.saveFile.highScores[1].ToString();

        if (!is_play)
        {
            PlayerPrefs.SetInt("currGameMod", 0);
        }
        else if (is_over)
        {
            GameStart();
        }

    }

    public void SelectMode0()
    {
        PlayerPrefs.SetInt("currGameMod", 1);
    }
    public void SelectMode1()
    {
        PlayerPrefs.SetInt("currGameMod", 2);
    }

    public void SelectMode2()
    {
        PlayerPrefs.SetInt("currGameMod", 3);
    }

    public void SelectMode3()
    {
        PlayerPrefs.SetInt("currGameMod", 4);
    }

    public void GameStart()
    {
        sfxPlay(sfx.Click);

        switch (PlayerPrefs.GetInt("currGameMod"))
        {
            case 2:
                if (dataManager.saveFile.unlockEX)
                {
                    isEX = true;
                    mode.gameObject.SetActive(true);
                    mode.text = "EXTRA";
                }
                else
                {
                    StartCoroutine(messege());
                    return;
                }
                break;
            case 3:
                if (dataManager.saveFile.unlockHard)
                {
                    isHard = true;
                    mode.gameObject.SetActive(true);
                    mode.text = "HARD";
                }
                else
                {
                    StartCoroutine(messege());
                    return;
                }
                break;
            case 4:
                if (dataManager.saveFile.unlockHard && dataManager.saveFile.unlockHardEX)
                {
                    isEX = true;
                    isHard = true;
                    mode.gameObject.SetActive(true);
                    mode.text = "INSANE";
                }
                else
                {
                    StartCoroutine(messege());
                    return;
                }
                break;
            default:
                break;
        }

        currentMode = PlayerPrefs.GetInt("currGameMod");
        Line.SetActive(true);
        Plane.SetActive(true);
        current_score.gameObject.SetActive(true);
        max_score[4].gameObject.SetActive(true);
        max_score[4].text = dataManager.saveFile.highScores[currentMode - 1].ToString();
        pause.gameObject.SetActive(true);
        start_group[0].SetActive(false);
        is_play = true;
        is_over = false;
        score = 0;
        max_level = 0;
        spawn_level = 0;
        bgmPlayer.Play();
        Invoke("next", 1.5f);
        StartCoroutine(molru());

        Debug.Log(PlayerPrefs.GetInt("currGameMod"));
    }

    IEnumerator messege()
    {
        WarnText.color = new Color(0, 0.22f, 0.44f, 1);
        yield return new WaitForSeconds(0.5f);

        for (float F = 1; F >= 0; F -= 0.032f)
        {
            WarnText.color = new Color(0, 0.22f, 0.44f, F);
            yield return null;
        }
    }

    void LateUpdate()
    {
        current_score.text = score.ToString();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause && is_play)
        {
            Pause();
        }
    }

    public void Touchdown()
    {
        if (lastball == null)
        {
            return;
        }

        lastball.Drag();
    }

    void Update()
    {
        if (Input.GetButtonDown("Cancel"))
        {
            if (is_play)
            {
                Pause();
            }
            else if (start_group[0].gameObject.activeSelf)
            {
                quit_messege();
            }
        }
    }

    public void Pause()
    {
        sfxPlay(sfx.Click);
        pause_group.SetActive(true);
        enable(false);
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    public void quit_messege()
    {
        start_group[0].SetActive(false);
        quit_group.SetActive(true);
    }

    public void Touchup()
    {
        if (lastball == null)
        {
            return;
        }

        lastball.Drop();
        lastball = null;
    }

    public void Dead()
    {
        if (is_over)
        {
            return;
        }

        is_over = true;
        StartCoroutine(Dead_R());
    }

    IEnumerator Dead_R()
    {
        Balls[] balls = FindObjectsOfType<Balls>();

        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].rgbd.simulated = false;
        }
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Hide(Vector3.up * 1000);
            sfxPlay(sfx.LevelUp);
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(1);


        if (score > dataManager.saveFile.highScores[currentMode - 1])
        {
            dataManager.saveFile.highScores[currentMode - 1] = score;
            new_best.gameObject.SetActive(true);
        }


        is_play = false;

        current_score.gameObject.SetActive(false);
        max_score[4].gameObject.SetActive(false);
        Plane.SetActive(false);
        Line.SetActive(false);
        mode.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
        end_group.SetActive(true);

        subtext.text = current_score.text;
        max_score[5].text = dataManager.saveFile.highScores[currentMode - 1].ToString();

        bgmPlayer.Stop();
        dataManager.Save(dataManager.saveFile);
        sfxPlay(sfx.End);
    }

    public void sfxPlay(sfx type)
    {
        switch (type)
        {
            case sfx.LevelUp:
                sfxPlayer[sfxCursor].clip = sfxClip[UnityEngine.Random.Range(0, 3)];
                break;
            case sfx.Next:
                sfxPlayer[sfxCursor].clip = sfxClip[3];
                break;
            case sfx.Attach:
                sfxPlayer[sfxCursor].clip = sfxClip[4];
                break;
            case sfx.Tada:
                sfxPlayer[sfxCursor].clip = sfxClip[5];
                break;
            case sfx.Click:
                sfxPlayer[sfxCursor].clip = sfxClip[6];
                break;
            case sfx.End:
                sfxPlayer[sfxCursor].clip = sfxClip[7];
                break;
        }

        sfxPlayer[sfxCursor].Play();
        sfxCursor = (sfxCursor + 1) % sfxPlayer.Length;
    }

    public void Option()
    {
        sfxPlay(sfx.Click);
        enable(true);
        option_group.SetActive(true);
        start_group[0].SetActive(false);
    }

    public void OpToMain()
    {
        sfxPlay(sfx.Click);
        option_group.SetActive(false);
        start_group[0].SetActive(true);
    }

    public void reset_option()
    {
        sfxPlay(sfx.Click);
        option_group.SetActive(false);
        warning_group.SetActive(true);
    }

    public void No()
    {
        sfxPlay(sfx.Click);
        warning_group.SetActive(false);
        option_group.SetActive(true);
    }

    public void Quit()
    {
        sfxPlay(sfx.Click);
        Application.Quit();
    }

    public void Reset_data()
    {
        sfxPlay(sfx.Click);
        dataManager.InitializeData(dataManager.path, true);
        reset();
    }

    public void Stop()
    {
        sfxPlay(sfx.Click);
        pause_group.gameObject.SetActive(false);
        Time.timeScale = 1.25f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        Dead();
    }

    public void changeMode()
    {
        sfxPlay(sfx.Click);

        if (start_group[1].activeSelf)
        {
            changeButton.image.sprite = hardImage;

            start_group[1].SetActive(false);
            start_group[2].SetActive(true);

            max_score[2].text = dataManager.saveFile.highScores[2].ToString();
            max_score[3].text = dataManager.saveFile.highScores[3].ToString();
        }
        else
        {
            changeButton.image.sprite = normalImage;

            start_group[2].SetActive(false);
            start_group[1].SetActive(true);

            max_score[0].text = dataManager.saveFile.highScores[0].ToString();
            max_score[1].text = dataManager.saveFile.highScores[1].ToString();
        }
    }

    public void reset()
    {
        Time.timeScale = 1.25f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        SceneManager.LoadScene("Game");
    }

    IEnumerator molru()
    {
        while (true)
        {
            if (!isEX && max_level > 9)
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

                yield return new WaitForSeconds(0.5f);
                sfxPlay(sfx.Tada);
                eff_group.SetActive(true);
                yield return new WaitForSeconds(3);
                eff_group.SetActive(false);
                yield break;
            }
            if (isEX && max_level > 11)
            {
                yield return new WaitForSeconds(0.5f);
                sfxPlay(sfx.Tada);
                eff_group.SetActive(true);
                yield return new WaitForSeconds(3);
                eff_group.SetActive(false);
                yield break;
            }
            yield return null;
        }
    }

    public void SoundOn_BGM()
    {
        PlayerPrefs.SetInt("IsMuted_bgm", 0);
        if (!is_play)
        {
            PlayerPrefs.SetFloat("volumeBGM", Bgm_slider.value);
        }
        if (is_play)
        {
            PlayerPrefs.SetFloat("volumeBGM", pause_Bgm_slider.value);
        }
        sfxPlay(sfx.Click);
    }

    public void SoundOff_BGM()
    {
        PlayerPrefs.SetInt("IsMuted_bgm", 1);
        sfxPlay(sfx.Click);
    }

    public void SoundOn_SFX()
    {
        PlayerPrefs.SetInt("IsMuted_sfx", 0);
        if (!is_play)
        {
            PlayerPrefs.SetFloat("volumeSFX", Sfx_slider.value);
        }
        if (is_play)
        {
            PlayerPrefs.SetFloat("volumeSFX", pause_Sfx_slider.value);
        }
        sfxPlay(sfx.Click);
    }

    public void SoundOff_SFX()
    {
        PlayerPrefs.SetInt("IsMuted_sfx", 1);
    }
    public void SoundOn_ATTACH()
    {
        PlayerPrefs.SetInt("IsMuted_attach", 0);
        sfxPlay(sfx.Click);
    }
    public void SoundOff_ATTACH()
    {
        PlayerPrefs.SetInt("IsMuted_attach", 1);
        sfxPlay(sfx.Click);
    }
    IEnumerator EnableIcon_bgm()
    {
        while (true)
        {
            yield return new WaitUntil(() => PlayerPrefs.GetInt("IsMuted_bgm") == 1);
            bgmPlayer.mute = true;
            BgmMute_on.gameObject.SetActive(false);
            BgmMute_off.gameObject.SetActive(true);
            pause_BgmMute_on.gameObject.SetActive(false);
            pause_BgmMute_off.gameObject.SetActive(true);

            yield return new WaitWhile(() => PlayerPrefs.GetInt("IsMuted_bgm") == 1);
            bgmPlayer.mute = false;
            BgmMute_on.gameObject.SetActive(!false);
            BgmMute_off.gameObject.SetActive(!true);
            pause_BgmMute_on.gameObject.SetActive(!false);
            pause_BgmMute_off.gameObject.SetActive(!true);
        }
    }

    IEnumerator EnableIcon_sfx()
    {
        while (true)
        {
            yield return new WaitUntil(() => PlayerPrefs.GetInt("IsMuted_sfx") == 1);
            SfxMute_on.gameObject.SetActive(false);
            SfxMute_off.gameObject.SetActive(true);
            pause_SfxMute_on.gameObject.SetActive(false);
            pause_SfxMute_off.gameObject.SetActive(true);
            for (int i = 0; i < 6; i++)
            {
                sfxPlayer[i].mute = true;
            }

            yield return new WaitWhile(() => PlayerPrefs.GetInt("IsMuted_sfx") == 1);
            SfxMute_on.gameObject.SetActive(!false);
            SfxMute_off.gameObject.SetActive(!true);
            pause_SfxMute_on.gameObject.SetActive(!false);
            pause_SfxMute_off.gameObject.SetActive(!true);
            for (int i = 0; i < 6; i++)
            {
                sfxPlayer[i].mute = false;
            }
        }
    }

    IEnumerator EnableIcon_attach()
    {
        while (true)
        {
            yield return new WaitUntil(() => PlayerPrefs.GetInt("IsMuted_attach") == 1);
            play_attach_sound = false;
            AttachMute_on.gameObject.SetActive(false);
            AttachMute_off.gameObject.SetActive(true);
            pause_AttachMute_on.gameObject.SetActive(false);
            pause_AttachMute_off.gameObject.SetActive(true);

            yield return new WaitWhile(() => PlayerPrefs.GetInt("IsMuted_attach") == 1);
            play_attach_sound = true;
            AttachMute_on.gameObject.SetActive(!false);
            AttachMute_off.gameObject.SetActive(!true);
            pause_AttachMute_on.gameObject.SetActive(!false);
            pause_AttachMute_off.gameObject.SetActive(!true);
        }
    }

    public void bgm_value()
    {
        if (!is_play)
        {
            PlayerPrefs.SetFloat("Volume_BGM", Bgm_slider.value);
        }
        if (is_play)
        {
            PlayerPrefs.SetFloat("Volume_BGM", pause_Bgm_slider.value);
        }
        bgmPlayer.volume = PlayerPrefs.GetFloat("Volume_BGM");
    }

    public void sfx_value()
    {
        if (!is_play)
        {
            PlayerPrefs.SetFloat("Volume_SFX", Sfx_slider.value);
        }
        if (is_play)
        {
            PlayerPrefs.SetFloat("Volume_SFX", pause_Sfx_slider.value);
        }

        for (int i = 0; i < 6; i++)
        {
            sfxPlayer[i].volume = PlayerPrefs.GetFloat("Volume_SFX");
        }
    }

    void enable(bool is_main)
    {
        if (is_main)
        {
            Bgm_slider.value = PlayerPrefs.GetFloat("Volume_BGM");
            Sfx_slider.value = PlayerPrefs.GetFloat("Volume_SFX");
        }
        if (!is_main)
        {
            pause_Bgm_slider.value = PlayerPrefs.GetFloat("Volume_BGM");
            pause_Sfx_slider.value = PlayerPrefs.GetFloat("Volume_SFX");
        }
    }

    public void Return()
    {
        sfxPlay(sfx.Click);
        pause_group.SetActive(false);
        Time.timeScale = 1.25f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    public void Restart()
    {
        end_group.gameObject.SetActive(false);
        GameStart();
    }
}