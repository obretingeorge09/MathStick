using UnityEngine;

public class EquationLayout : MonoBehaviour
{
    [System.Serializable]
    public struct ElementPos
    {
        public RectTransform rt;
        public Vector2 portraitPos;
        public Vector2 landscapePos;
    }

    public ElementPos[] elements;
    public RectTransform divider;
    public RectTransform dividerGlow;
    public RectTransform equalsSign; // shown only in landscape
    public RectTransform eqBackground; // hidden in landscape
    public RectTransform container;

    public Vector2 containerPortraitSize = new Vector2(700, 700);
    public Vector2 containerLandscapeSize = new Vector2(1200, 300);
    public float portraitScale = 1f;
    public float landscapeScale = 1f;

    bool currentLandscape = false;

    Canvas rootCanvas;
    RectTransform canvasRect;

    void OnEnable()
    {
        // Find the root canvas to get actual rendered dimensions
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            var root = rootCanvas.rootCanvas;
            canvasRect = root.GetComponent<RectTransform>();
        }

        currentLandscape = CheckLandscape();
        Apply(currentLandscape);
    }

    void Update()
    {
        bool landscape = CheckLandscape();
        if (landscape != currentLandscape)
        {
            currentLandscape = landscape;
            Debug.Log($"EquationLayout: switched to {(landscape ? "LANDSCAPE" : "PORTRAIT")}");
            Apply(landscape);
        }
    }

    bool CheckLandscape()
    {
        // Use canvas rect dimensions — these reflect actual rendered area
        // accounting for CanvasScaler and device simulator
        if (canvasRect != null)
        {
            var r = canvasRect.rect;
            return r.width > r.height;
        }
        // Fallback to screen dimensions
        return Screen.width > Screen.height;
    }

    public void ForceApply(bool landscape)
    {
        currentLandscape = landscape;
        Apply(landscape);
    }

    void Apply(bool landscape)
    {
        if (elements == null) return;

        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i].rt != null)
                elements[i].rt.anchoredPosition = landscape ? elements[i].landscapePos : elements[i].portraitPos;
        }

        if (divider != null) divider.gameObject.SetActive(!landscape);
        if (dividerGlow != null) dividerGlow.gameObject.SetActive(!landscape);
        if (equalsSign != null) equalsSign.gameObject.SetActive(landscape);
        if (eqBackground != null) eqBackground.gameObject.SetActive(!landscape);

        if (container != null)
        {
            container.sizeDelta = landscape ? containerLandscapeSize : containerPortraitSize;
            container.localScale = Vector3.one * (landscape ? landscapeScale : portraitScale);
        }
    }
}
