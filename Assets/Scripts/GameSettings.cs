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

    // Background color options
    public static readonly Color[] BackgroundColors = {
        new Color(0.03f, 0.05f, 0.10f),  // 0 Dark Navy (default)
        new Color(0.00f, 0.00f, 0.00f),  // 1 Black
        new Color(0.10f, 0.05f, 0.05f),  // 2 Dark Red
        new Color(0.05f, 0.10f, 0.05f),  // 3 Dark Green
        new Color(0.05f, 0.05f, 0.12f),  // 4 Dark Blue
        new Color(0.10f, 0.05f, 0.10f),  // 5 Dark Purple
        new Color(0.08f, 0.08f, 0.08f),  // 6 Dark Gray
        new Color(0.06f, 0.04f, 0.02f),  // 7 Dark Brown
    };

    public static readonly string[] BackgroundColorNames = {
        "NAVY", "BLACK", "DARK RED", "DARK GREEN", "DARK BLUE", "DARK PURPLE", "DARK GRAY", "DARK BROWN"
    };

    int segColorIndex;
    int bgColorIndex;

    public int SegColorIndex => segColorIndex;
    public int BgColorIndex  => bgColorIndex;

    public Color SelectedSegColor => SegmentColors[segColorIndex];
    public Color ActiveSegColor   => SelectedSegColor * 0.5f;
    public Color InactiveSegColor => SelectedSegColor * 0.12f;
    public Color GlowColor        => new Color(SelectedSegColor.r, SelectedSegColor.g, SelectedSegColor.b, 0.45f);
    public Color SelectedBgColor  => BackgroundColors[bgColorIndex];

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        segColorIndex = PlayerPrefs.GetInt("SegColor", 0);
        bgColorIndex  = PlayerPrefs.GetInt("BgColor", 0);
    }

    public void SetSegColor(int index)
    {
        segColorIndex = Mathf.Clamp(index, 0, SegmentColors.Length - 1);
        PlayerPrefs.SetInt("SegColor", segColorIndex);
        PlayerPrefs.Save();
        Messenger.Broadcast(Message.StartNewGame); // refresh visuals
    }

    public void SetBgColor(int index)
    {
        bgColorIndex = Mathf.Clamp(index, 0, BackgroundColors.Length - 1);
        PlayerPrefs.SetInt("BgColor", bgColorIndex);
        PlayerPrefs.Save();
    }
}
