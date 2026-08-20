using UnityEngine;
using System;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource sfxSource;

    /// <summary>Raised whenever a level changes, so icons and sliders can redraw.</summary>
    public event Action OnAudioChanged;

    public const int STEPS = 5;

    // Perceptual ladder rather than even 0.2 increments: linear steps sound
    // like four loud settings and one silent one, because loudness is not
    // proportional to amplitude.
    static readonly float[] GAIN = { 0f, 0.06f, 0.20f, 0.40f, 0.66f, 1.00f };

    // Master is the situational control, so it is stepped and the STEP is the
    // stored truth. Music and SFX are a set-once balance, so they stay smooth.
    int   masterStep = STEPS;
    float music = 0.7f;
    float sfx   = 1f;
    bool  muted = false;

    public int   MasterStep  => masterStep;
    public float Master      => GAIN[Mathf.Clamp(masterStep, 0, STEPS)];
    public float MusicVolume => music;
    public float SFXVolume   => sfx;
    public bool  IsMuted     => muted || masterStep == 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        music = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfx   = PlayerPrefs.GetFloat("SFXVolume", 1f);
        muted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

        if (PlayerPrefs.HasKey("MasterStep"))
        {
            masterStep = Mathf.Clamp(PlayerPrefs.GetInt("MasterStep"), 0, STEPS);
        }
        else if (PlayerPrefs.HasKey("MasterVolume"))
        {
            // One-time migration from the old continuous master
            masterStep = NearestStep(PlayerPrefs.GetFloat("MasterVolume"));
        }
    }

    void Start()
    {
        Apply();

        // UI built in the same frame subscribes after Awake, so without this
        // the first draw would show stale defaults.
        OnAudioChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Levels
    // ═══════════════════════════════════════════════════════════════════

    static int NearestStep(float gain)
    {
        int best = 0;
        for (int i = 1; i <= STEPS; i++)
            if (Mathf.Abs(GAIN[i] - gain) < Mathf.Abs(GAIN[best] - gain)) best = i;
        return best;
    }

    public void SetMasterStep(int step)
    {
        masterStep = Mathf.Clamp(step, 0, STEPS);

        // Stepping up off silence is the player asking to hear again
        if (masterStep > 0) muted = false;

        Persist();
    }

    public void StepMaster(int delta) => SetMasterStep(masterStep + delta);

    /// <summary>Float entry point kept for older call sites; snaps to the ladder.</summary>
    public void SetMaster(float value) => SetMasterStep(NearestStep(Mathf.Clamp01(value)));

    public void SetMusicVolume(float value)
    {
        music = Mathf.Clamp01(value);
        Persist();
    }

    public void SetSFXVolume(float value)
    {
        sfx = Mathf.Clamp01(value);
        Persist();
    }

    public void ToggleMute()
    {
        muted = !muted;

        // Un-muting at step 0 would still be silent, so give it something audible
        if (!muted && masterStep == 0) masterStep = 3;

        Persist();
    }

    void Persist()
    {
        Apply();

        PlayerPrefs.SetInt("MasterStep", masterStep);
        PlayerPrefs.SetFloat("MusicVolume", music);
        PlayerPrefs.SetFloat("SFXVolume", sfx);
        PlayerPrefs.SetInt("IsMuted", muted ? 1 : 0);
        PlayerPrefs.Save();

        OnAudioChanged?.Invoke();
    }

    void Apply()
    {
        float g = IsMuted ? 0f : Master;
        if (musicSource) musicSource.volume = music * g;
        if (sfxSource)   sfxSource.volume   = sfx * g;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Playback
    // ═══════════════════════════════════════════════════════════════════

    public void PlaySFX(AudioClip clip)
    {
        if (IsMuted || sfxSource == null || clip == null) return;

        // volumeScale multiplies AudioSource.volume, which Apply() already set
        // to sfx * master. Passing the gain again squared it — at master 0.4
        // effects played at 0.16 of the level the player asked for.
        sfxSource.PlayOneShot(clip, 1f);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = IsMuted ? 0f : music * Master;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }
}
