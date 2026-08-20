using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds one Settings slider to an AudioManager channel.
///
/// Kept as a component rather than a persistent UnityEvent because the scene is
/// generated from an editor script, and float-argument listeners do not survive
/// that round trip cleanly.
/// </summary>
[RequireComponent(typeof(Slider))]
public class AudioSliderBinder : MonoBehaviour
{
    public enum Channel { Music, SFX }

    public Channel channel = Channel.Music;
    public Text valueLabel;

    Slider slider;
    bool suppress;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    void OnEnable()
    {
        slider.onValueChanged.AddListener(OnChanged);

        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged += Pull;

        Pull();
    }

    void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnChanged);

        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged -= Pull;
    }

    void OnChanged(float v)
    {
        if (suppress) return;

        var am = AudioManager.Instance;
        if (am == null) return;

        if (channel == Channel.Music) am.SetMusicVolume(v);
        else                          am.SetSFXVolume(v);

        UpdateLabel(v);
    }

    /// <summary>Refresh from the model without re-entering OnChanged.</summary>
    void Pull()
    {
        var am = AudioManager.Instance;
        if (am == null || slider == null) return;

        float v = channel == Channel.Music ? am.MusicVolume : am.SFXVolume;

        suppress = true;
        slider.value = v;
        suppress = false;

        UpdateLabel(v);
    }

    void UpdateLabel(float v)
    {
        if (valueLabel != null)
            valueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
    }
}
