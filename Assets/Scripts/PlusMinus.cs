using UnityEngine;

// The +/− operator built from two matchstick segments.
// Horizontal bar (line1) + vertical bar (line2) selected = PLUS
// Only horizontal bar (line1) selected                   = MINUS
// Player taps each matchstick individually, just like digit segments.
public class PlusMinus : MonoBehaviour
{
    public Line line1 = null; // horizontal bar
    public Line line2 = null; // vertical bar

    public bool IsPlus()
    {
        return line1 != null && line1.IsSelected()
            && line2 != null && line2.IsSelected();
    }

    public bool IsMinus()
    {
        return line1 != null && line1.IsSelected()
            && (line2 == null || !line2.IsSelected());
    }

    // Called at the start of each round — both bars unlit, player must compose
    public void ResetToggle()
    {
        if (line1) line1.SetActive();
        if (line2) line2.SetActive();
    }

    // Legacy stubs
    public void SetPlus()  { if (line1) line1.SetSelected(); if (line2) line2.SetSelected(); }
    public void SetMinus() { if (line1) line1.SetSelected(); if (line2) line2.SetActive(); }
}
