using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ProfilerSettings
{
    [Header("Performance")]
    public int targetFrameRate = 120; // More realistic for Android
    public bool disableVSync = true; // Usually better enabled on mobile
    public bool enableProfiler = false;

    [Header("Display - Optimized for Mobile")]
    public bool showFPS = true;
    public bool showMemory = true;
    public bool showCPU = true;
    public bool showGPU = false; // Disabled for Android
    public bool showBatches = false; // Disabled for Android
    public bool showTriangles = false; // Disabled for Android

    [Header("Overlay Style")]
    public Color backgroundColor = new Color(0, 0, 0, 0.7f);
    public Color textColor = Color.white;
    public Color warningColor = Color.yellow;
    public Color criticalColor = Color.red;
    public int fontSize = 18; // Larger for mobile readability
    public TextAnchor textAlignment = TextAnchor.UpperLeft;

    [Header("Thresholds")]
    public float lowFPSWarning = 25f; // Lower threshold for mobile
    public float criticalFPSWarning = 15f;
    public float highMemoryWarning = 256f; // MB - Lower for mobile
    public float criticalMemoryWarning = 512f; // MB - Lower for mobile
}
[System.Serializable]
public class PerformanceMetrics
{
    public float currentFPS;
    public float averageFPS;
    public float minFPS;
    public float maxFPS;

    public float totalMemoryMB;
    public float usedMemoryMB;
    public float reservedMemoryMB;
    public float monoMemoryMB;

    public float cpuTimeMs;
    public float gpuTimeMs;

    public int batches;
    public int triangles;
    public int vertices;
    public int drawCalls;

    public void Reset()
    {
        minFPS = float.MaxValue;
        maxFPS = 0f;
    }
}