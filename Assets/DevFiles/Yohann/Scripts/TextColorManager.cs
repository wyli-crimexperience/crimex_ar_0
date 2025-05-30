using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;


public class UITextMeshProColorManager : MonoBehaviour
{
    [Header("Color Profiles")]
    [SerializeField]
    private List<TextColorProfile> colorProfiles = new List<TextColorProfile>();

    [Header("Search Settings")]
    [SerializeField] private bool findTextInChildren = true;
    [SerializeField] private bool includeInactive = false;
    [SerializeField] private bool autoRefreshOnEnable = true;
    [SerializeField] private float refreshInterval = 0f; // 0 = no auto refresh

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = false;
    [SerializeField] private bool logColorChanges = true;

    [Header("Performance")]
    [SerializeField] private bool cacheTextComponents = true;
    [SerializeField] private int maxTextComponents = 100;

    // Cached components for better performance
    private readonly Dictionary<string, TextColorProfile> profileLookup = new Dictionary<string, TextColorProfile>();
    private readonly List<TextMeshProUGUI> cachedTextComponents = new List<TextMeshProUGUI>();
    private readonly Dictionary<TextMeshProUGUI, Color> originalColors = new Dictionary<TextMeshProUGUI, Color>();

    private Coroutine refreshCoroutine;
    private bool isInitialized = false;

    void Awake()
    {
        // Initialize with default profiles if empty
        if (colorProfiles.Count == 0)
        {
            InitializeDefaultProfiles();
        }
        InitializeProfileLookup();
    }

    private void InitializeDefaultProfiles()
    {
        colorProfiles.Add(new TextColorProfile
        {
            name = "Primary",
            color = Color.white,
            tags = new[] { "Primary" }
        });

        colorProfiles.Add(new TextColorProfile
        {
            name = "Secondary",
            color = Color.gray,
            tags = new[] { "Secondary" }
        });
    }

    void Start()
    {
        InitializeColorManager();
    }

    void OnEnable()
    {
        if (autoRefreshOnEnable && isInitialized)
        {
            RefreshTextColors();
        }

        StartAutoRefresh();
    }

    void OnDisable()
    {
        StopAutoRefresh();
    }

    private void InitializeProfileLookup()
    {
        profileLookup.Clear();

        foreach (var profile in colorProfiles)
        {
            if (profile.tags != null)
            {
                foreach (string tag in profile.tags)
                {
                    if (!string.IsNullOrEmpty(tag))
                    {
                        profileLookup[tag] = profile;
                    }
                }
            }
        }
    }

    private void InitializeColorManager()
    {
        RefreshTextComponents();
        ApplyColorsToAllText();
        isInitialized = true;

        if (enableDebugLogging)
        {
            Debug.Log($"UITextMeshProColorManager initialized with {cachedTextComponents.Count} text components.");
        }
    }

