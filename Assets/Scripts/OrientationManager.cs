using UnityEngine;
using System;

/// <summary>
/// Global orientation detection using Canvas dimensions.
/// Other scripts subscribe to OnOrientationChanged.
/// </summary>
public class OrientationManager : MonoBehaviour
{
    public static OrientationManager Instance { get; private set; }

    public event Action<bool> OnOrientationChanged; // true = landscape
    public bool IsLandscape { get; private set; }

    RectTransform canvasRect;
    bool initialized;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(this); return; }
    }

    void Update()
    {
        // Find canvas lazily (may not exist at Awake time)
        if (canvasRect == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;
        }

        var r = canvasRect.rect;
        bool landscape = r.width > r.height;

        if (!initialized)
        {
            IsLandscape = landscape;
            initialized = true;
            return;
        }

        if (landscape != IsLandscape)
        {
            IsLandscape = landscape;
            Debug.Log($"OrientationManager: {(landscape ? "LANDSCAPE" : "PORTRAIT")} (canvas {r.width}x{r.height})");
            OnOrientationChanged?.Invoke(landscape);
        }
    }
}
