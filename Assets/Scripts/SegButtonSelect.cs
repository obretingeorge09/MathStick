using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a SegButton show whether it is the chosen option in a group.
///
/// The mode-select panel built its buttons with the chosen one bright and the
/// rest dim, and then never touched them again. Clicking 5 or 7 did change the
/// selection — EnterRandomQueue received the new value — but nothing on screen
/// moved, so the button read as dead. This carries the two looks so the panel
/// can switch between them.
/// </summary>
public class SegButtonSelect : MonoBehaviour
{
    public Image rim = null;
    public Image face = null;
    public Image gloss = null;
    public Text label = null;

    /// <summary>The button's own colour when it is the chosen one.</summary>
    public Color accent = Color.white;

    /// <summary>How far the unchosen look pulls back towards the background.</summary>
    [Range(0f, 1f)] public float dim = 0.38f;

    bool selected = true;

    public bool IsSelected => selected;

    public void SetSelected(bool on)
    {
        selected = on;

        // Same derivation SegButton uses to build a button, scaled down as a
        // whole when unchosen so the surfaces keep their relationship.
        float k = on ? 1f : dim;
        Color c = new Color(accent.r * k, accent.g * k, accent.b * k, 1f);

        if (rim != null) rim.color = c;
        if (face != null) face.color = new Color(c.r * 0.17f, c.g * 0.17f, c.b * 0.17f, 1f);
        if (gloss != null) gloss.color = new Color(c.r, c.g, c.b, on ? 0.11f : 0.05f);
        if (label != null) label.color = on ? accent : c;

        // A chosen key also sits a touch proud of its neighbours. Small, but it
        // is what makes the row read as one control rather than three buttons.
        transform.localScale = on ? Vector3.one * 1.06f : Vector3.one;
    }
}
