using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;


public class ProfilerOverlay : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private ProfilerSettings settings = new ProfilerSettings();

    [Header("UI References (Optional)")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private TextMeshProUGUI overlayText;
    [SerializeField] private Image backgroundImage;

    [Header("Input")]
    [SerializeField] private string toggleKey = "<Keyboard>/f1";
    [SerializeField] private string resetStatsKey = "<Keyboard>/f2";

    [Header("Mobile Input")]
    [SerializeField] private bool enableTouchToggle = true;
    [SerializeField] private int touchCount = 3; // Triple tap to toggle
    [SerializeField] private bool enableShakeToReset = true;
    [SerializeField] private float shakeThreshold = 2.0f;

    // Private fields
    private static ProfilerOverlay instance;
    private InputAction toggleOverlayAction;
    private InputAction resetStatsAction;

    private PerformanceMetrics metrics = new PerformanceMetrics();
    private bool showOverlay = true;
    private bool isInitialized = true;

    // Performance tracking
    private float deltaTime = 0.0f;
    private readonly Queue<float> fpsHistory = new Queue<float>(30); // Reduced for mobile
    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.25f; // Slower updates for mobile (250ms)

    // Mobile input tracking
    private float lastTouchTime;
    private int currentTouchCount;
    private Vector3 lastAcceleration;

    // UI Style caching
    private GUIStyle cachedStyle;
    private bool styleNeedsUpdate = true;

    // Properties
    public static ProfilerOverlay Instance => instance;
    public PerformanceMetrics CurrentMetrics => metrics;
    public bool IsVisible => showOverlay;

    void Awake()
    {
        InitializeSingleton();
        ApplyPerformanceSettings();
        SetupInputActions();
        InitializeUI();
    }

    void Start()
    {
        InitializeProfiler();
        metrics.Reset();
        lastAcceleration = Input.acceleration;
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        UpdateDeltaTime();
        UpdateMetrics();
        HandleMobileInput();

        if (showOverlay && overlayText != null)
        {
            UpdateUIText();
        }
    }

    void OnGUI()
    {
        if (!showOverlay || overlayText != null) return; // Skip OnGUI if using UI Text

        DrawOverlayGUI();
    }

    void OnDestroy()
    {
        CleanupInputActions();
    }

    #region Initialization

    private void InitializeSingleton()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void ApplyPerformanceSettings()
    {
        // Android-specific frame rate handling
        Application.targetFrameRate = settings.targetFrameRate;

        // VSync handling for Android
        if (settings.disableVSync)
            QualitySettings.vSyncCount = 0;
        else
            QualitySettings.vSyncCount = 1; // Usually better for mobile

        // Enable profiler only in development builds on Android
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (settings.enableProfiler)
            Profiler.enabled = true;
#endif
    }

    private void SetupInputActions()
    {
        // Only setup keyboard input if not on mobile or in editor
#if UNITY_EDITOR || UNITY_STANDALONE
        // Toggle overlay
        toggleOverlayAction = new InputAction(type: InputActionType.Button, binding: toggleKey);
        toggleOverlayAction.performed += _ => ToggleOverlay();
        toggleOverlayAction.Enable();

        // Reset stats
        resetStatsAction = new InputAction(type: InputActionType.Button, binding: resetStatsKey);
        resetStatsAction.performed += _ => ResetStats();
        resetStatsAction.Enable();
#endif
    }

    private void InitializeUI()
    {
        if (overlayCanvas == null)
        {
            CreateUIOverlay();
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = settings.backgroundColor;
        }

        // Ensure the overlay is visible by default
        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(showOverlay);
        }
    }

    private void CreateUIOverlay()
    {
        // Create Canvas with Android-friendly settings
        GameObject canvasObj = new GameObject("ProfilerOverlay_Canvas");
        canvasObj.transform.SetParent(transform);

        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 1000;

        // Android-optimized canvas scaler
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        //canvasObj.AddComponent<GraphicRaycaster>();

        CreateMobileOptimizedUI(canvasObj);
    }

    private void CreateMobileOptimizedUI(GameObject canvasObj)
    {
        // Background sized for mobile
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);

        backgroundImage = bgObj.AddComponent<Image>();
        backgroundImage.color = settings.backgroundColor;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 1);
        bgRect.anchorMax = new Vector2(0, 1);
        bgRect.pivot = new Vector2(0, 1);
        bgRect.anchoredPosition = new Vector2(20, -20); // More padding for mobile
        bgRect.sizeDelta = new Vector2(400, 350); // Larger for mobile readability

        // Text with mobile-friendly settings
        GameObject textObj = new GameObject("OverlayText");
        textObj.transform.SetParent(bgObj.transform, false);

        overlayText = textObj.AddComponent<TextMeshProUGUI>();
        overlayText.text = "Profiler Loading...";
        overlayText.fontSize = settings.fontSize;
        overlayText.color = settings.textColor;
        overlayText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10); // More padding
        textRect.offsetMax = new Vector2(-10, -10);
    }

    private void InitializeProfiler()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (settings.enableProfiler)
        {
            Profiler.BeginSample("ProfilerOverlay.Initialize");
            Profiler.EndSample();
        }
