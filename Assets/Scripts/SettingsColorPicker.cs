using UnityEngine;
using UnityEngine.UI;

public class SettingsColorPicker : MonoBehaviour
{
    public int colorType;  // 0 = segment, 1 = background
    public int colorIndex;

    /// <summary>Frame shown only while this swatch is the chosen one.</summary>
    public Image ring = null;

    void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClick);
    }

    void OnEnable()
    {
        // The picker showed eight swatches and no indication of which was in
        // use, so the screen could not answer the first question it invites.
        Messenger.AddListener(Message.OnThemeChanged, Refresh);
        Refresh();
    }

    void OnDisable()
    {
        Messenger.TryRemoveListener(Message.OnThemeChanged, Refresh);
    }

    void Refresh()
    {
        if (ring == null) return;

        var gs = GameSettings.Instance;
        bool mine = gs != null &&
                    colorIndex == (colorType == 0 ? gs.SegColorIndex : gs.BgColorIndex);

        ring.enabled = mine;
    }

    void OnClick()
    {
        if (GameSettings.Instance == null) return;

        if (colorType == 0)
            GameSettings.Instance.SetSegColor(colorIndex);
        else
            GameSettings.Instance.SetBgColor(colorIndex);
    }
}
