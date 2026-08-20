using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// The tap marker used by the tutorial: a solid core dot with expanding rings
/// behind it, the convention mobile games use to say "press here".
///
/// It replaces the old pointing hand, which covered the very segment the player
/// was meant to watch light up. The rings expand outward from the target and
/// fade, so the target itself is never hidden.
///
/// Build it with TapIndicator.Create(...) or wire the fields from a scene builder.
/// </summary>
public class TapIndicator : MonoBehaviour
{
    [Header("Layers (core drawn on top of rings)")]
    public Image core;
    public Image[] rings;

    [Header("Timing, seconds")]
    public float ringDuration = 0.85f;   // one ring's full expand-and-fade
    public float ringStagger  = 0.28f;   // delay between successive rings
    public float pressDuration = 0.13f;  // core dip on contact
    public float fadeDuration  = 0.16f;  // show / hide

    [Header("Geometry, multiples of the core size")]
    public float ringStartScale = 0.55f;
    public float ringEndScale   = 2.6f;
    public float corePressScale = 0.72f;

    [Header("Look")]
    public Color tint = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)] public float coreAlpha = 0.9f;
    [Range(0f, 1f)] public float ringAlpha = 0.55f;

    Coroutine loop;
    RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        // Only clear the layers here. Deactivating the root in Awake would fight
        // ShowAt, which activates the object immediately before starting the
        // coroutine — Awake runs in between and would switch it back off.
        ClearLayers();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Place the indicator over a target and start rippling.</summary>
    public void ShowAt(RectTransform target)
    {
        if (target == null) return;
        if (rt == null) rt = GetComponent<RectTransform>();

        rt.position = target.position;
        gameObject.SetActive(true);

        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(RippleLoop());
    }

    public void Hide()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
        ClearLayers();
        gameObject.SetActive(false);
    }

    /// <summary>Plays the contact beat — call it the moment the tap registers.</summary>
    public IEnumerator PlayPress()
    {
        if (core == null) yield break;

        var t0 = core.rectTransform.localScale;
        float half = pressDuration * 0.5f;

        for (float t = 0; t < half; t += Time.deltaTime)
        {
            float n = t / half;
            core.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, corePressScale, n);
            yield return null;
        }
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            float n = t / half;
            // Slight overshoot on release reads as a real button press
            core.rectTransform.localScale = Vector3.one * Mathf.Lerp(corePressScale, 1.08f, n);
            yield return null;
        }

        core.rectTransform.localScale = Vector3.one;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Animation
    // ═══════════════════════════════════════════════════════════════════

    IEnumerator RippleLoop()
    {
        ClearLayers();

        // Fade the core in rather than popping it
        if (core != null)
        {
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                SetAlpha(core, Mathf.Lerp(0f, coreAlpha, t / fadeDuration));
                yield return null;
            }
            SetAlpha(core, coreAlpha);
        }

        if (rings != null)
            for (int i = 0; i < rings.Length; i++)
                if (rings[i] != null)
                    StartCoroutine(RingWave(rings[i], i * ringStagger));

        yield return null;
    }

    IEnumerator RingWave(Image ring, float delay)
    {
        yield return new WaitForSeconds(delay);

        var rrt = ring.rectTransform;

        while (true)
        {
            for (float t = 0; t < ringDuration; t += Time.deltaTime)
            {
                float n = t / ringDuration;

                // Fast out, slow in: the wave leaps away then coasts, which is
                // what makes it read as energy leaving the contact point.
                float e = 1f - Mathf.Pow(1f - n, 3f);

                rrt.localScale = Vector3.one * Mathf.Lerp(ringStartScale, ringEndScale, e);
                SetAlpha(ring, Mathf.Lerp(ringAlpha, 0f, n * n));
                yield return null;
            }
        }
    }

    /// <summary>Reset every layer to fully transparent without touching the root.</summary>
    void ClearLayers()
    {
        if (core != null)
        {
            core.gameObject.SetActive(true);
            core.rectTransform.localScale = Vector3.one;
            SetAlpha(core, 0f);
        }

        if (rings != null)
            foreach (var r in rings)
                if (r != null)
                {
                    r.gameObject.SetActive(true);
                    r.rectTransform.localScale = Vector3.one * ringStartScale;
                    SetAlpha(r, 0f);
                }
    }

    void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        img.color = new Color(tint.r, tint.g, tint.b, a);
    }
}
