using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialAnimator : MonoBehaviour
{
    public Line[] segsToTap;        // real Line segments to tap (in order)
    public Line[] allSegs;          // all segments (to reset)
    public TapIndicator tap;        // ripple marker shown over the segment to press
    public Text lblHint;            // popup hint text
    public Image hintBg;            // hint background
    public Text lblCongrats;        // congrats text

    string[] hintMessages = {
        "Tap segments to\nbuild the first number!",
        "Tap the operator key\nto switch + and -",
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

            // Operators own their own visual state, so clear them explicitly
            foreach (var pm in GetComponentsInChildren<PlusMinus>(true))
                pm.ResetToggle();

            if (tap) tap.Hide();
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

                // Ripple over the target long enough for the eye to land on it
                if (tap) tap.ShowAt(target);
                yield return new WaitForSeconds(0.5f);

                // Contact beat, then the segment lights under it
                if (tap) yield return tap.PlayPress();

                var pmOwner = segsToTap[i].GetComponentInParent<PlusMinus>();
                if (pmOwner != null) pmOwner.SetPlus();   // the key sets both bars
                else                 segsToTap[i].SetSelected();

                PlayClickSound();

                // Let the lit segment be seen before the marker leaves
                yield return new WaitForSeconds(0.12f);
                if (tap) tap.Hide();

                yield return new WaitForSeconds(0.14f);
            }

            // Success!
            if (tap) tap.Hide();
            ShowHint("", false);

            if (lblCongrats)
            {
                lblCongrats.gameObject.SetActive(true);
                lblCongrats.text = Loc.T("CORRECT!");
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
        if (idx < 7)  return 0;     // first number segments
        if (idx < 8)  return 1;     // operator — a single key since the rewrite
        if (idx < 18) return 2;     // second number
        return 3;                   // answer
    }

    void ShowHint(string text, bool show)
    {
        if (lblHint) { lblHint.text = text; lblHint.gameObject.SetActive(show); }
        if (hintBg) hintBg.gameObject.SetActive(show);
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

}
