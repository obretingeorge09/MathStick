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

    bool wasLandscape = false;
    bool initialized = false;

    void Start()
    {
        bool isLandscape = Screen.width > Screen.height * 1.2f;
        wasLandscape = isLandscape;
        Apply(isLandscape);
        initialized = true;
    }

    void Update()
    {
        bool isLandscape = Screen.width > Screen.height * 1.2f;
        if (isLandscape != wasLandscape)
        {
            wasLandscape = isLandscape;
            Apply(isLandscape);
        }
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
