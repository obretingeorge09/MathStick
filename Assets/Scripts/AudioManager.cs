using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource sfxSource;

    public float MusicVolume { get; set; } = 1f;
    public float SFXVolume { get; set; } = 1f;
    public bool IsMuted { get; set; } = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Load saved mute state
        if (PlayerPrefs.HasKey("IsMuted"))
            IsMuted = PlayerPrefs.GetInt("IsMuted") == 1;

        if (PlayerPrefs.HasKey("MusicVolume"))
            MusicVolume = PlayerPrefs.GetFloat("MusicVolume");

        if (PlayerPrefs.HasKey("SFXVolume"))
            SFXVolume = PlayerPrefs.GetFloat("SFXVolume");

        UpdateVolumes();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (IsMuted || sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, SFXVolume);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = IsMuted ? 0 : MusicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        UpdateVolumes();
        PlayerPrefs.SetInt("IsMuted", IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    void UpdateVolumes()
    {
        if (musicSource) musicSource.volume = IsMuted ? 0 : MusicVolume;
        if (sfxSource) sfxSource.volume = IsMuted ? 0 : SFXVolume;
    }
}
