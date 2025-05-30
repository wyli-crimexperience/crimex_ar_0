using UnityEngine;

[System.Serializable]
public class TextColorProfile
{
    [Header("Profile Settings")]
    public string name = "Default";
    public Color color = Color.white;

    [Header("Targeting")]
    public string[] tags = new string[0];

    [Tooltip("Optional: Specific text names to target")]
    public string[] targetNames = new string[0];
}