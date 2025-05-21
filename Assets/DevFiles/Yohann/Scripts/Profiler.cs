using UnityEngine;
using UnityEngine.InputSystem;

public class ProfilerOverlay : MonoBehaviour
{
    [Header("Settings")]
    public int targetFrameRate = 120;
    public bool disableVSync = true;

    private float deltaTime = 0.0f;
    private bool showOverlay = true;

    private static ProfilerOverlay instance;
    private InputAction toggleOverlayAction;

    void Awake()
    {
        // Singleton pattern
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        Application.targetFrameRate = targetFrameRate;

        if (disableVSync)
            QualitySettings.vSyncCount = 0;

        // Setup new input system toggle action
        toggleOverlayAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/f1");
        toggleOverlayAction.performed += _ => ToggleOverlay();
        toggleOverlayAction.Enable();
    }

    void OnDestroy()
    {
        toggleOverlayAction?.Dispose();
    }

    private void ToggleOverlay()
    {
        showOverlay = !showOverlay;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        if (!showOverlay) return;

        int width = Screen.width, height = Screen.height;

        GUIStyle style = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = height / 40,
            normal = { textColor = Color.white }
        };

        Rect rect = new Rect(10, 10, width, height * 0.1f);

        float fps = 1.0f / deltaTime;
        float memoryUsage = (float)(System.GC.GetTotalMemory(false) / (1024.0 * 1024.0));
        float cpuTimeEstimate = Time.deltaTime * 1000.0f; // Not actual CPU time, but an estimate

        string text = $"FPS: {fps:F1}\nMemory: {memoryUsage:F1} MB\nCPU Time (est): {cpuTimeEstimate:F1} ms";

        GUI.Label(rect, text, style);
    }
}
