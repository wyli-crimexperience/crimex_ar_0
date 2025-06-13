using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

[System.Serializable]
public class LoggerSettings
{
    [Header("Logging Configuration")]
    public bool enableLogging = true;
    public bool enableDebugLogs = true;
    public bool enableOfflineCache = true;
    public int maxCacheSize = 100;
    public float retryInterval = 30f;
    public int maxRetryAttempts = 3;

    [Header("Performance")]
    public float batchUploadInterval = 10f;
    public int maxBatchSize = 10;
    public bool enableBatching = true;

    [Header("Data Collection")]
    public bool logDeviceInfo = true;
    public bool logPerformanceMetrics = true;
    public bool logUserInteractions = true;
    public bool logSceneTransitions = true;
}

[System.Serializable]
public class LogEntry
{
    public string eventType;
    public string userId;
    public DateTime timestamp;
    public Dictionary<string, object> data;
    public int retryCount;
    public string documentId;

    public LogEntry(string eventType, string userId, Dictionary<string, object> extraData = null)
    {
        this.eventType = eventType;
        this.userId = userId;
        this.timestamp = DateTime.UtcNow;
        this.data = extraData ?? new Dictionary<string, object>();
        this.retryCount = 0;

        string timestampKey = timestamp.ToString("yyyyMMdd_HHmmss_fff");
        this.documentId = $"{timestampKey}_{eventType}_{UnityEngine.Random.Range(1000, 9999)}";
    }

    public Dictionary<string, object> ToFirestoreData()
    {
        var firestoreData = new Dictionary<string, object>
        {
            { "eventType", eventType },
            { "userId", userId },
            { "timestamp", Timestamp.FromDateTime(timestamp.ToUniversalTime()) },
            { "deviceInfo", GetDeviceInfo() }
        };

        foreach (var pair in data)
        {
            firestoreData[pair.Key] = pair.Value;
        }

        return firestoreData;
    }

    private Dictionary<string, object> GetDeviceInfo()
    {
        return new Dictionary<string, object>
        {
            { "deviceModel", SystemInfo.deviceModel },
            { "operatingSystem", SystemInfo.operatingSystem },
            { "platform", Application.platform.ToString() },
            { "appVersion", Application.version },
            { "unityVersion", Application.unityVersion }
        };
    }
}

public class LoggerManager : MonoBehaviour
{
    public static LoggerManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private LoggerSettings settings = new LoggerSettings();

    // Firebase references
    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private bool isFirebaseInitialized;

    // Caching and batching
    private Queue<LogEntry> offlineCache = new Queue<LogEntry>();
    private List<LogEntry> currentBatch = new List<LogEntry>();
    private Coroutine batchUploadCoroutine;
    private Coroutine retryCoroutine;

    // Performance tracking
    private float sessionStartTime;
    private int totalLogsAttempted;
    private int totalLogsSuccessful;
    private int totalLogsFailed;

    // Properties
    public LoggerSettings Settings => settings;
    public bool IsOnline => Application.internetReachability != NetworkReachability.NotReachable;
    public int CachedLogsCount => offlineCache.Count;
    public float SuccessRate => totalLogsAttempted > 0 ? (float)totalLogsSuccessful / totalLogsAttempted : 0f;

    // Events
    public System.Action<LogEntry> OnLogSuccess;
    public System.Action<LogEntry, string> OnLogFailure;
    public System.Action<int> OnCacheUpdated;

    private void Awake()
    {
        InitializeSingleton();
        InitializeFirebase();
        InitializeSession();
        SetupEventListeners();
    }

