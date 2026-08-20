using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// The single seam the game uses to ask for a rewarded ad.
///
/// No ad SDK is installed yet, so this ships a STUB that fakes the wait. The stub
/// exists so the whole coin flow can be built and played today; it is deliberately
/// impossible to ship by accident:
///
///   * without the MATHSTICK_ADS_ADMOB compile symbol it screams in the console
///     every single time it runs, and
///   * in a non-development build it reports NO ad available at all, so a shipped
///     APK without the real SDK hands out nothing.
///
/// To go live, install the Google Mobile Ads Unity plugin, define
/// MATHSTICK_ADS_ADMOB in Player Settings, and fill in the real implementation
/// where LoadReal/ShowReal are marked below.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    /// <summary>Raised when readiness or the remaining daily count changes.</summary>
    public event Action OnAvailabilityChanged;

    [Header("Stub UI (used only until a real SDK is wired in)")]
    public GameObject stubPanel;
    public Text       stubCountdown;

    [Header("Limits")]
    [Tooltip("How many rewarded ads a player may watch per day.")]
    public int dailyCap = 5;

    [Tooltip("Seconds the stub pretends an ad runs for.")]
    public float stubDuration = 4f;

    bool showing;
    int  watchedToday;
    string watchedDate = "";

    public bool IsShowing => showing;

    public int RewardsRemainingToday
    {
        get { SyncDay(); return Mathf.Max(0, dailyCap - watchedToday); }
    }

    /// <summary>
    /// True only when an ad can actually be shown right now. Callers must always
    /// offer another way forward when this is false — the player can never be
    /// left with an ad as their only exit.
    /// </summary>
    public bool IsRewardedReady
    {
        get
        {
            if (showing) return false;
            if (RewardsRemainingToday <= 0) return false;

#if MATHSTICK_ADS_ADMOB
            return IsRealAdLoaded();
#else
            // A release build with no SDK must never pay out.
            return Debug.isDebugBuild || Application.isEditor;
#endif
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        watchedToday = PlayerPrefs.GetInt("AdsWatchedToday", 0);
        watchedDate  = PlayerPrefs.GetString("AdsWatchedDate", "");
    }

    void Start()
    {
        if (stubPanel != null) stubPanel.SetActive(false);

#if MATHSTICK_ADS_ADMOB
        InitialiseReal();
#endif
        OnAvailabilityChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Daily cap
    // ═══════════════════════════════════════════════════════════════════

    static string Today => FirebaseDBManager.Instance != null
        ? FirebaseDBManager.Instance.ServerDateKey
        : DateTime.UtcNow.ToString("yyyy-MM-dd");

    /// <summary>Roll the counter over when the server date changes.</summary>
    void SyncDay()
    {
        string today = Today;
        if (watchedDate == today) return;

        watchedDate = today;
        watchedToday = 0;

        PlayerPrefs.SetString("AdsWatchedDate", watchedDate);
        PlayerPrefs.SetInt("AdsWatchedToday", 0);
        PlayerPrefs.Save();
    }

    void CountWatch()
    {
        SyncDay();
        watchedToday++;

        PlayerPrefs.SetInt("AdsWatchedToday", watchedToday);
        PlayerPrefs.SetString("AdsWatchedDate", watchedDate);
        PlayerPrefs.Save();

        OnAvailabilityChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Showing
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a rewarded ad. onFinished(true) ONLY when it was watched to the end —
    /// a dismissal, a load failure or no fill all report false, and the caller
    /// must not pay out on false.
    /// </summary>
    public void ShowRewarded(Action<bool> onFinished)
    {
        if (showing) { onFinished?.Invoke(false); return; }

        if (!IsRewardedReady)
        {
            Debug.LogWarning("AdManager: no rewarded ad available.");
            onFinished?.Invoke(false);
            return;
        }

#if MATHSTICK_ADS_ADMOB
        showing = true;
        ShowReal(granted =>
        {
            showing = false;
            if (granted) CountWatch();
            onFinished?.Invoke(granted);
        });
#else
        showing = true;
        StartCoroutine(ShowStub(onFinished));
#endif
    }

    IEnumerator ShowStub(Action<bool> onFinished)
    {
        Debug.LogWarning("═══ AdManager STUB: no ad SDK installed. " +
                         "This grants the reward WITHOUT showing a real ad. " +
                         "Install Google Mobile Ads and define MATHSTICK_ADS_ADMOB before release. ═══");

        if (stubPanel != null) stubPanel.SetActive(true);

        float t = stubDuration;
        while (t > 0f)
        {
            if (stubCountdown != null)
                stubCountdown.text = "SIMULATED AD  ·  " + Mathf.CeilToInt(t) + "s";

            // Unscaled: the popover must work even if the game is paused
            t -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (stubPanel != null) stubPanel.SetActive(false);

        showing = false;
        CountWatch();
        onFinished?.Invoke(true);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Real SDK — filled in once the plugin is installed
    // ═══════════════════════════════════════════════════════════════════

#if MATHSTICK_ADS_ADMOB
    // Replace these three with the Google Mobile Ads plugin calls. Keep the
    // contract: ShowReal must invoke its callback with TRUE only on a completed
    // view, and the game must never grant coins on false.
    void InitialiseReal()
    {
        Debug.LogError("AdManager: MATHSTICK_ADS_ADMOB is defined but the real " +
                       "implementation is still empty. Wire up MobileAds.Initialize " +
                       "and RewardedAd.Load here.");
    }

    bool IsRealAdLoaded() => false;

    void ShowReal(Action<bool> onGranted) => onGranted?.Invoke(false);
#endif
}
