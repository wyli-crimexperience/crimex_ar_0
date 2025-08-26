using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManagerAR : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle vibrationToggle;

    [Header("Graphics Settings (AR/Android)")]
    [SerializeField] private Slider renderScaleSlider; // 0.5 - 1.5 typical range
    [SerializeField] private TMP_Dropdown msaaDropdown; // Off / 2x / 4x
    [SerializeField] private Toggle arBackgroundToggle;

    private void Start()
    {
        // --- AUDIO ---
        if (volumeSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat("MainVolume", 1f);
            AudioListener.volume = savedVolume;
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (vibrationToggle != null)
        {
            bool vibrationEnabled = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;
            vibrationToggle.isOn = vibrationEnabled;
            vibrationToggle.onValueChanged.AddListener(SetVibration);

            // Sync with UIAudioManager at start
            if (UIAudioManager.Instance != null)
                UIAudioManager.Instance.SetHapticFeedback(vibrationEnabled);
        }

        // --- GRAPHICS ---
        if (renderScaleSlider != null)
        {
            float savedScale = PlayerPrefs.GetFloat("RenderScale", 1f);
            renderScaleSlider.value = savedScale;
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = savedScale;
            renderScaleSlider.onValueChanged.AddListener(SetRenderScale);
        }

        if (msaaDropdown != null)
        {
            msaaDropdown.ClearOptions();
            msaaDropdown.AddOptions(new System.Collections.Generic.List<string> { "Off", "2x", "4x" });

            int savedMSAA = PlayerPrefs.GetInt("MSAA", 2); // default 4x
            QualitySettings.antiAliasing = savedMSAA;
            msaaDropdown.value = savedMSAA switch { 0 => 0, 2 => 1, 4 => 2, _ => 2 };
            msaaDropdown.onValueChanged.AddListener(SetMSAA);
        }

        if (arBackgroundToggle != null)
        {
            bool savedBackground = PlayerPrefs.GetInt("ARBackground", 1) == 1;
            arBackgroundToggle.isOn = savedBackground;
            arBackgroundToggle.onValueChanged.AddListener(SetARBackground);
            SetARBackground(savedBackground);
        }
    }

    // --- AUDIO ---
    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MainVolume", volume);
    }

    public void SetVibration(bool enabled)
    {
        PlayerPrefs.SetInt("VibrationEnabled", enabled ? 1 : 0);

        if (UIAudioManager.Instance != null)
        {
            UIAudioManager.Instance.SetHapticFeedback(enabled);

            // Give immediate feedback when vibration is turned ON
            if (enabled)
            {
                UIAudioManager.Instance.PlayButtonPress(); // plays sound + haptic
                // OR call vibration directly:
                // UIAudioManager.Instance.TestHapticFeedback();
            }
        }
        else
        {
            Debug.LogWarning("UIAudioManager.Instance is NULL!");
            if (enabled) Handheld.Vibrate(); // fallback test vibrate
        }
    }

    // --- GRAPHICS ---
    public void SetRenderScale(float scale)
    {
        UnityEngine.XR.XRSettings.eyeTextureResolutionScale = scale;
        PlayerPrefs.SetFloat("RenderScale", scale);
    }

    public void SetMSAA(int index)
    {
        int aaLevel = index switch { 0 => 0, 1 => 2, 2 => 4, _ => 4 };
        QualitySettings.antiAliasing = aaLevel;
        PlayerPrefs.SetInt("MSAA", aaLevel);
    }

    public void SetARBackground(bool enabled)
    {
        var arBackground = Camera.main.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
        if (arBackground != null) arBackground.enabled = enabled;

        PlayerPrefs.SetInt("ARBackground", enabled ? 1 : 0);
    }

    public void ResetToDefaults()
    {
        SetVolume(1f);
        SetRenderScale(1f);
        SetMSAA(2); // default 4x
        SetARBackground(true);
        SetVibration(true);

        if (volumeSlider != null) volumeSlider.value = 1f;
        if (renderScaleSlider != null) renderScaleSlider.value = 1f;
        if (msaaDropdown != null) msaaDropdown.value = 2;
        if (arBackgroundToggle != null) arBackgroundToggle.isOn = true;
        if (vibrationToggle != null) vibrationToggle.isOn = true;
    }
}