    private void RefreshTextComponents()
    {
        cachedTextComponents.Clear();
        originalColors.Clear();

        TextMeshProUGUI[] foundTexts = FindTextComponents();

        if (foundTexts.Length == 0)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning("No TextMeshProUGUI components found.");
            }
            return;
        }

        // Limit the number of components to prevent performance issues
        int componentsToAdd = Mathf.Min(foundTexts.Length, maxTextComponents);

        for (int i = 0; i < componentsToAdd; i++)
        {
            var textComponent = foundTexts[i];
            if (textComponent != null)
            {
                cachedTextComponents.Add(textComponent);
                // Store original color for potential restoration
                originalColors[textComponent] = textComponent.color;
            }
        }

        if (enableDebugLogging)
        {
            Debug.Log($"Cached {cachedTextComponents.Count} TextMeshProUGUI components.");
        }
    }

    private TextMeshProUGUI[] FindTextComponents()
    {
        if (findTextInChildren)
        {
            return GetComponentsInChildren<TextMeshProUGUI>(includeInactive);
        }
        else
        {
            return FindObjectsByType<TextMeshProUGUI>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
        }
    }

    private void ApplyColorsToAllText()
    {
        int processedCount = 0;
        int changedCount = 0;

        foreach (var textComponent in cachedTextComponents)
        {
            if (textComponent == null) continue;

            processedCount++;
            if (SetTextColor(textComponent))
            {
                changedCount++;
            }
        }

        if (enableDebugLogging)
        {
            Debug.Log($"Processed {processedCount} text components, changed {changedCount} colors.");
        }
    }

    private bool SetTextColor(TextMeshProUGUI textComponent)
    {
        if (textComponent == null) return false;

        TextColorProfile matchedProfile = FindMatchingProfile(textComponent);

        if (matchedProfile != null)
        {
            Color newColor = matchedProfile.color;
            if (textComponent.color != newColor)
            {
                textComponent.color = newColor;

                if (logColorChanges && enableDebugLogging)
                {
                    Debug.Log($"'{textComponent.name}' color changed to {matchedProfile.name} ({newColor})");
                }

                return true;
            }
        }
        else
        {
            // Apply default color (first profile or white)
            Color defaultColor = colorProfiles.Count > 0 ? colorProfiles[0].color : Color.white;
            if (textComponent.color != defaultColor)
            {
                textComponent.color = defaultColor;

                if (logColorChanges && enableDebugLogging)
                {
                    Debug.Log($"'{textComponent.name}' set to default color ({defaultColor})");
                }

                return true;
            }
        }

        return false;
    }

    private TextColorProfile FindMatchingProfile(TextMeshProUGUI textComponent)
    {
        // First, check by tag
        if (profileLookup.TryGetValue(textComponent.tag, out TextColorProfile profileByTag))
        {
            return profileByTag;
        }

        // Then, check by name
        foreach (var profile in colorProfiles)
        {
            if (profile.targetNames != null)
            {
                foreach (string targetName in profile.targetNames)
                {
                    if (!string.IsNullOrEmpty(targetName) &&
                        textComponent.name.Contains(targetName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return profile;
                    }
                }
            }
        }

        return null;
    }

    private void StartAutoRefresh()
    {
        if (refreshInterval > 0f)
        {
            StopAutoRefresh();
            refreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
        }
    }

    private void StopAutoRefresh()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    private IEnumerator AutoRefreshCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);
            RefreshTextColors();
        }
    }

    // Public Methods
    public void RefreshTextColors()
    {
        if (cacheTextComponents)
        {
            RefreshTextComponents();
        }
        ApplyColorsToAllText();
    }

    public void AddColorProfile(TextColorProfile newProfile)
    {
        if (newProfile != null && !colorProfiles.Contains(newProfile))
        {
            colorProfiles.Add(newProfile);
            InitializeProfileLookup();

            if (enableDebugLogging)
            {
                Debug.Log($"Added color profile: {newProfile.name}");
            }
        }
    }

    public void RemoveColorProfile(string profileName)
    {
        int removed = colorProfiles.RemoveAll(p => p.name == profileName);
        if (removed > 0)
        {
            InitializeProfileLookup();

            if (enableDebugLogging)
            {
                Debug.Log($"Removed {removed} color profile(s) named: {profileName}");
            }
        }
    }

    public void SetProfileColor(string profileName, Color newColor)
    {
        var profile = colorProfiles.Find(p => p.name == profileName);
        if (profile != null)
        {
            profile.color = newColor;
            RefreshTextColors();

            if (enableDebugLogging)
            {
                Debug.Log($"Updated {profileName} color to {newColor}");
            }
        }
    }

    public void RestoreOriginalColors()
    {
        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null)
            {
                kvp.Key.color = kvp.Value;
            }
        }

        if (enableDebugLogging)
        {
            Debug.Log("Restored original colors for all text components.");
        }
    }

    public int GetManagedTextCount()
    {
        return cachedTextComponents.Count;
    }

    public bool IsTextManaged(TextMeshProUGUI textComponent)
    {
        return cachedTextComponents.Contains(textComponent);
    }

    // Context menu for easy testing in editor
    [ContextMenu("Refresh Text Colors")]
    private void ContextRefreshColors()
    {
        RefreshTextColors();
    }

    [ContextMenu("Restore Original Colors")]
    private void ContextRestoreColors()
    {
        RestoreOriginalColors();
    }
}