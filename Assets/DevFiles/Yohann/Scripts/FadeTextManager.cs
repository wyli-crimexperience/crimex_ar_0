using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class FadeProfile
{
    [Header("Fade Settings")]
    public string name = "Default";
    public float duration = 3.0f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public bool fadeIn = false; // true for fade in, false for fade out

    [Header("Timing")]
    public float delay = 0f;
    public bool loop = false;
    public float loopDelay = 0f;

    [Header("Auto Start")]
    public bool autoStart = false;
    public float autoStartDelay = 0f;
}

public class FadeTextManager : MonoBehaviour
{
    [Header("Text Component")]
    [SerializeField] private TextMeshProUGUI targetText;
    [SerializeField] private bool findTextAutomatically = true;

    [Header("Fade Profiles")]
    [SerializeField]
    private List<FadeProfile> fadeProfiles = new List<FadeProfile>
    {
        new FadeProfile { name = "FadeOut", duration = 3.0f, fadeIn = false },
        new FadeProfile { name = "FadeIn", duration = 2.0f, fadeIn = true }
    };

    [SerializeField] private int defaultProfileIndex = 0;

    [Header("Advanced Settings")]
    [SerializeField] private bool unscaledTime = false;
    [SerializeField] private bool disableGameObjectOnComplete = false;
    [SerializeField] private bool resetOnEnable = true;
    [SerializeField] private float minAlpha = 0f;
    [SerializeField] private float maxAlpha = 1f;

    [Header("Performance")]
    [SerializeField] private bool useCoroutines = true;
    [SerializeField] private bool cacheOriginalColor = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    [Header("Events")]
    public UnityEvent OnFadeStarted;
    public UnityEvent OnFadeCompleted;
    public UnityEvent OnFadeCancelled;
    public UnityEvent<float> OnFadeProgress; // 0-1 progress value

    // Private variables
    private Color originalColor;
    private Color currentColor;
    private bool isFading = false;
    private float currentFadeTime = 0f;
    private FadeProfile currentProfile;
    private Coroutine fadeCoroutine;
    private readonly Dictionary<string, FadeProfile> profileLookup = new Dictionary<string, FadeProfile>();

    // Properties
    public bool IsFading => isFading;
    public float FadeProgress => currentProfile != null ? Mathf.Clamp01(currentFadeTime / currentProfile.duration) : 0f;
    public FadeProfile CurrentProfile => currentProfile;

    void Awake()
    {
        InitializeTextComponent();
        InitializeProfileLookup();
    }

    void Start()
    {
        if (cacheOriginalColor && targetText != null)
        {
            originalColor = targetText.color;
            currentColor = originalColor;
        }

        StartAutoFades();
    }

    void OnEnable()
    {
        if (resetOnEnable && targetText != null)
        {
            ResetToOriginalColor();
        }
    }

    void OnDisable()
    {
        StopAllFades();
    }

