using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// The corner audio key and the stepped popover it opens.
///
/// The old control could only mute, so the player could turn the game off but
/// never down. This one drives the master level through six discrete states and
/// reports the current one on the key itself, as a column of lit cells — the
/// same visual language as the 7-segment digits the game is made of.
///
/// Discrete steps rather than a slider: the whole game is tapping, a drag in the
/// top corner of a tall phone is a bad one-handed reach, and nobody needs 37%.
/// </summary>
public class VolumeControl : MonoBehaviour
{
    public static VolumeControl Instance { get; private set; }

    [Header("Key")]
    public Image   glyphCone;
    public Image   glyphSlash;
    public Image[] keyCells    = new Image[5];   // index 0 = level 1 (bottom)
    public Image[] keyOutlines = new Image[5];
    public CanvasGroup keyGroup;

    [Header("Popover")]
    public GameObject popover;
    public GameObject scrim;
    public CanvasGroup popGroup;
    public Image[] pips        = new Image[5];   // index 0 = level 5 (top)
    public Image[] pipOutlines = new Image[5];
    public Button  btnPlus;
    public Button  btnMinus;

    [Header("Context")]
    public GameObject gameplayPanel;             // pnl_main — key dims over play

    [Header("Look")]
    public Color lit   = new Color(0.463f, 1f, 0.012f);
    public Color unlit = new Color(0.102f, 0.180f, 0.063f);
    public Color dim   = new Color(0.200f, 0.412f, 0.118f);

    [Header("Timing, seconds")]
    public float autoHide  = 2.5f;
    public float openTime  = 0.14f;
    public float closeTime = 0.10f;

    Coroutine hideCo, animCo;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void OnEnable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged += Redraw;

        // Draw directly rather than waiting for an event that may already have fired
        Redraw();
    }

    void OnDisable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged -= Redraw;
    }

    void Start()
    {
        if (popover != null) popover.SetActive(false);
        if (scrim != null) scrim.SetActive(false);
        Redraw();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Open / close
    // ═══════════════════════════════════════════════════════════════════

    public void Toggle()
    {
        if (popover != null && popover.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        // Without a popover the key degrades to a plain mute toggle
        if (popover == null)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.ToggleMute();
            Redraw();
            return;
        }

        popover.SetActive(true);
        if (scrim != null) scrim.SetActive(true);
        Redraw();

        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(OpenAnim());

        RestartAutoHide();
    }

    public void Close()
    {
        if (popover == null || !popover.activeSelf) return;

        if (hideCo != null) { StopCoroutine(hideCo); hideCo = null; }
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(CloseAnim());
    }

    IEnumerator OpenAnim()
    {
        var rt = popover.GetComponent<RectTransform>();

        for (float t = 0; t < openTime; t += Time.unscaledDeltaTime)
        {
            float n = t / openTime;
            if (popGroup != null) popGroup.alpha = n;

            // Small overshoot so it springs out of the key rather than inflating
            float sc = n < 0.75f
                ? Mathf.Lerp(0.88f, 1.06f, n / 0.75f)
                : Mathf.Lerp(1.06f, 1f, (n - 0.75f) / 0.25f);

            if (rt != null) rt.localScale = Vector3.one * sc;
            yield return null;
        }

        if (popGroup != null) popGroup.alpha = 1f;
        if (rt != null) rt.localScale = Vector3.one;
        animCo = null;
    }

    IEnumerator CloseAnim()
    {
        var rt = popover.GetComponent<RectTransform>();

        for (float t = 0; t < closeTime; t += Time.unscaledDeltaTime)
        {
            float n = t / closeTime;
            if (popGroup != null) popGroup.alpha = 1f - n;
            if (rt != null) rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, n);
            yield return null;
        }

        if (rt != null) rt.localScale = Vector3.one;
        popover.SetActive(false);
        if (scrim != null) scrim.SetActive(false);
        animCo = null;
    }

    void RestartAutoHide()
    {
        if (hideCo != null) StopCoroutine(hideCo);
        hideCo = StartCoroutine(AutoHide());
    }

    IEnumerator AutoHide()
    {
        float t = 0f;
        while (t < autoHide) { t += Time.unscaledDeltaTime; yield return null; }

        hideCo = null;
        Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Stepping
    // ═══════════════════════════════════════════════════════════════════

    public void StepUp()   { Step(+1); }
    public void StepDown() { Step(-1); }

    void Step(int delta)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.StepMaster(delta);
        RestartAutoHide();
        Redraw();

        // The player has to HEAR the level they just set. At step 0 PlaySFX
        // early-returns, and that silence is itself the right feedback.
        am.PlaySFX(TickClip());
    }

    static AudioClip s_tick;

    static AudioClip TickClip()
    {
        if (s_tick != null) return s_tick;

        const int rate = 44100;
        const float dur = 0.06f;
        int n = (int)(rate * dur);

        s_tick = AudioClip.Create("volTick", n, 1, rate, false);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1100f * t) * (1f - t / dur) * 0.5f;
        }
        s_tick.SetData(data, 0);
        return s_tick;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Drawing
    // ═══════════════════════════════════════════════════════════════════

    public void Redraw()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Gate on IsMuted, not MasterStep: a ToggleMute from anywhere else
        // leaves the step at 5 while the game is silent, and the icon would lie.
        int step = am.IsMuted ? 0 : am.MasterStep;
        bool off = step == 0;

        if (glyphCone != null)  glyphCone.color = off ? dim : lit;
        if (glyphSlash != null) glyphSlash.gameObject.SetActive(off);

        // Key column runs bottom-up: cell i is level i+1
        Paint(keyCells, keyOutlines, step, true);

        // Popover ladder runs top-down: pip i is level STEPS - i
        Paint(pips, pipOutlines, step, false);

        if (btnPlus  != null) btnPlus.interactable  = step < AudioManager.STEPS;
        if (btnMinus != null) btnMinus.interactable = step > 0;

        // Quiet game: the chrome steps back while you are actually playing
        if (keyGroup != null)
            keyGroup.alpha = gameplayPanel != null && gameplayPanel.activeSelf ? 0.7f : 1f;
    }

    void Paint(Image[] cells, Image[] outlines, int step, bool bottomUp)
    {
        if (cells == null) return;

        for (int i = 0; i < cells.Length; i++)
        {
            int level = bottomUp ? i + 1 : AudioManager.STEPS - i;

            if (cells[i] != null)
                cells[i].color = level <= step ? lit : unlit;

            // Outlines appear only at zero, so an all-dark ladder still reads
            // as "switched off" rather than "broken".
            if (outlines != null && i < outlines.Length && outlines[i] != null)
                outlines[i].gameObject.SetActive(step == 0);
        }
    }
}
