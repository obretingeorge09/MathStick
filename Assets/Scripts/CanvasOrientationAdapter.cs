using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adjusts CanvasScaler.matchWidthOrHeight based on screen orientation.
/// Portrait: match=0.5 (balanced)
/// Landscape: match=0 (match width → height shrinks → content scrollable)
/// Uses Screen dimensions (not canvas rect) to avoid feedback loop.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasOrientationAdapter : MonoBehaviour
{
    CanvasScaler scaler;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
    }

    void Update()
    {
        if (scaler == null) return;
        bool landscape = Screen.width > Screen.height;
        scaler.matchWidthOrHeight = landscape ? 0f : 0.5f;
    }
}
