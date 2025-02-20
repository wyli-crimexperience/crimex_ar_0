using UnityEngine;
using TMPro; // Required for TextMeshPro

public class UITextMeshProColorManager : MonoBehaviour
{
    [Header("Primary Text Color Settings")]
    public Color primaryColor = Color.white;

    [Header("Secondary Text Color Settings")]
    public Color secondaryColor = Color.gray;

    [Header("Find Text Components")]
    public bool findTextInChildren = true; // Toggle for local or global search

    private void Start()
    {
        TextMeshProUGUI[] texts;

        if (findTextInChildren)
        {
            // Find TextMeshProUGUI components in the children of this GameObject (e.g., the Panel)
            texts = GetComponentsInChildren<TextMeshProUGUI>();
        }
        else
        {
            // Find TextMeshProUGUI components globally in the scene
            texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        }

        if (texts.Length == 0)
        {
            Debug.LogWarning("No TextMeshProUGUI components found.");
            return;
        }

        foreach (TextMeshProUGUI text in texts)
        {
            Debug.Log($"Processing Text object: {text.name} (Tag: {text.tag})");
            SetTextColor(text);
        }
    }

    private void SetTextColor(TextMeshProUGUI text)
    {
        if (text.CompareTag("Primary"))
        {
            text.color = primaryColor;
            Debug.Log($"{text.name} set to Primary color.");
        }
        else if (text.CompareTag("Secondary"))
        {
            text.color = secondaryColor;
            Debug.Log($"{text.name} set to Secondary color.");
        }
        else
        {
            text.color = primaryColor; // Default to primary color
            Debug.Log($"{text.name} has no matching tag. Defaulted to Primary color.");
        }
    }
}