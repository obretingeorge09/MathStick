using UnityEngine;
using System.Collections;

public class TimerManager : MonoBehaviour
{
    public float StartTime = 60f;
    public float MinTime = 25f;

    float currentTimer = 0f;
    float currentMaxTime = 0f;
    bool gameAlreadyWon = false;

    float GetStartTimeForMode()
    {
        if (GameManager.Instance == null) return StartTime;

        // Arcade mode: fixed timer per mode (no progressive reduction)
        if (GameManager.Instance.isArcadeMode)
        {
            switch (GameManager.Instance.currentMode)
            {
                case GameMode.Easy:   return 30f;
                case GameMode.Medium: return 45f;
                case GameMode.Hard:   return 60f;
                default:              return 30f;
            }
        }

        switch (GameManager.Instance.currentMode)
        {
            case GameMode.Easy:   return 30f;
            case GameMode.Medium: return 60f;
            case GameMode.Hard:   return 90f;
            default:              return StartTime;
        }
    }

    void Awake()
    {
        currentMaxTime = StartTime;

        Messenger.AddListener<string>(Message.OnEndFadeToTransparent, OnFadeToTransparentEnd);
        Messenger.AddListener<string>(Message.OnEndFadeToOpaque, OnFadeToOpaqueEnd);
        Messenger.AddListener(Message.StartNewGame, () =>
        {
            // Reset timer to mode's start time each new game session
            float modeStart = GetStartTimeForMode();
            currentMaxTime = modeStart;
        });
        Messenger.AddListener(Message.ArcadeRoundWon, () =>
        {
            StopCoroutine("TimerCo");
            gameAlreadyWon = true;
            // No timer reduction in arcade mode
        });
        Messenger.AddListener(Message.GameWon, () =>
        {
            StopCoroutine("TimerCo");
            gameAlreadyWon = true;
            if (GameManager.Instance != null && GameManager.Instance.isArcadeMode) return;
            float modeMin = GetStartTimeForMode() * 0.5f; // min = half of mode start
            if (modeMin < MinTime) modeMin = MinTime;
            float reduction = currentMaxTime > modeMin + 5f ? 5f : 1f;
            currentMaxTime = Mathf.Max(currentMaxTime - reduction, modeMin);
        });
        Messenger.AddListener(Message.GameLost, () =>
        {
            gameAlreadyWon = true;
            if (GameManager.Instance == null || !GameManager.Instance.isArcadeMode)
                currentMaxTime = GetStartTimeForMode();
        });
    }

    void OnFadeToTransparentEnd(string fadeToTransparentName)
    {
        if (fadeToTransparentName == "fadeToTransparentBeforeGameStarts")
        {
            StartCoroutine("TimerCo");
        }
    }

    void OnFadeToOpaqueEnd(string fadeToOpaqueName)
    {
        if (fadeToOpaqueName == "fadeToOpaqueBeforeGameStarts")
        {
            currentTimer = currentMaxTime;
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
            Messenger.Broadcast<float>(Message.OnSetTimerMax, currentMaxTime);
        }
    }

    /// <summary>
    /// Start the timer for arcade mode rounds (bypasses fade system).
    /// </summary>
    public void StartArcadeTimer()
    {
        StopCoroutine("TimerCo");
        currentMaxTime = GetStartTimeForMode();
        currentTimer = currentMaxTime;
        Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
        Messenger.Broadcast<float>(Message.OnSetTimerMax, currentMaxTime);
        StartCoroutine("TimerCo");
    }

    public void StopArcadeTimer()
    {
        StopCoroutine("TimerCo");
    }

    IEnumerator TimerCo()
    {
        currentTimer = currentMaxTime;
        gameAlreadyWon = false;
        Messenger.Broadcast<float>(Message.OnSetTimerMax, currentMaxTime);

        while (currentTimer > 0)
        {
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
            currentTimer -= Time.deltaTime;
            yield return null;
        }

        if (!gameAlreadyWon)
        {
            currentTimer = 0f;
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);

            if (GameManager.Instance != null && GameManager.Instance.isArcadeMode)
                ArcadeMatchManager.Instance?.OnLocalPlayerTimeout();
            else
                Messenger.Broadcast(Message.GameLost);
        }
    }
}
