using UnityEngine;
using TMPro;
using Vuforia;
using System.Collections.Generic;

public class ModelSwitcher : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_Dropdown modelDropdown;

    [Header("Models")]
    public GameObject[] models;
    [SerializeField] private string[] customModelNames; // Optional custom names for dropdown

    [Header("Vuforia")]
    public ObserverBehaviour targetObserver;

    [Header("Settings")]
    [SerializeField] private bool hideModelsWhenTargetLost = true;
    [SerializeField] private bool rememberLastSelection = true;
    [SerializeField] private float transitionDelay = 0.1f; // Small delay for smoother transitions

    private bool isTargetVisible = false;
    private int lastSelectedIndex = -1;
    private int currentActiveModel = -1;

    #region Unity Lifecycle
    private void Start()
    {
        InitializeComponents();
        SetupDropdown();
        SetupVuforiaCallbacks();

        // Load saved selection if enabled
        if (rememberLastSelection)
        {
            lastSelectedIndex = PlayerPrefs.GetInt("ModelSwitcher_LastSelection", 0);
            modelDropdown.value = lastSelectedIndex;
        }

        // Initialize with all models hidden
        SetActiveModel(-1);
    }

    private void OnDestroy()
    {
        CleanupVuforiaCallbacks();

        // Save current selection if enabled
        if (rememberLastSelection && modelDropdown != null)
        {
            PlayerPrefs.SetInt("ModelSwitcher_LastSelection", modelDropdown.value);
        }
    }

    private void OnValidate()
    {
        // Validate arrays in editor
        if (customModelNames != null && models != null && customModelNames.Length != models.Length)
        {
            Debug.LogWarning("Custom model names array length doesn't match models array length!");
        }
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Validate required components
        if (modelDropdown == null)
        {
            Debug.LogError("ModelSwitcher: Model dropdown is not assigned!");
            enabled = false;
            return;
        }

        if (models == null || models.Length == 0)
        {
            Debug.LogError("ModelSwitcher: No models assigned!");
            enabled = false;
            return;
        }

        if (targetObserver == null)
        {
            Debug.LogWarning("ModelSwitcher: Target observer is not assigned. Models will always be visible.");
        }

        // Remove null models from array
        var validModels = new List<GameObject>();
        foreach (var model in models)
        {
            if (model != null)
                validModels.Add(model);
            else
                Debug.LogWarning("ModelSwitcher: Null model found in models array!");
        }
        models = validModels.ToArray();
    }

    private void SetupDropdown()
    {
        if (modelDropdown == null) return;

        // Clear existing options
        modelDropdown.ClearOptions();

        // Build options list
        var options = new List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < models.Length; i++)
        {
            string modelName;

            // Use custom name if available, otherwise use GameObject name
            if (customModelNames != null && i < customModelNames.Length && !string.IsNullOrEmpty(customModelNames[i]))
            {
                modelName = customModelNames[i];
            }
            else
            {
                modelName = models[i].name;
            }

            options.Add(new TMP_Dropdown.OptionData(modelName));
        }

        // Add options and setup listener
        modelDropdown.AddOptions(options);
        modelDropdown.onValueChanged.RemoveAllListeners(); // Prevent duplicate listeners
        modelDropdown.onValueChanged.AddListener(OnModelSelected);
    }

    private void SetupVuforiaCallbacks()
    {
        if (targetObserver != null)
        {
            targetObserver.OnTargetStatusChanged += OnTargetStatusChanged;
        }
    }

    private void CleanupVuforiaCallbacks()
    {
        if (targetObserver != null)
        {
            targetObserver.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }
    #endregion

    #region Vuforia Callbacks
    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool wasVisible = isTargetVisible;
        isTargetVisible = status.Status == Status.TRACKED || status.StatusInfo == StatusInfo.NORMAL;

        // Only process if visibility state changed
        if (wasVisible != isTargetVisible)
        {
            if (isTargetVisible)
            {
                // Target became visible - show selected model
                if (transitionDelay > 0)
                {
                    Invoke(nameof(ShowSelectedModel), transitionDelay);
                }
                else
                {
                    ShowSelectedModel();
                }
            }
            else if (hideModelsWhenTargetLost)
            {
                // Target lost - hide all models
                SetActiveModel(-1);
            }
        }
    }

    private void ShowSelectedModel()
    {
        if (isTargetVisible)
        {
            OnModelSelected(modelDropdown.value);
        }
    }
    #endregion

    #region Model Management
    private void OnModelSelected(int index)
    {
        // Validate index
        if (index < 0 || index >= models.Length)
        {
            Debug.LogWarning($"ModelSwitcher: Invalid model index {index}");
            return;
        }

        lastSelectedIndex = index;

        // Only show model if target is visible (or if no target observer is assigned)
        if (isTargetVisible || targetObserver == null)
        {
            SetActiveModel(index);
        }
    }

    private void SetActiveModel(int activeIndex)
    {
        // Skip if already showing this model
        if (currentActiveModel == activeIndex) return;

        // Deactivate all models first
        for (int i = 0; i < models.Length; i++)
        {
            if (models[i] != null)
                models[i].SetActive(false);
        }

        // Activate selected model
        if (activeIndex >= 0 && activeIndex < models.Length && models[activeIndex] != null)
        {
            models[activeIndex].SetActive(true);
            currentActiveModel = activeIndex;
        }
        else
        {
            currentActiveModel = -1;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Programmatically select a model by index
    /// </summary>
    public void SelectModel(int index)
    {
        if (index >= 0 && index < models.Length)
        {
            modelDropdown.value = index;
            OnModelSelected(index);
        }
    }

    /// <summary>
    /// Programmatically select a model by name
    /// </summary>
    public void SelectModel(string modelName)
    {
        for (int i = 0; i < models.Length; i++)
        {
            string compareName = (customModelNames != null && i < customModelNames.Length && !string.IsNullOrEmpty(customModelNames[i]))
                ? customModelNames[i]
                : models[i].name;

            if (compareName.Equals(modelName, System.StringComparison.OrdinalIgnoreCase))
            {
                SelectModel(i);
                return;
            }
        }
        Debug.LogWarning($"ModelSwitcher: Model with name '{modelName}' not found!");
    }

    /// <summary>
    /// Get the currently active model GameObject
    /// </summary>
    public GameObject GetActiveModel()
    {
        return (currentActiveModel >= 0 && currentActiveModel < models.Length) ? models[currentActiveModel] : null;
    }

    /// <summary>
    /// Get the index of the currently active model
    /// </summary>
    public int GetActiveModelIndex()
    {
        return currentActiveModel;
    }

    /// <summary>
    /// Refresh the dropdown options (useful if models array changes at runtime)
    /// </summary>
    public void RefreshDropdown()
    {
        SetupDropdown();
    }
    #endregion
}