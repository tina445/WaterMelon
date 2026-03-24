using Fusion;
using UnityEngine;

public class AudioService : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource bgmPlayer;
    public AudioClip bgmClip;

    public AudioSource[] sfxPlayer;
    public AudioClip[] sfxClip;

    private const string KEY_MUTE_BGM = "IsMuted_bgm";
    private const string KEY_MUTE_SFX = "IsMuted_sfx";
    private const string KEY_VOL_BGM  = "Volume_BGM";
    private const string KEY_VOL_SFX  = "Volume_SFX";
    public enum sfx { LevelUp, Next, Tada, Click, End };
    int sfxCursor;

    private void Awake()
    {
        InitializeFromPrefs();
    }

    // ===== Init / Apply =====
    public void InitializeFromPrefs()
    {
        // 기본값 보장
        if (!PlayerPrefs.HasKey(KEY_MUTE_BGM)) PlayerPrefs.SetInt(KEY_MUTE_BGM, 0);
        if (!PlayerPrefs.HasKey(KEY_MUTE_SFX)) PlayerPrefs.SetInt(KEY_MUTE_SFX, 0);

        if (!PlayerPrefs.HasKey(KEY_VOL_BGM)) PlayerPrefs.SetFloat(KEY_VOL_BGM, 0.3f);
        if (!PlayerPrefs.HasKey(KEY_VOL_SFX)) PlayerPrefs.SetFloat(KEY_VOL_SFX, 1.0f);

        ApplyFromPrefs();
    }

    public void ApplyFromPrefs()
    {
        bool muteBgm = PlayerPrefs.GetInt(KEY_MUTE_BGM, 0) == 1;
        bool muteSfx = PlayerPrefs.GetInt(KEY_MUTE_SFX, 0) == 1;

        float vBgm = PlayerPrefs.GetFloat(KEY_VOL_BGM, 0.3f);
        float vSfx = PlayerPrefs.GetFloat(KEY_VOL_SFX, 1.0f);

        bgmPlayer.mute = muteBgm;
        bgmPlayer.volume = Mathf.Clamp01(vBgm);

        foreach (var source in sfxPlayer)
        {
            source.volume = Mathf.Clamp01(vSfx);
            source.mute = muteSfx;
        }
    }

    // ===== BGM =====
    public void SetBgmClip(AudioClip clip)
    {
        bgmPlayer.clip = clip;
    }

    public void PlayBgm()
    {
        if (bgmPlayer.clip == null) return;
        if (!bgmPlayer.isPlaying) bgmPlayer.Play();
    }

    public void StopBgm()
    {
        if (bgmPlayer.isPlaying) bgmPlayer.Stop();
    }

    // ===== SFX =====
    public void PlayOneShot(sfx type)
    {
        AudioClip clip = null;

        switch (type)
        {
            case sfx.LevelUp:
                clip = sfxClip[Random.Range(0, 3)];
                break;
            case sfx.Next:
                clip = sfxClip[3];
                break;
            case sfx.Tada:
                clip = sfxClip[4];
                break;
            case sfx.Click:
                clip = sfxClip[5];
                break;
            case sfx.End:
                clip = sfxClip[6];
                break;
            default:
                break;
        }

        sfxPlayer[sfxCursor].PlayOneShot(clip);
        sfxCursor = (sfxCursor + 1) % sfxPlayer.Length;

        Debug.Log("효과음 재생됨");
    }

    // ===== Settings (UI에서 호출) =====
    public void SetMuteBgm(bool mute)
    {
        PlayerPrefs.SetInt(KEY_MUTE_BGM, mute ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFromPrefs();
    }

    public void SetMuteSfx(bool mute)
    {
        PlayerPrefs.SetInt(KEY_MUTE_SFX, mute ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFromPrefs();
    }

    public void SetVolumeBgm(float v)
    {
        PlayerPrefs.SetFloat(KEY_VOL_BGM, Mathf.Clamp01(v));
        PlayerPrefs.Save();
        ApplyFromPrefs();
    }

    public void SetVolumeSfx(float v)
    {
        PlayerPrefs.SetFloat(KEY_VOL_SFX, Mathf.Clamp01(v));
        PlayerPrefs.Save();
        ApplyFromPrefs();
    }
}
