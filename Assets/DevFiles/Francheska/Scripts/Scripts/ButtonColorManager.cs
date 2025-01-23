using UnityEngine;
using UnityEngine.UI;

public class UIButtonColorManager : MonoBehaviour
{
    [Header("Button Settings")]
    public Color defaultColor = Color.white;
    public Color hoverColor = Color.gray;
    public Color pressedColor = Color.black;
    public Color selectedColor = Color.white;

    private void Start()
    {

        // Find all Button components in the children of this GameObject
        Button[] buttons = GetComponentsInChildren<Button>();

        if (buttons.Length == 0)
        {
            Debug.LogWarning("No buttons found under " + gameObject.name);
            return;
        }

        foreach (Button button in buttons)
        {
            SetButtonColors(button);
        }
    }

    private void SetButtonColors(Button button)
    {
        // Fetch the Button's ColorBlock
        ColorBlock colorBlock = button.colors;

        // Update the colors
        colorBlock.normalColor = defaultColor;
        colorBlock.highlightedColor = hoverColor;
        colorBlock.pressedColor = pressedColor;
        colorBlock.selectedColor = selectedColor;

        // Apply the modified ColorBlock back to the button
        button.colors = colorBlock;
    }
}
