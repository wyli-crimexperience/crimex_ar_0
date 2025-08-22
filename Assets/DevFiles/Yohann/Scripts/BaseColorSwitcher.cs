using UnityEngine;
using TMPro;
using Vuforia;
using System.Collections.Generic;

public class TextureLightSwitcher : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_Dropdown optionDropdown;

    [Header("Target Model")]
    [SerializeField] private Renderer targetRenderer; // Assign your model's Renderer

    [Header("Textures")]
    [SerializeField] private string[] customNames;
    [SerializeField] private Texture[] baseTextures;   // PNG/JPG textures

    [Header("Spotlights")]
    [SerializeField] private Light[] spotlights; // Assign one Spotlight per option

    [Header("Vuforia")]
    public ObserverBehaviour targetObserver;
    [SerializeField] private bool hideModelWhenTargetLost = true;

    private bool isTargetVisible = false;

    private void Start()
    {
        if (!targetRenderer)
        {
            Debug.LogError("TextureLightSwitcher: No Renderer assigned!");
            enabled = false;
            return;
        }

        if (spotlights != null)
        {
            foreach (var light in spotlights)
                if (light != null) light.enabled = false; // start disabled
        }

        SetupDropdown();
        SetupVuforiaCallbacks();

        // Initialize with first option
        if (baseTextures.Length > 0)
            ApplyPreset(0);
    }

    private void OnDestroy()
    {
        if (targetObserver != null)
            targetObserver.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private void SetupDropdown()
    {
        if (optionDropdown == null) return;

        optionDropdown.ClearOptions();
        var options = new List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < baseTextures.Length; i++)
        {
            string name = (customNames != null && i < customNames.Length && !string.IsNullOrEmpty(customNames[i]))
                ? customNames[i]
                : $"Preset {i + 1}";

            options.Add(new TMP_Dropdown.OptionData(name));
        }

        optionDropdown.AddOptions(options);
        optionDropdown.onValueChanged.AddListener(OnOptionSelected);
    }

    private void SetupVuforiaCallbacks()
    {
        if (targetObserver != null)
            targetObserver.OnTargetStatusChanged += OnTargetStatusChanged;
    }

    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool wasVisible = isTargetVisible;
        isTargetVisible = status.Status == Status.TRACKED || status.StatusInfo == StatusInfo.NORMAL;

        if (wasVisible != isTargetVisible)
        {
            if (isTargetVisible)
            {
                targetRenderer.gameObject.SetActive(true);
                ApplyPreset(optionDropdown.value);
            }
            else if (hideModelWhenTargetLost)
            {
                targetRenderer.gameObject.SetActive(false);
                DisableAllLights();
            }
        }
    }

    private void OnOptionSelected(int index)
    {
        if (isTargetVisible || targetObserver == null)
        {
            ApplyPreset(index);
        }
    }

    private void ApplyPreset(int index)
    {
        if (index < 0 || index >= baseTextures.Length) return;

        Material mat = targetRenderer.material;

        // --- Swap Colormap (Albedo/BaseMap) ---
        if (mat.HasProperty("_BaseMap")) // URP Lit
            mat.SetTexture("_BaseMap", baseTextures[index]);
        else if (mat.HasProperty("_MainTex")) // Built-in Standard
            mat.SetTexture("_MainTex", baseTextures[index]);

        // --- Handle spotlights ---
        DisableAllLights();

        if (spotlights != null && index < spotlights.Length && spotlights[index] != null)
        {
            spotlights[index].enabled = true;
        }
    }

    private void DisableAllLights()
    {
        if (spotlights == null) return;
        foreach (var light in spotlights)
            if (light != null) light.enabled = false;
    }
}
