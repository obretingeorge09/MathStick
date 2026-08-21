using UnityEngine;
using UnityEngine.UI;

// The +/− operator: one key, one tap. Minus becomes plus, plus becomes minus.
//
// It used to be two independently tappable matchsticks, which turned two
// meanings into four states — "neither lit" and "only the vertical lit" were
// dead ends that could never be correct. CheckAnswer fell through silently on
// both, so a player could place every digit right and have the game say
// nothing. In Hard, with two operators, 12 of the 16 combinations were dead.
//
// As a single toggle there is no invalid state left to get stuck in.
public class PlusMinus : MonoBehaviour
{
    public Line line1 = null;   // horizontal bar — always shown
    public Line line2 = null;   // vertical bar — shown only for plus

    bool isPlus = false;

    void Awake()
    {
        EnsureClickTarget();
    }

    /// <summary>
    /// A scene built before this became a single key still has two separately
    /// tappable bars and no Button. The state now lives in a bool, so tapping
    /// a bar there changes its colour but never the operator — it sticks on
    /// minus forever. Rather than fail that way, build the missing key here.
    /// </summary>
    void EnsureClickTarget()
    {
        if (GetComponentInChildren<Button>(true) != null) return;   // scene is current

        Debug.LogWarning("PlusMinus: this scene predates the single-key operator. " +
                         "Built a fallback key at runtime — re-run PlusMinus > Build Scene.");

        // The bars must stop eating the tap before the key can receive it
        foreach (var line in new[] { line1, line2 })
        {
            if (line == null) continue;
            var img = line.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }

        var go = new GameObject("btn_face");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var face = go.AddComponent<Image>();
        face.color = new Color(0f, 0f, 0f, 0.01f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = face;
        btn.onClick.AddListener(Toggle);

        // Behind the bars visually; raycasts still reach it since they are off
        go.transform.SetAsFirstSibling();
    }

    public bool IsPlus()  => isPlus;
    public bool IsMinus() => !isPlus;

    /// <summary>Flip the operator. Wired to the key's Button.</summary>
    public void Toggle()
    {
        isPlus = !isPlus;
        Apply();

        AudioManager.Instance?.PlaySFX(ClickSound());
        Messenger.Broadcast(Message.CheckForSolution);
    }

    /// <summary>Start of a round. Minus is the resting state, and it is valid.</summary>
    public void ResetToggle()
    {
        isPlus = false;
        Apply();
    }

    public void SetPlus()  { isPlus = true;  Apply(); }
    public void SetMinus() { isPlus = false; Apply(); }

    void Apply()
    {
        if (line1 != null)
        {
            line1.gameObject.SetActive(true);
            line1.SetSelected();
        }

        if (line2 != null)
        {
            // Always present, lit only for plus. Hiding it entirely was a
            // mistake: it made the key look like a minus sign that happened to
            // be tappable, with no hint that plus was even an option. The
            // surrounding key frame is what stops the two bars reading as
            // separate sticks, so the unlit bar can stay — it is the only thing
            // telling the player what this control can become.
            line2.gameObject.SetActive(true);

            if (isPlus) line2.SetSelected();
            else        line2.SetActive();   // dim, the way an unlit segment looks
        }
    }

    // Same procedural click the segments use, so the operator does not sound
    // like a different kind of control.
    static AudioClip s_click;

    static AudioClip ClickSound()
    {
        if (s_click != null) return s_click;

        const int rate = 44100;
        const float dur = 0.1f;
        int n = (int)(rate * dur);

        s_click = AudioClip.Create("pmClick", n, 1, rate, false);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float freq = 800f * Mathf.Exp(-t * 10f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - t / dur);
        }
        s_click.SetData(data, 0);
        return s_click;
    }
}
