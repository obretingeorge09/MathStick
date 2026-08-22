using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the language list. Selecting it switches the whole game over.
/// </summary>
public class LanguagePicker : MonoBehaviour
{
    public int languageIndex;

    Text label;

    /// <summary>Frame shown only while this is the language in use.</summary>
    public Image ring = null;

    void Start()
    {
        var btn = GetComponentInChildren<Button>(true);
        if (btn != null) btn.onClick.AddListener(OnClick);
    }

    void OnEnable()
    {
        Messenger.AddListener(Message.OnLanguageChanged, Refresh);
        Refresh();
    }

    void OnDisable()
    {
        Messenger.TryRemoveListener(Message.OnLanguageChanged, Refresh);
    }

    void Refresh()
    {
        if (ring != null) ring.enabled = Loc.Current == languageIndex;

        // 简体中文 has to read as 简体中文 whatever language the game is in, so
        // this row never goes through the string table and never keeps the
        // segment font — it always draws in the platform face.
        if (label == null) label = GetComponentInChildren<Text>(true);
        if (label != null && Loc.PlatformFont != null) label.font = Loc.PlatformFont;
    }

    void OnClick()
    {
        Loc.SetLanguage(languageIndex);
    }
}