    private void Start()
    {
        if (settings.enableBatching)
        {
            StartBatchUpload();
        }

        LogAppStarted();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            LogEvent("App Paused");
            FlushAllLogs(); // Ensure logs are sent before app goes to background
        }
        else
        {
            LogEvent("App Resumed");
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            LogEvent("App Lost Focus");
        }
        else
        {
            LogEvent("App Gained Focus");
        }
    }

    private void OnApplicationQuit()
    {
        LogAppQuit();
        FlushAllLogs();
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
        StopAllCoroutines();
    }

    #region Initialization

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void InitializeFirebase()
    {
        try
        {
            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;
            isFirebaseInitialized = true;

            if (settings.enableDebugLogs)
                Debug.Log("[LoggerManager] Firebase initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoggerManager] Firebase initialization failed: {e.Message}");
            isFirebaseInitialized = false;
        }
    }

    private void InitializeSession()
    {
        sessionStartTime = Time.time;
        totalLogsAttempted = 0;
        totalLogsSuccessful = 0;
        totalLogsFailed = 0;
    }

    private void SetupEventListeners()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        Application.lowMemory += OnLowMemory;
    }

    private void CleanupEventListeners()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Application.lowMemory -= OnLowMemory;
    }

    #endregion

    #region Event Listeners

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!settings.logSceneTransitions) return;

        LogSceneLoaded(scene.name, mode.ToString());

        if (scene.name.ToLower().Contains("ar"))
        {
            LogARSceneOpened(scene.name);
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (!settings.logSceneTransitions) return;

        LogEvent("Scene Unloaded", new Dictionary<string, object>
        {
            { "sceneName", scene.name },
            { "timeInScene", Time.time - sessionStartTime }
        });
    }

    private void OnLowMemory()
    {
        LogEvent("Low Memory Warning", new Dictionary<string, object>
        {
            { "availableMemory", SystemInfo.systemMemorySize },
            { "usedMemory", UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024) }
        });
    }

    #endregion

    #region Core Logging Methods

    public void LogEvent(string eventType, Dictionary<string, object> extraData = null)
    {
        if (!settings.enableLogging) return;

        try
        {
            string userId = GetCurrentUserId();

            // Don't log events if there's no authenticated user
            if (string.IsNullOrEmpty(userId))
            {
                if (settings.enableDebugLogs)
                    Debug.Log($"[LoggerManager] Skipping log '{eventType}' - no authenticated user");
                return;
            }

            var logEntry = new LogEntry(eventType, userId, extraData);

            totalLogsAttempted++;

            if (settings.enableBatching)
            {
                AddToBatch(logEntry);
            }
            else
            {
                SendLogEntry(logEntry);
            }

            if (settings.enableDebugLogs)
                Debug.Log($"[LoggerManager] Queued log: {eventType}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoggerManager] Error creating log entry: {e.Message}");
        }
    }

    private void SendLogEntry(LogEntry logEntry)
    {
        if (!isFirebaseInitialized || !IsOnline)
        {
            CacheLogEntry(logEntry);
            return;
        }

        var firestoreData = logEntry.ToFirestoreData();

        db.Collection("users")
          .Document(logEntry.userId)
          .Collection("LogsAR")
          .Document(logEntry.documentId)
          .SetAsync(firestoreData)
          .ContinueWithOnMainThread(task =>
          {
              if (task.IsCompletedSuccessfully)
              {
                  totalLogsSuccessful++;
                  OnLogSuccess?.Invoke(logEntry);

                  if (settings.enableDebugLogs)
                      Debug.Log($"[LoggerManager] Successfully logged: {logEntry.documentId}");
              }
              else
              {
                  totalLogsFailed++;
                  HandleLogFailure(logEntry, task.Exception?.Message ?? "Unknown error");
              }
          });
    }

    private void HandleLogFailure(LogEntry logEntry, string error)
    {
        OnLogFailure?.Invoke(logEntry, error);

        if (logEntry.retryCount < settings.maxRetryAttempts)
        {
            logEntry.retryCount++;
            CacheLogEntry(logEntry);

            if (settings.enableDebugLogs)
                Debug.LogWarning($"[LoggerManager] Log failed, cached for retry: {logEntry.documentId} (Attempt {logEntry.retryCount})");
        }
        else
        {
            Debug.LogError($"[LoggerManager] Log permanently failed after {settings.maxRetryAttempts} attempts: {error}");
        }
    }

    #endregion

    #region Caching and Batching

    private void CacheLogEntry(LogEntry logEntry)
    {
        if (!settings.enableOfflineCache) return;

        if (offlineCache.Count >= settings.maxCacheSize)
        {
            var oldestEntry = offlineCache.Dequeue();
            if (settings.enableDebugLogs)
                Debug.LogWarning($"[LoggerManager] Cache full, dropped oldest entry: {oldestEntry.documentId}");
        }

        offlineCache.Enqueue(logEntry);
        OnCacheUpdated?.Invoke(offlineCache.Count);

        // Start retry coroutine if not already running
        if (retryCoroutine == null)
        {
            retryCoroutine = StartCoroutine(RetryFailedLogs());
        }
    }

    private void AddToBatch(LogEntry logEntry)
    {
        currentBatch.Add(logEntry);

        if (currentBatch.Count >= settings.maxBatchSize)
        {
            ProcessCurrentBatch();
        }
    }

    private void ProcessCurrentBatch()
    {
        if (currentBatch.Count == 0) return;

        var batchToProcess = new List<LogEntry>(currentBatch);
        currentBatch.Clear();

        if (!isFirebaseInitialized || !IsOnline)
        {
            foreach (var entry in batchToProcess)
            {
                CacheLogEntry(entry);
            }
            return;
        }

        StartCoroutine(SendBatch(batchToProcess));
    }

    private IEnumerator SendBatch(List<LogEntry> batch)
    {
        var writeBatch = db.StartBatch();

        foreach (var logEntry in batch)
        {
            try
            {
                var docRef = db.Collection("users")
                             .Document(logEntry.userId)
                             .Collection("LogsAR")
                             .Document(logEntry.documentId);

                writeBatch.Set(docRef, logEntry.ToFirestoreData());
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoggerManager] Error preparing batch entry: {e.Message}");
                CacheLogEntry(logEntry);
            }
        }

        var commitTask = writeBatch.CommitAsync();

        while (!commitTask.IsCompleted)
        {
            yield return null;
        }

        if (commitTask.IsCompletedSuccessfully)
        {
            totalLogsSuccessful += batch.Count;

            if (settings.enableDebugLogs)
                Debug.Log($"[LoggerManager] Successfully sent batch of {batch.Count} logs");
        }
        else
        {
            totalLogsFailed += batch.Count;

            foreach (var entry in batch)
            {
                HandleLogFailure(entry, commitTask.Exception?.Message ?? "Batch commit failed");
            }
        }
    }

    private void StartBatchUpload()
    {
        if (batchUploadCoroutine != null)
        {
            StopCoroutine(batchUploadCoroutine);
        }

        batchUploadCoroutine = StartCoroutine(BatchUploadCoroutine());
    }

    private IEnumerator BatchUploadCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(settings.batchUploadInterval);

            if (currentBatch.Count > 0)
            {
                ProcessCurrentBatch();
            }
        }
    }

    private IEnumerator RetryFailedLogs()
    {
        while (offlineCache.Count > 0)
        {
            yield return new WaitForSeconds(settings.retryInterval);

            if (!IsOnline || !isFirebaseInitialized)
                continue;

            var logsToRetry = new List<LogEntry>();
            int retryCount = Mathf.Min(offlineCache.Count, settings.maxBatchSize);

            for (int i = 0; i < retryCount; i++)
            {
                if (offlineCache.Count > 0)
                {
                    logsToRetry.Add(offlineCache.Dequeue());
                }
            }

            if (logsToRetry.Count > 0)
            {
                StartCoroutine(SendBatch(logsToRetry));
                OnCacheUpdated?.Invoke(offlineCache.Count);
            }
        }

        retryCoroutine = null;
    }

    #endregion

    #region Utility Methods

    private string GetCurrentUserId()
    {
        if (auth?.CurrentUser != null)
        {
            return auth.CurrentUser.UserId;
        }

        // No authenticated user - return empty string
        // This prevents logging for unauthenticated users
        return string.Empty;
    }

    public void FlushAllLogs()
    {
        // Process current batch immediately
        if (currentBatch.Count > 0)
        {
            ProcessCurrentBatch();
        }

        // Force retry all cached logs
        if (offlineCache.Count > 0 && IsOnline && isFirebaseInitialized)
        {
            var allCachedLogs = offlineCache.ToList();
            offlineCache.Clear();
            StartCoroutine(SendBatch(allCachedLogs));
        }
    }

    public Dictionary<string, object> GetSessionStats()
    {
        return new Dictionary<string, object>
        {
            { "sessionDuration", Time.time - sessionStartTime },
            { "totalLogsAttempted", totalLogsAttempted },
            { "totalLogsSuccessful", totalLogsSuccessful },
            { "totalLogsFailed", totalLogsFailed },
            { "successRate", SuccessRate },
            { "cachedLogs", offlineCache.Count },
            { "isOnline", IsOnline },
            { "firebaseInitialized", isFirebaseInitialized }
        };
    }

    #endregion

    #region Specific Log Methods

    public void LogAppStarted()
    {
        var data = new Dictionary<string, object>
        {
            { "sessionId", System.Guid.NewGuid().ToString() },
            { "startTime", DateTime.UtcNow }
        };

        if (settings.logDeviceInfo)
        {
            data.Add("deviceModel", SystemInfo.deviceModel);
            data.Add("platform", Application.platform.ToString());
            data.Add("operatingSystem", SystemInfo.operatingSystem);
            data.Add("processorType", SystemInfo.processorType);
            data.Add("systemMemorySize", SystemInfo.systemMemorySize);
            data.Add("graphicsDeviceName", SystemInfo.graphicsDeviceName);
        }

        LogEvent("App Started", data);
    }

    public void LogAppQuit()
    {
        var sessionStats = GetSessionStats();
        sessionStats.Add("endTime", DateTime.UtcNow);
        LogEvent("App Quit", sessionStats);
    }

    public void LogARSceneOpened(string sceneName)
    {
        LogEvent("AR Scene Opened", new Dictionary<string, object>
        {
            { "sceneName", sceneName },
            { "loadTime", Time.time }
        });
    }

    public void LogSceneLoaded(string sceneName, string loadMode = "Single")
    {
        LogEvent("Scene Loaded", new Dictionary<string, object>
        {
            { "sceneName", sceneName },
            { "loadMode", loadMode },
            { "loadTime", Time.time }
        });
    }

    public void LogUserLogin(string loginMethod = "Firebase")
    {
        LogEvent("User Login", new Dictionary<string, object>
        {
            { "loginMethod", loginMethod },
            { "loginTime", DateTime.UtcNow }
        });
    }

    public void LogUserLogout()
    {
        LogEvent("User Logout", new Dictionary<string, object>
        {
            { "sessionDuration", Time.time - sessionStartTime }
        });
    }

    public void LogUserSignup(string userType, string signupMethod = "Firebase")
    {
        LogEvent("User Signup", new Dictionary<string, object>
        {
            { "userType", userType },
            { "signupMethod", signupMethod },
            { "signupTime", DateTime.UtcNow }
        });
    }

    public void LogToolInteraction(string toolName, string interactionType = "Used")
    {
        LogEvent("Tool Interaction", new Dictionary<string, object>
        {
            { "tool", toolName },
            { "interactionType", interactionType },
            { "sessionTime", Time.time - sessionStartTime }
        });
    }

    public void LogModelSelected(string modelName, string category = "Unknown")
    {
        LogEvent("Model Selected", new Dictionary<string, object>
        {
            { "model", modelName },
            { "category", category },
            { "selectionTime", Time.time }
        });
    }

    public void LogPerformanceMetric(string metricName, float value, string unit = "")
    {
        if (!settings.logPerformanceMetrics) return;

        LogEvent("Performance Metric", new Dictionary<string, object>
        {
            { "metric", metricName },
            { "value", value },
            { "unit", unit },
            { "timestamp", Time.time }
        });
    }

    public void LogError(string errorType, string errorMessage, string stackTrace = "")
    {
        LogEvent("Error", new Dictionary<string, object>
        {
            { "errorType", errorType },
            { "message", errorMessage },
            { "stackTrace", stackTrace },
            { "scene", SceneManager.GetActiveScene().name }
        });
    }

    #endregion

    #region Context Menu (Editor Only)

    [ContextMenu("Flush All Logs")]
    private void ContextFlushLogs()
    {
        FlushAllLogs();
    }

    [ContextMenu("Clear Cache")]
    private void ContextClearCache()
    {
        offlineCache.Clear();
        currentBatch.Clear();
        OnCacheUpdated?.Invoke(0);
        Debug.Log("[LoggerManager] Cache cleared");
    }

    [ContextMenu("Log Session Stats")]
    private void ContextLogSessionStats()
    {
        var stats = GetSessionStats();
        Debug.Log($"[LoggerManager] Session Stats: {string.Join(", ", stats.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
    }

    #endregion
}