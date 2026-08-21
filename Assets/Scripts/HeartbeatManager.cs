using UnityEngine;

public class HeartbeatManager : MonoBehaviour
{
    AudioSource heartbeatSource;
    AudioClip heartbeatClip;
    float currentTimer = 60f;
    float maxTimer = 60f;
    bool isActive = false;
    float nextBeatTime = 0f;

    void Awake()
    {
        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.spatialBlend = 0f;  // 2D sound
        heartbeatClip = CreateHeartbeatClip();

        Messenger.AddListener<float>(Message.OnSetTimer,    t => currentTimer = t);
        Messenger.AddListener<float>(Message.OnSetTimerMax, m => maxTimer = m);
        Messenger.AddListener<string>(Message.OnEndFadeToTransparent, OnFadeTransparent);
        Messenger.AddListener(Message.GameWon,  Stop);
        Messenger.AddListener(Message.GameLost, Stop);
    }

    void OnFadeTransparent(string name)
    {
        // Arcade rounds run a stopwatch (max = 0): there is no countdown to
        // dramatise, and a zero max reads as ratio 0 = permanent 180 BPM panic.
        if (GameManager.Instance != null && GameManager.Instance.isArcadeMode)
            return;

        if (name == "fadeToTransparentBeforeGameStarts")
            StartBeating();
    }

    void StartBeating() { isActive = true; nextBeatTime = Time.time; }
    void Stop() { isActive = false; }

    void Update()
    {
        if (!isActive) return;
        if (AudioManager.Instance != null && AudioManager.Instance.IsMuted) return;
        if (Time.time < nextBeatTime) return;

        // No countdown (the arcade stopwatch broadcasts max = 0) → no beat.
        // Without this, ratio read 0 = max panic at full volume, forever —
        // e.g. after leaving a training round mid-game and entering arcade.
        if (maxTimer <= 0f) return;

        float ratio = Mathf.Clamp01(currentTimer / maxTimer);
        float bpm = 40f + Mathf.Pow(1f - ratio, 0.8f) * 140f;  // 40 BPM idle → 180 BPM panic
        float interval = 60f / bpm;
        float volume = Mathf.Lerp(0.50f, 0.85f, 1f - ratio);   // loud from start → very loud

        heartbeatSource.PlayOneShot(heartbeatClip, volume);
        nextBeatTime = Time.time + interval;
    }

    AudioClip CreateHeartbeatClip()
    {
        const int sampleRate = 44100;
        const float clipDuration = 0.5f;
        int total = (int)(sampleRate * clipDuration);
        float[] data = new float[total];

        // Lub: low thump at t=0.00s, freq=55Hz, dur=0.12s
        AddThump(data, sampleRate, 0.00f, 55f, 0.12f, 1.0f);
        // Dub: slightly higher, softer thump at t=0.15s, freq=70Hz, dur=0.08s
        AddThump(data, sampleRate, 0.15f, 70f, 0.08f, 0.55f);

        var clip = AudioClip.Create("heartbeat", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void AddThump(float[] data, int sampleRate, float startSec, float freq, float duration, float amplitude)
    {
        int start = (int)(startSec * sampleRate);
        int len   = (int)(duration * sampleRate);
        for (int i = 0; i < len && start + i < data.Length; i++)
        {
            float t   = (float)i / sampleRate;
            float env = Mathf.Exp(-t / (duration * 0.35f));  // fast decay
            float s   = Mathf.Sin(2f * Mathf.PI * freq * t) * env
                      + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * env;  // 2nd harmonic
            data[start + i] += s * amplitude;
        }
    }
}
