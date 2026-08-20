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

        // Training is where the player LEARNS the puzzle, so it gets real room.
        // The old 30/60/90 was tuned before Medium grew to three digits and Hard
        // to three numbers with two operators, and it had not been revisited.
        switch (GameManager.Instance.currentMode)
        {
            case GameMode.Easy:   return 50f;
            case GameMode.Medium: return 100f;
            case GameMode.Hard:   return 160f;
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
            StopCoroutine("StopwatchCo");
            gameAlreadyWon = true;
            // No timer reduction in arcade mode
        });
        Messenger.AddListener(Message.GameWon, () =>
        {
            StopCoroutine("TimerCo");
            gameAlreadyWon = true;
            if (GameManager.Instance != null && GameManager.Instance.isArcadeMode) return;

            // The streak tightens the clock, but gently: losing 5s per win meant
            // a good run punished itself into the floor within ten rounds.
            // 3s per win against a 65% floor keeps a long streak playable.
            float modeMin = GetStartTimeForMode() * 0.65f;
            if (modeMin < MinTime) modeMin = MinTime;
            float reduction = currentMaxTime > modeMin + 3f ? 3f : 1f;
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
    /// Arcade rounds run a stopwatch, not a countdown. A max of 0 tells the
    /// HUD there is no limit, so it drops the progress bar and stops tinting
    /// the readout red as if the player were running out.
    /// </summary>
    public void StartArcadeTimer()
    {
        StopCoroutine("TimerCo");
        StopCoroutine("StopwatchCo");

        currentTimer = 0f;
        currentMaxTime = 0f;
        Messenger.Broadcast<float>(Message.OnSetTimerMax, 0f);
        Messenger.Broadcast<float>(Message.OnSetTimer, 0f);
        StartCoroutine("StopwatchCo");
    }

    public void StopArcadeTimer()
    {
        StopCoroutine("TimerCo");
        StopCoroutine("StopwatchCo");
    }

    IEnumerator StopwatchCo()
    {
        currentTimer = 0f;
        gameAlreadyWon = false;

        while (true)
        {
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
            currentTimer += Time.deltaTime;
            yield return null;
        }
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
            {
                // A bot match owns the round when one is running; otherwise it's a real 1v1
                if (BotMatchManager.Instance != null && BotMatchManager.Instance.IsInMatch)
                    BotMatchManager.Instance.OnLocalPlayerTimeout();
                else
                    ArcadeMatchManager.Instance?.OnLocalPlayerTimeout();
            }
            else
                Messenger.Broadcast(Message.GameLost);
        }
    }
}
