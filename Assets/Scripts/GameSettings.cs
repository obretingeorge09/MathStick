using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    // Segment color options
    public static readonly Color[] SegmentColors = {
        new Color(0.98f, 0.88f, 0.10f),  // 0 Yellow (default)
        new Color(1.00f, 0.27f, 0.14f),  // 1 Red
        new Color(0.26f, 0.52f, 0.96f),  // 2 Blue
        new Color(0.30f, 0.87f, 0.37f),  // 3 Green
        new Color(1.00f, 0.41f, 0.71f),  // 4 Pink
        new Color(0.73f, 0.33f, 0.83f),  // 5 Purple
        new Color(0.55f, 0.36f, 0.96f),  // 6 Violet
        new Color(1.00f, 0.60f, 0.00f),  // 7 Orange
    };

    public static readonly string[] SegmentColorNames = {
        "YELLOW", "RED", "BLUE", "GREEN", "PINK", "PURPLE", "VIOLET", "ORANGE"
    };

    /// <summary>
    /// Backgrounds, dark first then light. Every option used to be a near-black
    /// from one palette, so the eight swatches were hard to tell apart and the
    /// choice barely showed. The second row is deliberately from the other end:
    /// a real display is as often dark digits on a pale panel as the reverse.
    ///
    /// The first four keep their old indices so a saved choice still resolves
    /// to the colour it picked.
    /// </summary>
    public static readonly Color[] BackgroundColors = {
        new Color(0.03f, 0.05f, 0.10f),  // 0 Navy (default)
        new Color(0.00f, 0.00f, 0.00f),  // 1 Black
        new Color(0.10f, 0.05f, 0.05f),  // 2 Dark red
        new Color(0.05f, 0.10f, 0.05f),  // 3 Dark green
        new Color(0.94f, 0.92f, 0.87f),  // 4 Paper — warm off-white
        new Color(0.78f, 0.83f, 0.75f),  // 5 Mint — the classic LCD panel
        new Color(0.89f, 0.90f, 0.92f),  // 6 Silver — cool light grey
        new Color(0.91f, 0.85f, 0.72f),  // 7 Sand — warm light
    };

    public static readonly string[] BackgroundColorNames = {
        "NAVY", "BLACK", "DARK RED", "DARK GREEN", "PAPER", "MINT", "SILVER", "SAND"
    };

    /// <summary>Index of the first light background — where the second row starts.</summary>
    public const int FIRST_LIGHT_BG = 4;

    int segColorIndex;
    int bgColorIndex;

    public int SegColorIndex => segColorIndex;
    public int BgColorIndex  => bgColorIndex;

    public Color SelectedBgColor => BackgroundColors[bgColorIndex];

    Color RawSegColor => SegmentColors[segColorIndex];

    static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    /// <summary>
    /// Whether the board is pale enough that an emissive palette would vanish
    /// on it. Measured rather than compared against FIRST_LIGHT_BG, so adding a
    /// colour to the table cannot get this wrong.
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
