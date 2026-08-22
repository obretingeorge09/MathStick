using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps one label or image readable whichever board it is sitting on.
///
/// The whole interface was built assuming a near-black background, so its
/// colours are the bright end of the palette. Now that a board can be paper or
/// mint, anything drawn over it has to come down with it — amber on off-white
/// is not text. Attach this to a Graphic over the game background and it
/// repaints itself whenever the theme changes.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class BgAdaptiveTint : MonoBehaviour
{
    /// <summary>The colour this was designed in — used as-is on a dark board.</summary>
    public Color darkThemeColor = Color.white;

    /// <summary>
    /// Set when simply darkening is the wrong move — a translucent dark card
    /// behind the digits has to become a translucent PALE one, not a darker
    /// one, or the light board is a dark board with a light border.
    /// </summary>
    public bool overrideOnLight = false;
    public Color lightThemeColor = Color.black;

    Graphic target;

    void Awake()
    {
        target = GetComponent<Graphic>();

        // Whoever built this may not have filled the field in
        if (darkThemeColor == Color.white && target != null) darkThemeColor = target.color;
    }

    void OnEnable()
    {
        Messenger.AddListener(Message.OnThemeChanged, Apply);
        Apply();
    }

    void OnDisable()
    {
        Messenger.TryRemoveListener(Message.OnThemeChanged, Apply);
    }

    void Apply()
    {
        if (target == null) return;

        var gs = GameSettings.Instance;
        if (gs == null) { target.color = darkThemeColor; return; }

        target.color = (overrideOnLight && gs.BackgroundIsLight)
            ? lightThemeColor
            : gs.OnBackground(darkThemeColor);
    }
}
