using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a full-screen panel scrollable when content doesn't fit (landscape).
/// Attach to the panel GameObject. Content height is set to referenceHeight (portrait design height).
/// In portrait, no scrolling (content fits). In landscape, vertical scrolling enabled.
/// </summary>
public class ScrollablePanel : MonoBehaviour
{
    [Tooltip("Height of the content in reference pixels (portrait design)")]
    public float referenceHeight = 1920f;

    ScrollRect scrollRect;
    RectTransform contentRect;
    bool setup;

    void OnEnable()
    {
        if (!setup) Setup();

        ResizeContent();

        // Reset scroll position
        if (scrollRect != null && scrollRect.content != null)
            scrollRect.content.anchoredPosition = Vector2.zero;
    }

    void OnRectTransformDimensionsChange()
    {
        if (setup) ResizeContent();
    }

    /// <summary>
    /// The content box must never be shorter than the panel itself. A fixed
    /// 1920 box on a 2120-tall screen left a dead strip along the bottom and
    /// pulled every bottom-anchored child up off the screen edge.
    /// </summary>
    void ResizeContent()
    {
        if (contentRect == null) return;

        var panelRt = GetComponent<RectTransform>();
        float panelHeight = panelRt.rect.height;
        float h = Mathf.Max(referenceHeight, panelHeight);

        if (!Mathf.Approximately(contentRect.sizeDelta.y, h))
            contentRect.sizeDelta = new Vector2(0, h);

        // Only scroll when the design actually overflows the screen
        if (scrollRect != null)
            scrollRect.vertical = h > panelHeight + 1f;
    }

    void Setup()
    {
        setup = true;
        var panelRt = GetComponent<RectTransform>();

        // Create a viewport (this panel becomes the viewport area)
        // We need to reparent all children into a content container

        // Create content holder
        var contentGO = new GameObject("_ScrollContent");
        contentGO.transform.SetParent(transform, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        // Anchor to top, stretch width
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, referenceHeight);
        contentRect.anchoredPosition = Vector2.zero;

        // Move all children (except _ScrollContent) into content holder
        var children = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            children[i] = transform.GetChild(i);

        foreach (var child in children)
        {
            if (child != contentGO.transform)
                child.SetParent(contentGO.transform, false);
        }

        // Add mask to clip content
        var maskImg = GetComponent<Image>();
        if (maskImg == null)
        {
            maskImg = gameObject.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0);
            maskImg.raycastTarget = true;
        }
        gameObject.AddComponent<RectMask2D>();

        // Add ScrollRect
        scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
    }
}
