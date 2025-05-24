using UnityEngine;
using TMPro;
using Vuforia;

public class ModelSwitcher : MonoBehaviour
{
    public TMP_Dropdown modelDropdown; // TextMeshPro Dropdown
    public GameObject[] models;        // Your model prefabs
    public ObserverBehaviour targetObserver; // Assign the ImageTarget or ObserverBehaviour

    private bool isTargetVisible = false;

    private void Start()
    {
        if (targetObserver)
        {
            targetObserver.OnTargetStatusChanged += OnTargetStatusChanged;
        }

        // Clear any existing options first
        modelDropdown.ClearOptions();

        // Build the list of options from model names
        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
        foreach (var model in models)
        {
            if (model != null)
                options.Add(new TMP_Dropdown.OptionData(model.name));
        }

        // Add options to dropdown
        modelDropdown.AddOptions(options);

        // Hook up listener
        modelDropdown.onValueChanged.AddListener(OnModelSelected);

        // Hide all models initially
        SetActiveModel(-1);
    }

    private void OnDestroy()
    {
        if (targetObserver)
        {
            targetObserver.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }

    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        // Good tracking state
        isTargetVisible = status.Status == Status.TRACKED || status.StatusInfo == StatusInfo.NORMAL;

        if (isTargetVisible)
        {
            OnModelSelected(modelDropdown.value);
        }
        else
        {
            SetActiveModel(-1); // Hide all models
        }
    }

    private void OnModelSelected(int index)
    {
        if (!isTargetVisible) return;
        SetActiveModel(index);
    }

    private void SetActiveModel(int activeIndex)
    {
        for (int i = 0; i < models.Length; i++)
        {
            models[i].SetActive(i == activeIndex);
        }
    }
}
