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

    // ── Palette — reads from GameSettings if available ────────────────
    static Color SegSelected {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.SelectedSegColor : new Color(0.75f, 1.00f, 0.15f, 1f);
        }
    }
    static Color SegActive {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.ActiveSegColor : new Color(0.72f, 0.45f, 0.03f, 1f);
        }
    }
    static Color SegInactive {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.InactiveSegColor : new Color(0.10f, 0.06f, 0.01f, 1f);
        }
    }
    static Color GlowOn {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.GlowColor : new Color(0.70f, 1.00f, 0.10f, 0.45f);
        }
    }
    static readonly Color GlowOff     = new Color(0f, 0f, 0f, 0f);
    static readonly Color ShadowOn    = new Color(0f, 0f, 0f, 0.25f);
    static readonly Color ShadowInactive = new Color(0f, 0f, 0f, 0.10f);
    const float GLOW_PAD = 50f;

    // ── Sprite cache ───────────────────────────────────────────────────────
    static Sprite s_hSeg;   // horizontal segment sprite
    static Sprite s_vSeg;   // vertical segment sprite
    static Sprite s_hGlow;  // horizontal glow sprite (blurred)
    static Sprite s_vGlow;  // vertical glow sprite (blurred)

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

        // Glow — blurred larger sprite behind everything for diffuse light emission
        Vector2 glowSize = rt.sizeDelta + new Vector2(GLOW_PAD * 2, GLOW_PAD * 2);
        glow = MakeRect("Glow", Vector2.zero, glowSize, GlowOff);
        glow.sprite = horiz
            ? (s_hGlow != null ? s_hGlow : (s_hGlow = MakeGlowSprite(Mathf.RoundToInt(glowSize.x), Mathf.RoundToInt(glowSize.y), true)))
            : (s_vGlow != null ? s_vGlow : (s_vGlow = MakeGlowSprite(Mathf.RoundToInt(glowSize.x), Mathf.RoundToInt(glowSize.y), false)));
        glow.transform.SetAsFirstSibling(); // behind everything

        // Shadow — dark offset copy behind body
        shadow = MakeRect("Shadow", new Vector2(2f, -2f), rt.sizeDelta, ShadowOn);
        shadow.transform.SetSiblingIndex(1); // between glow and body

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
        Apply(SegSelected, GlowOn, ShadowOn);

        // Only bounce if there is somebody to see it. Unity cannot run a
        // coroutine on a hidden object, and the round is set up before the
        // game panel is revealed.
        if (isActiveAndEnabled) StartCoroutine(Bounce());
        else                    transform.localScale = Vector3.one;
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
    void OnGameWon()  { if (state == SegState.Selected && isActiveAndEnabled) StartCoroutine(Phosphor()); }
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
            case SegState.Selected: Apply(SegSelected, GlowOn, ShadowOn);        break;
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
            float pulse = 0.7f + 0.3f * Mathf.Sin(t * 6f);
            if (body)
                body.color = new Color(
                    SegSelected.r * (0.8f + 0.2f * pulse),
                    SegSelected.g * pulse,
                    SegSelected.b * (0.2f + 0.8f * pulse));
            if (glow)
                glow.color = new Color(GlowOn.r, GlowOn.g, GlowOn.b, GlowOn.a * pulse);
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

    // ── Glow sprite — soft gaussian blur for diffuse light emission ─────
    static Sprite MakeGlowSprite(int w, int h, bool horizontal)
    {
        // Use higher-res texture for smooth gradient
        int tw = w * 2, th = h * 2;
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        float cx = tw * 0.5f, cy = th * 0.5f;

        // Small sigma relative to total size — light concentrates at center
        // and fades to zero well before the edges
        float sigX = horizontal ? tw * 0.22f : tw * 0.28f;
        float sigY = horizontal ? th * 0.28f : th * 0.22f;

        for (int y = 0; y < th; y++)
        for (int x = 0; x < tw; x++)
        {
            float dx = (x - cx + 0.5f) / sigX;
            float dy = (y - cy + 0.5f) / sigY;
            float a  = Mathf.Exp(-(dx * dx + dy * dy));
            // Smooth to zero at edges — no hard cutoff
            a *= a; // square for sharper center, softer tails
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 1f);
    }

    // ── Sprite generation — authentic LCD digital-clock segment ─────────
    static Sprite MakeBeveledSprite(int w, int h)
    {
        // Higher resolution for crisp anti-aliased edges
        int tw = Mathf.Max(w, 64);
        int th = Mathf.Max(h, 64);
        if (w > h) { th = Mathf.RoundToInt(tw * (float)h / w); }
        else       { tw = Mathf.RoundToInt(th * (float)w / h); }
        tw = Mathf.Max(tw, 4); th = Mathf.Max(th, 4);

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        bool horizontal = w > h;

        if (horizontal)
        {
            // Horizontal: hexagon with pointed left/right ends  < ══════ >
            float hh    = th * 0.5f;
            float bevel = hh; // 45° pointed ends

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float fy   = y - hh + 0.5f;
                float absY = Mathf.Abs(fy);
                float leftEdge  = bevel * (absY / hh);
                float rightEdge = tw - bevel * (absY / hh);

                if (x >= leftEdge - 0.5f && x <= rightEdge + 0.5f)
                {
                    float aL    = Mathf.Clamp01(x - leftEdge + 1f);
                    float aR    = Mathf.Clamp01(rightEdge - x + 1f);
                    float alpha = Mathf.Min(aL, aR);

                    // Inner bevel: slight brightness gradient for 3D LCD depth
                    float distFromTop = (hh - fy) / th;
                    float lum = 0.82f + 0.18f * (1f - distFromTop);
                    tex.SetPixel(x, y, new Color(lum, lum, lum, alpha));
                }
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        else
        {
            // Vertical: hexagon rotated 90° — pointed top/bottom
            float hw    = tw * 0.5f;
            float bevel = hw;

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float fx   = x - hw + 0.5f;
                float absX = Mathf.Abs(fx);
                float botEdge = bevel * (absX / hw);
                float topEdge = th - bevel * (absX / hw);

                if (y >= botEdge - 0.5f && y <= topEdge + 0.5f)
                {
                    float aB    = Mathf.Clamp01(y - botEdge + 1f);
                    float aT    = Mathf.Clamp01(topEdge - y + 1f);
                    float alpha = Mathf.Min(aB, aT);

                    // Inner bevel: slight brightness gradient for 3D LCD depth
                    float distFromLeft = (hw + fx) / tw;
                    float lum = 0.80f + 0.20f * distFromLeft;
                    tex.SetPixel(x, y, new Color(lum, lum, lum, alpha));
                }
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 1f);
    }

}
