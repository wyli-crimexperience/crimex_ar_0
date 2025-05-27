using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class LoggerManager : MonoBehaviour
{
    public static LoggerManager Instance;

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        LogAppStarted();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        LogAppQuit();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LogSceneLoaded(scene.name);

        if (scene.name.ToLower().Contains("ar"))
        {
            LogARSceneOpened(scene.name);
        }
    }

    public void LogEvent(string eventType, Dictionary<string, object> extraData = null)
    {
        string userId = auth.CurrentUser != null ? auth.CurrentUser.UserId : "guest";

        var logEntry = new Dictionary<string, object>
    {
        { "eventType", eventType },
        { "timestamp", Timestamp.GetCurrentTimestamp() }
    };

        if (extraData != null)
        {
            foreach (var pair in extraData)
                logEntry[pair.Key] = pair.Value;
        }

        string timestampKey = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string documentId = $"{timestampKey}_{eventType}";

        db.Collection("Students")
          .Document(userId)
          .Collection("LogsAR")
          .Document(documentId)
          .SetAsync(logEntry)
          .ContinueWithOnMainThread(task =>
          {
              if (task.IsCompletedSuccessfully)
                  Debug.Log($"[LoggerManager] Logged: {documentId}");
              else
                  Debug.LogWarning($"[LoggerManager] Failed to log: {task.Exception}");
          });
    }

    // === Specific Log Methods ===

    public void LogAppStarted()
    {
        LogEvent("App Started", new Dictionary<string, object>
        {
            { "deviceModel", SystemInfo.deviceModel },
            { "platform", Application.platform.ToString() }
        });
    }

    public void LogAppQuit()
    {
        LogEvent("App Quit");
    }

    public void LogARSceneOpened(string sceneName)
    {
        LogEvent("AR Scene Opened", new Dictionary<string, object>
        {
            { "sceneName", sceneName }
        });
    }

    public void LogSceneLoaded(string sceneName)
    {
        LogEvent("SceneLoaded", new Dictionary<string, object>
        {
            { "sceneName", sceneName }
        });
    }

    public void LogUserLogin()
    {
        LogEvent("User Login");
    }

    public void LogUserLogout()
    {
        LogEvent("User Logout");
    }

    public void LogUserSignup(string userType)
    {
        LogEvent("User Signup", new Dictionary<string, object>
        {
            { "userType", userType }
        });
    }

    public void LogToolInteraction(string toolName)
    {
        LogEvent("Tool Used", new Dictionary<string, object>
        {
            { "tool", toolName }
        });
    }

    public void LogModelSelected(string modelName)
    {
        LogEvent("Model Selected", new Dictionary<string, object>
        {
            { "model", modelName }
        });
    }
}
