using UnityEngine;
using UnityEngine.UI;

public class SettingsColorPicker : MonoBehaviour
{
    public int colorType;  // 0 = segment, 1 = background
    public int colorIndex;

    void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClick);
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
