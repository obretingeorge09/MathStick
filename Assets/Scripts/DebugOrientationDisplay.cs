using UnityEngine;

/// <summary>
/// Temporary debug script — shows Screen and Canvas dimensions on screen.
/// Remove after debugging orientation issues.
/// </summary>
public class DebugOrientationDisplay : MonoBehaviour
{
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = 28;
        style.normal.textColor = Color.red;
        style.fontStyle = FontStyle.Bold;

        string info = $"Screen: {Screen.width}x{Screen.height}\n";

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            var rt = canvas.rootCanvas.GetComponent<RectTransform>();
            var r = rt.rect;
            info += $"Canvas: {r.width:F0}x{r.height:F0}\n";
            info += $"Landscape: {(r.width > r.height ? "YES" : "NO")}";
        }

        GUI.Label(new Rect(10, 10, 600, 120), info, style);
    }
}
