using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;

public class SettingsManagerAR : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private Slider volumeSlider;

    [Header("Graphics Settings (AR/Android)")]
    [SerializeField] private Slider renderScaleSlider; // 0.5 - 1.5 typical range
    [SerializeField] private TMP_Dropdown msaaDropdown; // Off / 2x / 4x
    [SerializeField] private Toggle arBackgroundToggle; // Optional

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
        // In ARFoundation, the ARCameraBackground component controls this
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
    }
}
