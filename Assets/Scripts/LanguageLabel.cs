using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the language currently in use, in that language.
///
/// Deliberately NOT a LocalizedText: a picker that says "Deutsch" only while
/// you are already reading German is no use to someone who set the wrong one
/// by accident, so every language names itself.
/// </summary>
[RequireComponent(typeof(Text))]
public class LanguageLabel : MonoBehaviour
{
    Text label;

    void Awake() { label = GetComponent<Text>(); }

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

        label.text = Loc.CurrentInfo.nativeName;

        var f = Loc.UIFont;
        if (f != null) label.font = f;
    }
}