#endif
    }

    #endregion

    #region Performance Tracking

    private void UpdateDeltaTime()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    private void UpdateMetrics()
    {
        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer < UPDATE_INTERVAL) return;

        updateTimer = 0f;

        // FPS Metrics
        UpdateFPSMetrics();

        // Memory Metrics
        if (settings.showMemory)
            UpdateMemoryMetrics();

        // Rendering Metrics (simplified for Android)
        if (settings.showBatches || settings.showTriangles)
            UpdateRenderingMetrics();

        // Performance Metrics
        if (settings.showCPU || settings.showGPU)
            UpdatePerformanceMetrics();
    }

    private void UpdateFPSMetrics()
    {
        metrics.currentFPS = 1.0f / deltaTime;

        // Track FPS history
        fpsHistory.Enqueue(metrics.currentFPS);
        if (fpsHistory.Count > 30) // Reduced from 60 for mobile
            fpsHistory.Dequeue();

        // Calculate statistics
        if (fpsHistory.Count > 0)
        {
            metrics.averageFPS = fpsHistory.Average();
            metrics.minFPS = Mathf.Min(metrics.minFPS, metrics.currentFPS);
            metrics.maxFPS = Mathf.Max(metrics.maxFPS, metrics.currentFPS);
        }
    }

    private void UpdateMemoryMetrics()
    {
        // Android-compatible memory tracking
        metrics.totalMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
        metrics.reservedMemoryMB = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);
        metrics.monoMemoryMB = (float)(System.GC.GetTotalMemory(false) / (1024.0 * 1024.0));

        // For Android, graphics memory is harder to track accurately
        // This is an approximation
        metrics.usedMemoryMB = metrics.totalMemoryMB - metrics.monoMemoryMB;
    }

    private void UpdateRenderingMetrics()
    {
        // On Android builds, most rendering stats aren't directly accessible
        // Remove FrameDebugger entirely for mobile
        metrics.batches = 0;
        metrics.triangles = 0;
        metrics.vertices = 0;
        metrics.drawCalls = 0;

        // Note: Consider implementing custom counters in your rendering code
        // if you need these metrics on Android
    }

    private void UpdatePerformanceMetrics()
    {
        metrics.cpuTimeMs = Time.deltaTime * 1000.0f;

        // GPU time is not easily accessible on Android
        metrics.gpuTimeMs = 0f;
    }

    #endregion

    #region Mobile Input Handling

    private void HandleMobileInput()
    {
        HandleTouchInput();

        if (enableShakeToReset)
            HandleShakeInput();
    }

    private void HandleTouchInput()
    {
        if (!enableTouchToggle) return;

        // Use the old Input system for touch (more reliable on mobile)
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
        {
            float timeSinceLastTouch = Time.time - lastTouchTime;

            if (timeSinceLastTouch < 0.5f) // Within 500ms
            {
                currentTouchCount++;
            }
            else
            {
                currentTouchCount = 1;
            }

            lastTouchTime = Time.time;

            if (currentTouchCount >= touchCount)
            {
                ToggleOverlay();
                currentTouchCount = 0;
            }
        }
    }

    private void HandleShakeInput()
    {
        Vector3 acceleration = Input.acceleration;
        Vector3 deltaAcceleration = acceleration - lastAcceleration;
        lastAcceleration = acceleration;

        float shakeDetected = deltaAcceleration.sqrMagnitude;

        if (shakeDetected > shakeThreshold)
        {
            ResetStats();
        }
    }

    #endregion
    #region UI Updates

    private void UpdateUIText()
    {
        if (overlayText == null) return;

        overlayText.text = GenerateDisplayText();

        // Update text color based on performance
        Color textColor = GetPerformanceColor();
        if (overlayText.color != textColor)
        {
            overlayText.color = textColor;
        }
    }

    private void DrawOverlayGUI()
    {
        if (styleNeedsUpdate)
        {
            UpdateGUIStyle();
            styleNeedsUpdate = false;
        }

        int width = Screen.width;
        int height = Screen.height;

        Rect backgroundRect = new Rect(10, 10, 400, 350);
        GUI.color = settings.backgroundColor;
        GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect textRect = new Rect(20, 20, 380, 330);
        GUI.Label(textRect, GenerateDisplayText(), cachedStyle);
    }

    private void UpdateGUIStyle()
    {
        int height = Screen.height;
        cachedStyle = new GUIStyle
        {
            alignment = settings.textAlignment,
            fontSize = Mathf.Max(14, height / 50), // Larger minimum for mobile
            normal = { textColor = GetPerformanceColor() },
            wordWrap = true
        };
    }

    private string GenerateDisplayText()
    {
        var text = new System.Text.StringBuilder();

        if (settings.showFPS)
        {
            text.AppendLine($"FPS: {metrics.currentFPS:F1}");
            text.AppendLine($"Avg: {metrics.averageFPS:F1} | Target: {settings.targetFrameRate}");
            text.AppendLine($"Min: {metrics.minFPS:F1} | Max: {metrics.maxFPS:F1}");
        }

        if (settings.showMemory)
        {
            text.AppendLine($"\nTotal Memory: {metrics.totalMemoryMB:F1} MB");
            text.AppendLine($"Mono Memory: {metrics.monoMemoryMB:F1} MB");
            text.AppendLine($"Reserved: {metrics.reservedMemoryMB:F1} MB");
        }

        if (settings.showCPU)
        {
            text.AppendLine($"\nFrame Time: {metrics.cpuTimeMs:F1} ms");
        }

        // Device information for Android
        text.AppendLine($"\nDevice: {SystemInfo.deviceModel}");
        text.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
        text.AppendLine($"RAM: {SystemInfo.systemMemorySize} MB");
        text.AppendLine($"OS: {SystemInfo.operatingSystem}");

        // Mobile-specific controls
        text.AppendLine($"\n{touchCount}x Tap: Toggle Overlay");
        if (enableShakeToReset)
            text.AppendLine("Shake: Reset Stats");

        return text.ToString();
    }

    private Color GetPerformanceColor()
    {
        if (metrics.currentFPS < settings.criticalFPSWarning ||
            metrics.totalMemoryMB > settings.criticalMemoryWarning)
        {
            return settings.criticalColor;
        }

        if (metrics.currentFPS < settings.lowFPSWarning ||
            metrics.totalMemoryMB > settings.highMemoryWarning)
        {
            return settings.warningColor;
        }

        return settings.textColor;
    }

    #endregion

    #region Public Methods

    public void ToggleOverlay()
    {
        showOverlay = !showOverlay;

        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(showOverlay);
        }

        Debug.Log($"Profiler Overlay: {(showOverlay ? "Enabled" : "Disabled")}");
    }

    public void ResetStats()
    {
        metrics.Reset();
        fpsHistory.Clear();
        Debug.Log("Profiler stats reset.");
    }

    public void SetTargetFrameRate(int fps)
    {
        settings.targetFrameRate = fps;
        Application.targetFrameRate = fps;
    }

    public void UpdateSettings(ProfilerSettings newSettings)
    {
        settings = newSettings;
        styleNeedsUpdate = true;
        ApplyPerformanceSettings();

        if (backgroundImage != null)
        {
            backgroundImage.color = settings.backgroundColor;
        }
    }

    #endregion

    #region Cleanup

    private void CleanupInputActions()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        toggleOverlayAction?.Dispose();
        resetStatsAction?.Dispose();
#endif
    }

    #endregion

    #region Context Menu

    [ContextMenu("Toggle Overlay")]
    private void ContextToggleOverlay()
    {
        ToggleOverlay();
    }

    [ContextMenu("Reset Stats")]
    private void ContextResetStats()
    {
        ResetStats();
    }

    [ContextMenu("Log Current Metrics")]
    private void ContextLogMetrics()
    {
        Debug.Log($"Current Metrics:\n{GenerateDisplayText()}");
    }

    #endregion
}