    private void InitializeTextComponent()
    {
        if (targetText == null && findTextAutomatically)
        {
            targetText = GetComponent<TextMeshProUGUI>();

            if (targetText == null)
            {
                targetText = GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (targetText == null)
        {
            Debug.LogError($"TextMeshProUGUI component not found on {gameObject.name}! Please assign it manually.", this);
            enabled = false;
        }
    }

    private void InitializeProfileLookup()
    {
        profileLookup.Clear();
        foreach (var profile in fadeProfiles)
        {
            if (!string.IsNullOrEmpty(profile.name))
            {
                profileLookup[profile.name] = profile;
            }
        }
    }

    private void StartAutoFades()
    {
        foreach (var profile in fadeProfiles)
        {
            if (profile.autoStart)
            {
                if (profile.autoStartDelay > 0f)
                {
                    StartCoroutine(DelayedAutoStart(profile));
                }
                else
                {
                    StartFade(profile);
                }
                break; // Only start one auto fade
            }
        }
    }

    private IEnumerator DelayedAutoStart(FadeProfile profile)
    {
        yield return new WaitForSeconds(profile.autoStartDelay);
        StartFade(profile);
    }

    void Update()
    {
        // Update-based fading (fallback if not using coroutines)
        if (!useCoroutines && isFading && currentProfile != null)
        {
            UpdateFade();
        }
    }

    private void UpdateFade()
    {
        if (currentProfile == null) return;

        float deltaTime = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        currentFadeTime += deltaTime;

        float normalizedTime = Mathf.Clamp01(currentFadeTime / currentProfile.duration);
        float curveValue = currentProfile.fadeCurve.Evaluate(normalizedTime);

        UpdateTextAlpha(curveValue);
        OnFadeProgress.Invoke(normalizedTime);

        if (normalizedTime >= 1f)
        {
            CompleteFade();
        }
    }

    private void UpdateTextAlpha(float curveValue)
    {
        if (targetText == null) return;

        float targetAlpha;
        if (currentProfile.fadeIn)
        {
            targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, curveValue);
        }
        else
        {
            targetAlpha = Mathf.Lerp(maxAlpha, minAlpha, curveValue);
        }

        currentColor.a = targetAlpha;
        targetText.color = currentColor;
    }

    private IEnumerator FadeCoroutine(FadeProfile profile)
    {
        if (profile.delay > 0f)
        {
            yield return unscaledTime ?
                new WaitForSecondsRealtime(profile.delay) :
                new WaitForSeconds(profile.delay);
        }

        do
        {
            currentFadeTime = 0f;

            while (currentFadeTime < profile.duration)
            {
                float deltaTime = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                currentFadeTime += deltaTime;

                float normalizedTime = Mathf.Clamp01(currentFadeTime / profile.duration);
                float curveValue = profile.fadeCurve.Evaluate(normalizedTime);

                UpdateTextAlpha(curveValue);
                OnFadeProgress.Invoke(normalizedTime);

                yield return null;
            }

            if (profile.loop && profile.loopDelay > 0f)
            {
                yield return unscaledTime ?
                    new WaitForSecondsRealtime(profile.loopDelay) :
                    new WaitForSeconds(profile.loopDelay);
            }

        } while (profile.loop && isFading);

        CompleteFade();
    }

    private void CompleteFade()
    {
        isFading = false;
        fadeCoroutine = null;

        if (disableGameObjectOnComplete)
        {
            gameObject.SetActive(false);
        }

        OnFadeCompleted.Invoke();

        if (enableDebugLogging)
        {
            Debug.Log($"Fade completed on {gameObject.name}");
        }
    }

    // Public Methods
    public void StartFade()
    {
        if (fadeProfiles.Count > 0)
        {
            int index = Mathf.Clamp(defaultProfileIndex, 0, fadeProfiles.Count - 1);
            StartFade(fadeProfiles[index]);
        }
    }

    public void StartFade(string profileName)
    {
        if (profileLookup.TryGetValue(profileName, out FadeProfile profile))
        {
            StartFade(profile);
        }
        else if (enableDebugLogging)
        {
            Debug.LogWarning($"Fade profile '{profileName}' not found on {gameObject.name}");
        }
    }

    public void StartFade(FadeProfile profile)
    {
        if (profile == null || targetText == null) return;

        StopAllFades();

        currentProfile = profile;
        currentColor = targetText.color;
        isFading = true;

        OnFadeStarted.Invoke();

        if (useCoroutines)
        {
            fadeCoroutine = StartCoroutine(FadeCoroutine(profile));
        }

        if (enableDebugLogging)
        {
            Debug.Log($"Started fade '{profile.name}' on {gameObject.name}");
        }
    }

    public void StartFade(int profileIndex)
    {
        if (profileIndex >= 0 && profileIndex < fadeProfiles.Count)
        {
            StartFade(fadeProfiles[profileIndex]);
        }
    }

    public void StopAllFades()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (isFading)
        {
            isFading = false;
            OnFadeCancelled.Invoke();

            if (enableDebugLogging)
            {
                Debug.Log($"Fade cancelled on {gameObject.name}");
            }
        }
    }

    public void ResetToOriginalColor()
    {
        if (targetText != null && cacheOriginalColor)
        {
            StopAllFades();
            targetText.color = originalColor;
            currentColor = originalColor;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (targetText != null)
        {
            currentColor = targetText.color;
            currentColor.a = Mathf.Clamp01(alpha);
            targetText.color = currentColor;
        }
    }

    public void FadeToAlpha(float targetAlpha, float duration = 1f)
    {
        var customProfile = new FadeProfile
        {
            name = "Custom",
            duration = duration,
            fadeIn = targetAlpha > (targetText?.color.a ?? 0f),
            fadeCurve = AnimationCurve.EaseInOut(0f, targetText?.color.a ?? 0f, 1f, targetAlpha)
        };

        StartFade(customProfile);
    }

    public void AddProfile(FadeProfile newProfile)
    {
        if (newProfile != null && !fadeProfiles.Contains(newProfile))
        {
            fadeProfiles.Add(newProfile);
            profileLookup[newProfile.name] = newProfile;
        }
    }

    public bool RemoveProfile(string profileName)
    {
        var profile = fadeProfiles.Find(p => p.name == profileName);
        if (profile != null)
        {
            fadeProfiles.Remove(profile);
            profileLookup.Remove(profileName);
            return true;
        }
        return false;
    }

    // Context menu for easy testing
    [ContextMenu("Start Default Fade")]
    private void ContextStartFade()
    {
        StartFade();
    }

    [ContextMenu("Stop All Fades")]
    private void ContextStopFades()
    {
        StopAllFades();
    }

    [ContextMenu("Reset To Original")]
    private void ContextReset()
    {
        ResetToOriginalColor();
    }
}