using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class Line : MonoBehaviour, IPointerClickHandler
{
    // ── State ──────────────────────────────────────────────────────────────
    enum SegState { Selected, Active, Inactive }
    SegState state = SegState.Active;

    // ── Visuals ────────────────────────────────────────────────────────────
    Image body   = null;  // segment rectangle
    Image shadow = null;  // dark offset duplicate for depth
    Image glow   = null;  // outer halo (larger rect behind body, semi-transparent when selected)

    // ── Palette (7-Segment Display) ───────────────────────────────────────
    // Active = visible amber segment
    static readonly Color SegActive   = new Color(0.72f, 0.45f, 0.03f, 1f);
    // Selected = phosphorescent bright amber
    static readonly Color SegSelected = new Color(0.98f, 0.88f, 0.10f, 1f);
    // Glow when selected (larger rect on top, creates outer halo)
    static readonly Color GlowOn      = new Color(0.98f, 0.88f, 0.10f, 0.60f);
    // Inactive = dark amber housing (visible but unlit, like real 7-segment display)
    static readonly Color SegInactive = new Color(0.10f, 0.06f, 0.01f, 1f);
    // Always off
    static readonly Color GlowOff     = new Color(0f, 0f, 0f, 0f);
    // Shadow colors
    static readonly Color ShadowOn    = new Color(0f, 0f, 0f, 0.25f);
    static readonly Color ShadowInactive = new Color(0f, 0f, 0f, 0.10f);
    // Glow padding constant
    const float GLOW_PAD = 10f;

    // ── Sprite cache for beveled segments ──────────────────────────────────
    static Sprite s_hSeg;  // horizontal segment sprite (76x16)
    static Sprite s_vSeg;  // vertical segment sprite (16x64)

    // ── Setup ──────────────────────────────────────────────────────────────
    void Awake()
    {
        body = GetComponent<Image>();

        var rt = GetComponent<RectTransform>();

        // Apply beveled segment sprite (authentic 7-segment display look)
        bool horiz = rt.sizeDelta.x > rt.sizeDelta.y;
        if (horiz)
            body.sprite = s_hSeg != null ? s_hSeg : (s_hSeg = MakeBeveledSprite(
                Mathf.RoundToInt(rt.sizeDelta.x), Mathf.RoundToInt(rt.sizeDelta.y)));
        else
            body.sprite = s_vSeg != null ? s_vSeg : (s_vSeg = MakeBeveledSprite(
                Mathf.RoundToInt(rt.sizeDelta.x), Mathf.RoundToInt(rt.sizeDelta.y)));

        Messenger.AddListener(Message.GameWon,      OnGameWon);
        Messenger.AddListener(Message.StartNewGame, OnNewGame);

        // Shadow — dark offset copy behind body (renders first = behind)
        shadow = MakeRect("Shadow", new Vector2(2f, -2f), rt.sizeDelta, ShadowOn);
        shadow.transform.SetAsFirstSibling();

        // Glow — slightly larger rect at same center, creates outer halo when selected
        Vector2 glowSize = rt.sizeDelta + new Vector2(GLOW_PAD * 2, GLOW_PAD * 2);
        glow = MakeRect("Glow", Vector2.zero, glowSize, GlowOff);

        // Apply initial state (Active by default, will be overridden by Initialize if needed)
        ApplyState();
    }

    // ── Click ──────────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (state == SegState.Selected)
        {
            SetActive();
            AudioManager.Instance?.PlaySFX(CreateClickSound());
            Messenger.Broadcast(Message.CheckForSolution);
        }
        else if (state == SegState.Active)
        {
            SetSelected();
            AudioManager.Instance?.PlaySFX(CreateClickSound());
            Messenger.Broadcast(Message.CheckForSolution);
        }
    }

    // Generate simple click sound procedurally
    AudioClip CreateClickSound()
    {
        const int sampleRate = 44100;
        const float duration = 0.1f;
        int samples = (int)(sampleRate * duration);
        AudioClip clip = AudioClip.Create("click", samples, 1, sampleRate, false);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float frequency = 800f * Mathf.Exp(-t * 10f); // frequency decay
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - t / duration);
        }

        clip.SetData(data, 0);
        return clip;
    }

    // ── State API ──────────────────────────────────────────────────────────
    public void SetSelected()
    {
        state = SegState.Selected;
        StopAllCoroutines();
        Apply(SegSelected, GlowOff, ShadowOn);
        StartCoroutine(Bounce());
    }

    public void SetActive()
    {
        state = SegState.Active;
        StopAllCoroutines();
        transform.localScale = Vector3.one;  // reset scale when deselecting
        Apply(SegActive, GlowOff, ShadowOn);
    }

    public void SetInactive()
    {
        state = SegState.Inactive;
        StopAllCoroutines();
        Apply(SegInactive, GlowOff, ShadowInactive);
    }

    public bool IsSelected() => state == SegState.Selected;
    public bool IsActive()   => state == SegState.Active;
    public bool IsInactive() => state == SegState.Inactive;

    // ── Events ─────────────────────────────────────────────────────────────
    void OnGameWon()  { if (state == SegState.Selected) StartCoroutine(Phosphor()); }
    void OnNewGame()  { StopAllCoroutines(); ApplyState(); }

    // ── Apply colours ──────────────────────────────────────────────────────
    void Apply(Color seg, Color glw, Color shd)
    {
        if (body)   body.color   = seg;
        if (glow)   glow.color   = glw;
        if (shadow) shadow.color = shd;
        ResetScale();
    }

    void ApplyState()
    {
        switch (state)
        {
            case SegState.Selected: Apply(SegSelected, GlowOff, ShadowOn);       break;
            case SegState.Active:   Apply(SegActive,   GlowOff, ShadowOn);       break;
            case SegState.Inactive: Apply(SegInactive, GlowOff, ShadowInactive); break;
        }
    }

    void ResetScale()
    {
        transform.localScale = Vector3.one;
    }

    // ── Animations ─────────────────────────────────────────────────────────
    IEnumerator Bounce()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.16f;
            transform.localScale = Vector3.one * (1f + 0.20f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        // End at 1.08 scale for selected state (stay slightly larger)
        transform.localScale = Vector3.one * 1.08f;
    }

    IEnumerator Phosphor()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float pulse = 0.7f + 0.3f * Mathf.Sin(t * 8f);
            if (body)
                body.color = new Color(SegSelected.r, SegSelected.g * pulse, SegSelected.b * (0.3f + 0.7f * pulse));
            yield return null;
        }
    }

    // ── Child creation helpers ─────────────────────────────────────────────
    Image MakeRect(string n, Vector2 offset, Vector2 sz, Color col)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = offset;
        rt.sizeDelta = sz;
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    // ── Sprite generation for authentic 7-segment display ──────────────────
    static Sprite MakeBeveledSprite(int w, int h)
    {
        int bevel = Mathf.Min(w, h) / 3;  // ~5px for 16-pixel-thick segments
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int dl = x;           // distance from left
            int dr = w - 1 - x;   // distance from right
            int dt = h - 1 - y;   // distance from top
            int db = y;           // distance from bottom

            // Check if pixel is in a beveled corner (45-degree chamfer)
            bool cut = (dl < bevel && db < bevel && dl + db < bevel) ||  // bottom-left
                       (dr < bevel && db < bevel && dr + db < bevel) ||  // bottom-right
                       (dl < bevel && dt < bevel && dl + dt < bevel) ||  // top-left
                       (dr < bevel && dt < bevel && dr + dt < bevel);    // top-right

            tex.SetPixel(x, y, cut ? Color.clear : Color.white);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
    }

}
