using UnityEngine;

/// <summary>
/// Keeps its RectTransform inside the device's safe area — the region not
/// covered by a notch, camera cutout, rounded corner or gesture bar.
///
/// Put this on a full-screen container and parent the interactive UI to it.
/// Backgrounds should stay outside it so artwork still bleeds to the edges.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;

    Rect lastSafeArea;
    int  lastWidth;
    int  lastHeight;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        // Rotation and resolution changes both move the safe area, and neither
        // raises an event we can hook, so it is polled.
        if (Screen.safeArea == lastSafeArea
            && Screen.width == lastWidth
            && Screen.height == lastHeight) return;

        Apply();
    }

    void Apply()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = Screen.safeArea;
        lastSafeArea = safe;
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;

        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        // A malformed safe area (some emulators report one) would collapse the
        // whole UI, so fall back to full screen instead.
        if (min.x < 0f || min.y < 0f || max.x > 1f || max.y > 1f
            || max.x - min.x < 0.5f || max.y - min.y < 0.5f)
        {
            min = Vector2.zero;
            max = Vector2.one;
        }

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
