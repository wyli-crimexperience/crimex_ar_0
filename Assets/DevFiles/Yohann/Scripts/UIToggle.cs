using TMPro;
using UnityEngine;

public class InteractionModeToggle : MonoBehaviour
{
    public TMP_Text modeLabel; // Assign in inspector to display current mode

    private void Start()
    {
        UpdateLabel();
    }

    public void ToggleMode()
    {
        InteractionState.CurrentMode = InteractionState.CurrentMode == InteractionMode.Transform
            ? InteractionMode.Description
            : InteractionMode.Transform;

        Debug.Log($"[InteractionModeToggle] Mode switched to: {InteractionState.CurrentMode}");
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (modeLabel != null)
            modeLabel.text = $"Mode: {InteractionState.CurrentMode}";
    }
}
