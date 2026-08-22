using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts one label through the string table, and gives it a font that can
/// actually draw the result.
///
/// The English text is the key, so this stores what the screen was built with
/// and re-renders it whenever the language changes.
/// </summary>
[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    /// <summary>The English source string — the key into the table.</summary>
    [TextArea] public string key = "";

    /// <summary>
    /// Set for labels whose text is rewritten at runtime (a score, a name, a
    /// countdown). Those own their own content; this only manages the font.
    /// </summary>
    public bool fontOnly = false;

    Text label;
    Font builtWith;

    void Awake()
    {
        label = GetComponent<Text>();
        builtWith = label.font;

        if (string.IsNullOrEmpty(key) && !fontOnly) key = label.text;
    }

    void OnEnable()
    {
        Messenger.AddListener(Message.OnLanguageChanged, Apply);
        Apply();
    }

    void OnDisable()
    {
        Messenger.TryRemoveListener(Message.OnLanguageChanged, Apply);
    }

    void Apply()
    {
        if (label == null) return;

        if (!fontOnly && !string.IsNullOrEmpty(key))
            label.text = Loc.T(key);

        // A translated string in the segment font is a row of empty boxes:
        // DSEG7 has no accented Latin at all, let alone Cyrillic or CJK.
        var f = Loc.UIFont;
        label.font = f != null ? f : builtWith;
    }
}
