using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialAnimator : MonoBehaviour
{
    public Line[] segsToTap;        // real Line segments to tap (in order)
    public Line[] allSegs;          // all segments (to reset)
    public Image finger;            // hand pointer
    public Image pulseRing;         // pulsing circle highlight
    public Text lblHint;            // popup hint text
    public Image hintBg;            // hint background
    public Text lblCongrats;        // congrats text

    string[] hintMessages = {
        "Tap segments to\nbuild the first number!",
        "Now set the\noperator: +",
        "Build the\nsecond number!",
        "Complete the\nanswer below the line!",
        "CORRECT!"
    };

    void OnEnable()
    {
        StartCoroutine(RunDemo());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator RunDemo()
    {
        while (true)
        {
            // ALL segments visible: tappable = Active (amber), non-tappable = Inactive (dark amber)
            // First set ALL to Inactive (dark but visible housing)
            if (allSegs != null)
                foreach (var s in allSegs)
                    if (s) { s.SetInactive(); s.transform.localScale = Vector3.one; }

            // Then set tappable ones to Active (brighter amber)
            if (segsToTap != null)
                foreach (var s in segsToTap)
                    if (s) { s.SetActive(); s.transform.localScale = Vector3.one; }

            if (finger) finger.gameObject.SetActive(false);
            if (pulseRing) pulseRing.gameObject.SetActive(false);
            if (lblCongrats) lblCongrats.gameObject.SetActive(false);
            ShowHint("Watch how to solve!", true);

            yield return new WaitForSeconds(2f);

            // Tap each segment with animation
            int phase = -1;
            for (int i = 0; i < segsToTap.Length; i++)
            {
                if (segsToTap[i] == null) continue;

                // Determine phase for hint
                int newPhase = GetPhase(i);
                if (newPhase != phase)
                {
                    phase = newPhase;
                    ShowHint(hintMessages[Mathf.Min(phase, hintMessages.Length - 1)], true);
                    yield return new WaitForSeconds(0.5f);
                }

                var target = segsToTap[i].GetComponent<RectTransform>();

                // Show pulse ring
                yield return PulseAt(target, 2);

                // Move finger
                yield return MoveFingerTo(target);

                // Tap
                yield return TapAnim();

                // Light up segment (Selected = bright glow) with click sound
                segsToTap[i].SetSelected();
                PlayClickSound();

                // Hide pulse & finger
                if (pulseRing) pulseRing.gameObject.SetActive(false);
                if (finger) finger.gameObject.SetActive(false);

                yield return new WaitForSeconds(0.2f);
            }

            // Success!
            if (finger) finger.gameObject.SetActive(false);
            if (pulseRing) pulseRing.gameObject.SetActive(false);
            ShowHint("", false);

            if (lblCongrats)
            {
                lblCongrats.gameObject.SetActive(true);
                lblCongrats.text = "CORRECT!";
            }

            // Flash all active segments
            for (int f = 0; f < 4; f++)
            {
                foreach (var s in segsToTap)
                    if (s) s.SetSelected();
                yield return new WaitForSeconds(0.2f);
                foreach (var s in segsToTap)
                    if (s) s.SetActive();
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(2f);
            if (lblCongrats) lblCongrats.gameObject.SetActive(false);
            yield return new WaitForSeconds(1f);
        }
    }

    // Which phase based on segment index (customize per equation)
    int GetPhase(int idx)
    {
        if (idx < 7) return 0;      // first number segments
        if (idx < 9) return 1;      // operator
        if (idx < 19) return 2;     // second number
        return 3;                    // answer
    }

    void ShowHint(string text, bool show)
    {
        if (lblHint) { lblHint.text = text; lblHint.gameObject.SetActive(show); }
        if (hintBg) hintBg.gameObject.SetActive(show);
    }

    IEnumerator PulseAt(RectTransform target, int count)
    {
        if (pulseRing == null) yield break;
        pulseRing.gameObject.SetActive(true);
        pulseRing.rectTransform.position = target.position;

        for (int p = 0; p < count; p++)
        {
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                float n = t / 0.45f;
                pulseRing.rectTransform.localScale = Vector3.one * (1f + n * 0.6f);
                pulseRing.color = new Color(0.96f, 0.62f, 0.04f, (1f - n) * 0.6f);
                yield return null;
            }
        }
        pulseRing.rectTransform.localScale = Vector3.one;
        pulseRing.color = new Color(0.96f, 0.62f, 0.04f, 0.4f);
    }

    IEnumerator MoveFingerTo(RectTransform target)
    {
        if (finger == null) yield break;
        finger.gameObject.SetActive(true);

        Vector3 start = finger.rectTransform.position;
        Vector3 dest = target.position + new Vector3(20, -25, 0);

        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            float e = t / 0.3f;
            e = e * e * (3f - 2f * e);
            finger.rectTransform.position = Vector3.Lerp(start, dest, e);
            yield return null;
        }
        finger.rectTransform.position = dest;
    }

    void PlayClickSound()
    {
        if (AudioManager.Instance == null || AudioManager.Instance.IsMuted) return;
        const int sampleRate = 44100;
        const float duration = 0.1f;
        int samples = (int)(sampleRate * duration);
        var clip = AudioClip.Create("tutClick", samples, 1, sampleRate, false);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float freq = 800f * Mathf.Exp(-t * 10f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - t / duration);
        }
        clip.SetData(data, 0);
        AudioManager.Instance.PlaySFX(clip);
    }

    IEnumerator TapAnim()
    {
        if (finger == null) yield break;
        var orig = finger.rectTransform.localScale;

        for (float t = 0; t < 0.08f; t += Time.deltaTime)
        {
            finger.rectTransform.localScale = orig * Mathf.Lerp(1f, 0.8f, t / 0.08f);
            yield return null;
        }
        for (float t = 0; t < 0.08f; t += Time.deltaTime)
        {
            finger.rectTransform.localScale = orig * Mathf.Lerp(0.8f, 1f, t / 0.08f);
            yield return null;
        }
        finger.rectTransform.localScale = orig;
    }
}
