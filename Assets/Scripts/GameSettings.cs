using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    /// <summary>
    /// Six segment colours, spread around the wheel so no two read as the same
    /// choice at a glance. There were eight: pink sat next to red and violet
    /// next to purple, which is two decisions the player does not get anything
    /// for making.
    /// </summary>
    public static readonly Color[] SegmentColors = {
        new Color(0.98f, 0.88f, 0.10f),  // 0 Yellow (default)
        new Color(1.00f, 0.60f, 0.00f),  // 1 Orange
        new Color(1.00f, 0.27f, 0.14f),  // 2 Red
        new Color(0.30f, 0.87f, 0.37f),  // 3 Green
        new Color(0.26f, 0.52f, 0.96f),  // 4 Blue
        new Color(0.73f, 0.33f, 0.83f),  // 5 Purple
    };

    public static readonly string[] SegmentColorNames = {
        "YELLOW", "ORANGE", "RED", "GREEN", "BLUE", "PURPLE"
    };

    /// <summary>
    /// Black or white, and nothing in between.
    ///
    /// There were eight, six of them near-black and hard to tell apart in the
    /// picker or on the board. A segment display is either lit glass or a
    /// printed panel; the shades between were choices that changed nothing.
    /// Everything drawn over the board follows this — see BackgroundIsLight.
    /// </summary>
    public static readonly Color[] BackgroundColors = {
        new Color(0.00f, 0.00f, 0.00f),  // 0 Black (default)
        new Color(1.00f, 1.00f, 1.00f),  // 1 White
    };

    public static readonly string[] BackgroundColorNames = { "BLACK", "WHITE" };

    int segColorIndex;
    int bgColorIndex;

    public int SegColorIndex => segColorIndex;
    public int BgColorIndex  => bgColorIndex;

    public Color SelectedBgColor => BackgroundColors[bgColorIndex];

    Color RawSegColor => SegmentColors[segColorIndex];

    static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    /// <summary>
    /// Whether the board is pale enough that an emissive palette would vanish
    /// on it. Measured from the colour rather than matched against an index,
    /// so adding a shade to the table cannot get this wrong.
    /// </summary>
    public bool BackgroundIsLight => Luminance(SelectedBgColor) > 0.45f;

    /// <summary>The chosen colour taken down to something that reads as ink.</summary>
    static Color Ink(Color c) => new Color(c.r * 0.38f, c.g * 0.38f, c.b * 0.38f, 1f);

    // On a dark board the segments glow: full colour lit, a dim version for a
    // segment that could be lit, near-black for the empty housing. On a pale
    // board that inverts — bright yellow on off-white is invisible — so the lit
    // segment becomes dark ink and the housing a faint tint of the panel.
    public Color SelectedSegColor => BackgroundIsLight ? Ink(RawSegColor) : RawSegColor;

    public Color ActiveSegColor => BackgroundIsLight
        ? Color.Lerp(SelectedBgColor, Ink(RawSegColor), 0.42f)
        : RawSegColor * 0.5f;

    public Color InactiveSegColor => BackgroundIsLight
        ? Color.Lerp(SelectedBgColor, Ink(RawSegColor), 0.14f)
        : RawSegColor * 0.12f;

    // A halo is light spilling from an emitter. On a pale panel there is no
    // emitter, and the same halo reads as a smudge — the drop shadow alone
    // carries the depth there.
    public Color GlowColor => BackgroundIsLight
        ? new Color(0f, 0f, 0f, 0f)
        : new Color(RawSegColor.r, RawSegColor.g, RawSegColor.b, 0.45f);

    /// <summary>
    /// A colour chosen for a dark board, adjusted to stay legible on a pale one.
    /// Keeps the hue so the screen still looks like itself.
    /// </summary>
    public Color OnBackground(Color darkThemeColor)
    {
        if (!BackgroundIsLight) return darkThemeColor;

        var c = Color.Lerp(darkThemeColor, Color.black, 0.74f);
        return new Color(c.r, c.g, c.b, darkThemeColor.a);
    }

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        segColorIndex = PlayerPrefs.GetInt("SegColor", 0);
        bgColorIndex  = PlayerPrefs.GetInt("BgColor", 0);

        // A saved index can outlive the table it was written against
        segColorIndex = Mathf.Clamp(segColorIndex, 0, SegmentColors.Length - 1);
        bgColorIndex  = Mathf.Clamp(bgColorIndex,  0, BackgroundColors.Length - 1);
    }

    void Start()
    {
        // Unity does not order Awake across objects, so a BgAdaptiveTint or a
        // LocalizedText can wake before this one and find Instance still null.
        // It then falls back to the colour it was built with — and nothing
        // corrects it, because the player did not CHANGE the theme, they
        // arrived with one already saved. That is how a dark translucent card
        // ended up sitting over a white board as a grey slab.
        //
        // Start runs after every Awake, so one broadcast here settles it.
        Messenger.Broadcast(Message.OnThemeChanged);
    }

    public void SetSegColor(int index)
    {
        segColorIndex = Mathf.Clamp(index, 0, SegmentColors.Length - 1);
        PlayerPrefs.SetInt("SegColor", segColorIndex);
        PlayerPrefs.Save();
        Broadcast();
    }

    public void SetBgColor(int index)
    {
        bgColorIndex = Mathf.Clamp(index, 0, BackgroundColors.Length - 1);
        PlayerPrefs.SetInt("BgColor", bgColorIndex);
        PlayerPrefs.Save();
        Broadcast();
    }

    void Broadcast()
    {
        // Anything that has to stay readable against the board repaints itself
        Messenger.Broadcast(Message.OnThemeChanged);

        // and the segments are rebuilt, which is what actually recolours them
        Messenger.Broadcast(Message.StartNewGame);
    }
}
