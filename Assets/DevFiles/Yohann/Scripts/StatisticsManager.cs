using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class StatisticsManager : MonoBehaviour
{
    [Header("User Info UI")]
    public TMP_Text companyText;
    public TMP_Text createdAtText;
    public TMP_Text firstNameText;
    public TMP_Text lastNameText;
    public TMP_Text fullNameText; // Combined first + last name display
    public TMP_Text emailText;
    public TMP_Text lastLoginText;
    public TMP_Text roleText;
    public TMP_Text uidText;
    public TMP_Text enrolledClassesText;
    public RawImage profileImage;

    [Header("Profile Image Settings")]
    public Texture2D defaultProfileImage; // Assign a default image in the inspector

    [Header("Enrolled Classes UI")]
    public Transform enrolledClassesParent; // For displaying individual class items
    public GameObject classEntryPrefab; // Prefab for individual class display

    [Header("History Log UI")]
    public Transform historyContentParent; // Assign ScrollView/Viewport/Content here
    public GameObject historyEntryPrefab;  // Assign a prefab with TMP_Text child

    [Header("Statistics UI")]
    public TMP_Text totalClassesText;
    public TMP_Text totalLoginText;
    public TMP_Text memberSinceText;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = false;

    private FirebaseAuth auth;
    private FirebaseFirestore firestore;
    private const string COLLECTION_USERS = "users";
    private const string COLLECTION_CLASSES = "classes"; // Add classes collection

    [System.Serializable]
    public class UserData
    {
        public string company;
        public string email;
        public List<string> enrolledClasses;
        public string firstName;
        public string lastName;
        public string role;
        public string uid;
        public DateTime createdAt;
        public DateTime lastLogin;
        public string profileImageUrl; // Added for profile image URL
    }

    [System.Serializable]
    public class ClassInfo
    {
        public string name;
        public string code; // Changed from classCode to code
        public string documentId;

        public ClassInfo(string name, string code, string documentId)
        {
            this.name = name;
            this.code = code;
            this.documentId = documentId;
        }
    }

    private void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        firestore = FirebaseFirestore.DefaultInstance;

        // Set default profile image immediately
        SetDefaultProfileImage();

        LoadUserInfo();
        LoadUserLogs();
    }

    private void SetDefaultProfileImage()
    {
        if (profileImage != null && defaultProfileImage != null)
        {
            profileImage.texture = defaultProfileImage;
        }
    }

    private void LoadUserInfo()
    {
        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("No user is logged in.");
            DisplayNoUserMessage();
            return;
        }

        DocumentReference userDocRef = firestore.Collection(COLLECTION_USERS).Document(user.UserId);
        userDocRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                try
                {
                    Dictionary<string, object> userData = task.Result.ToDictionary();
                    DisplayUserInfo(userData);
                    LoadAndDisplayEnrolledClasses(userData); // Changed to async method
                    DisplayUserStatistics(userData);
                    LoadProfileImageFromData(userData); // Load profile image
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing user data: {ex.Message}");
                    DisplayErrorMessage("Error loading user information");
                }
            }
            else
            {
                Debug.LogWarning("Failed to retrieve user info or user document doesn't exist.");
                DisplayErrorMessage("User information not found");
            }
        });
    }

    private void LoadProfileImageFromData(Dictionary<string, object> userData)
    {
        string profileImageUrl = GetFieldValue(userData, "profileImageUrl", "");

        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            StartCoroutine(LoadProfileImage(profileImageUrl));
        }
        else
        {
            // Keep the default image if no URL is found
            if (enableDebugLogging)
                Debug.Log("No profile image URL found, using default image");
        }
    }

    private void DisplayUserInfo(Dictionary<string, object> userData)
    {
        // Company
        if (companyText != null)
            companyText.text = GetFieldValue(userData, "company", "N/A");

        // Created At
        if (createdAtText != null)
            createdAtText.text = ConvertTimestamp(GetFieldValue(userData, "createdAt"));

        // Names
        string firstName = GetFieldValue(userData, "firstName", "");
        string lastName = GetFieldValue(userData, "lastName", "");

        if (firstNameText != null)
            firstNameText.text = string.IsNullOrEmpty(firstName) ? "N/A" : firstName;

        if (lastNameText != null)
            lastNameText.text = string.IsNullOrEmpty(lastName) ? "N/A" : lastName;

        if (fullNameText != null)
        {
            string fullName = $"{firstName} {lastName}".Trim();
            fullNameText.text = string.IsNullOrEmpty(fullName) ? "N/A" : fullName;
        }

        // Email
        if (emailText != null)
            emailText.text = GetFieldValue(userData, "email", "N/A");

        // Last Login
        if (lastLoginText != null)
            lastLoginText.text = ConvertTimestamp(GetFieldValue(userData, "lastLogin"));

        // Role (previously userType)
        if (roleText != null)
            roleText.text = GetFieldValue(userData, "role", "N/A");

        // UID
        if (uidText != null)
            uidText.text = GetFieldValue(userData, "uid", "N/A");

        if (enableDebugLogging)
        {
            Debug.Log($"User info loaded: {firstName} {lastName} ({GetFieldValue(userData, "role", "Unknown")})");
        }
    }

    private void LoadAndDisplayEnrolledClasses(Dictionary<string, object> userData)
    {
        List<string> enrolledClassIds = GetEnrolledClassesList(userData);

        if (enrolledClassIds.Count == 0)
        {
            DisplayEmptyEnrolledClasses();
            return;
        }

        // Fetch class details for each enrolled class
        StartCoroutine(FetchClassDetails(enrolledClassIds));
    }

    private System.Collections.IEnumerator FetchClassDetails(List<string> classIds)
    {
        List<ClassInfo> classInfoList = new List<ClassInfo>();
        int completedRequests = 0;

        foreach (string classId in classIds)
        {
            DocumentReference classDocRef = firestore.Collection(COLLECTION_CLASSES).Document(classId);

            var task = classDocRef.GetSnapshotAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Result.Exists)
            {
                try
                {
                    Dictionary<string, object> classData = task.Result.ToDictionary();
                    string className = GetFieldValue(classData, "name", "Unknown Class");
                    string classCode = GetFieldValue(classData, "code", "Unknown Code"); // Changed from "classCode" to "code"

                    classInfoList.Add(new ClassInfo(className, classCode, classId));

                    if (enableDebugLogging)
                        Debug.Log($"Loaded class: {className} ({classCode}) - Document ID: {classId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing class {classId}: {ex.Message}");
                    // Add with fallback values
                    classInfoList.Add(new ClassInfo("Unknown Class", "Unknown Code", classId));
                }
            }
            else
            {
                if (enableDebugLogging)
                    Debug.LogWarning($"Class document {classId} not found");
                // Add with fallback values
                classInfoList.Add(new ClassInfo("Class Not Found", "Unknown Code", classId));
            }

            completedRequests++;
        }

        // Wait for all requests to complete
        yield return new WaitUntil(() => completedRequests >= classIds.Count);

        // Display the results
        DisplayEnrolledClassesInfo(classInfoList);
    }

    private void DisplayEnrolledClassesInfo(List<ClassInfo> classInfoList)
    {
        // Update enrolled classes text - show "Class Name (Code)"
        if (enrolledClassesText != null)
        {
            if (classInfoList.Count > 0)
            {
                List<string> displayStrings = classInfoList.Select(c => $"{c.name} ({c.code})").ToList();
                enrolledClassesText.text = string.Join(", ", displayStrings);
            }
            else
            {
                enrolledClassesText.text = "No classes enrolled";
            }
        }

        // Create individual class entries if parent is assigned
        if (enrolledClassesParent != null && classEntryPrefab != null)
        {
            // Clear existing entries
            foreach (Transform child in enrolledClassesParent)
            {
                Destroy(child.gameObject);
            }

            // Create new entries - display "Class Name (Code)"
            foreach (ClassInfo classInfo in classInfoList)
            {
                GameObject entryGO = Instantiate(classEntryPrefab, enrolledClassesParent);
                TMP_Text entryText = entryGO.GetComponentInChildren<TMP_Text>();
                if (entryText != null)
                {
                    entryText.text = $"{classInfo.name} ({classInfo.code})";
                }
            }
        }

        // Update statistics
        if (totalClassesText != null)
        {
            totalClassesText.text = classInfoList.Count.ToString();
        }

        if (enableDebugLogging)
        {
            Debug.Log($"Displayed {classInfoList.Count} enrolled classes");
            foreach (var classInfo in classInfoList)
            {
                Debug.Log($"Class: {classInfo.name} ({classInfo.code})");
            }
        }
    }

    private void DisplayEmptyEnrolledClasses()
    {
        if (enrolledClassesText != null)
        {
            enrolledClassesText.text = "No classes enrolled";
        }

        // Clear existing entries
        if (enrolledClassesParent != null)
        {
            foreach (Transform child in enrolledClassesParent)
            {
                Destroy(child.gameObject);
            }
        }

        if (totalClassesText != null)
        {
            totalClassesText.text = "0";
        }
    }

    private void DisplayUserStatistics(Dictionary<string, object> userData)
    {
        // Total Classes will be updated in DisplayEnrolledClassesInfo

        // Member Since
        if (memberSinceText != null)
        {
            string createdAt = ConvertTimestamp(GetFieldValue(userData, "createdAt"));
            if (createdAt != "N/A" && createdAt != "Invalid Timestamp")
            {
                try
                {
                    DateTime created = ((Timestamp)userData["createdAt"]).ToDateTime();
                    memberSinceText.text = created.ToString("MMMM yyyy");
                }
                catch
                {
                    memberSinceText.text = "Unknown";
                }
            }
            else
            {
                memberSinceText.text = "Unknown";
            }
        }

        // Note: Total login count would require additional tracking in your database
        // For now, we can show last login info
        if (totalLoginText != null)
        {
            string lastLogin = ConvertTimestamp(GetFieldValue(userData, "lastLogin"));
            totalLoginText.text = lastLogin != "N/A" ? "Available" : "No login data";
        }
    }

    private void LoadUserLogs()
    {
        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("No user is logged in.");
            return;
        }

        // Updated to use new collection structure
        CollectionReference logsRef = firestore.Collection(COLLECTION_USERS).Document(user.UserId).Collection("LogsAR");
        logsRef.OrderByDescending("timestamp").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Failed to fetch user logs.");
                DisplayLogError();
                return;
            }

            // Clear existing log entries
            if (historyContentParent != null)
            {
                foreach (Transform child in historyContentParent)
                {
                    Destroy(child.gameObject);
                }
            }

            if (task.Result.Documents.Count() == 0)
            {
                DisplayNoLogsMessage();
                return;
            }

            foreach (DocumentSnapshot logDoc in task.Result.Documents)
            {
                try
                {
                    Dictionary<string, object> logData = logDoc.ToDictionary();
                    CreateLogEntry(logData, logDoc.Id);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing log entry {logDoc.Id}: {ex.Message}");
                }
            }

            if (enableDebugLogging)
            {
                Debug.Log($"Loaded {task.Result.Documents.Count()} log entries");
            }
        });
    }

    private void CreateLogEntry(Dictionary<string, object> logData, string logId)
    {
        if (historyContentParent == null || historyEntryPrefab == null) return;

        string eventType = GetFieldValue(logData, "eventType", "Unknown Event");
        string contextInfo = GetContextInfo(logData, eventType, logId);
        string timestamp = ConvertTimestamp(GetFieldValue(logData, "timestamp"));

        GameObject entryGO = Instantiate(historyEntryPrefab, historyContentParent);
        TMP_Text entryText = entryGO.GetComponentInChildren<TMP_Text>();
        if (entryText != null)
        {
            entryText.text = $"{eventType}{contextInfo} - {timestamp}";
        }
    }

    private string GetContextInfo(Dictionary<string, object> logData, string eventType, string logId)
    {
        string contextInfo = "";

        if (enableDebugLogging)
        {
            Debug.Log($"Log Entry: {eventType} - Keys: {string.Join(", ", logData.Keys)}");
        }

        // Determine context details based on eventType
        switch (eventType)
        {
            case "SceneLoaded":
                if (logData.TryGetValue("sceneName", out var scene))
                {
                    contextInfo = ": " + scene.ToString();
                }
                else
                {
                    if (enableDebugLogging)
                        Debug.LogWarning($"Missing 'sceneName' in log {logId}. Keys: {string.Join(", ", logData.Keys)}");
                }
                break;

            case "ARSceneOpened":
                if (logData.TryGetValue("sceneName", out scene))
                {
                    contextInfo = ": " + scene.ToString();
                }
                else
                {
                    if (enableDebugLogging)
                        Debug.LogWarning($"Missing 'sceneName' in log {logId}. Keys: {string.Join(", ", logData.Keys)}");
                }
                break;

            case "ButtonClick":
                if (logData.TryGetValue("buttonName", out var button))
                {
                    contextInfo = ": " + button.ToString();
                }
                else
                {
                    if (enableDebugLogging)
                        Debug.LogWarning($"Missing 'buttonName' in log {logId}");
                }
                break;

            case "ClassEnrolled":
                if (logData.TryGetValue("classCode", out var classCode))
                {
                    contextInfo = ": " + classCode.ToString();
                }
                break;

            case "ClassUnenrolled":
                if (logData.TryGetValue("classCode", out classCode))
                {
                    contextInfo = ": " + classCode.ToString();
                }
                break;

            case "AppQuit":
                // No additional context needed
                break;

            default:
                if (enableDebugLogging)
                    Debug.Log($"Unhandled eventType: {eventType} in log {logId}");
                break;
        }

        return contextInfo;
    }

    private string GetFieldValue(Dictionary<string, object> data, string key, string defaultValue = "N/A")
    {
        if (data != null && data.ContainsKey(key) && data[key] != null)
        {
            return data[key].ToString();
        }
        return defaultValue;
    }

    private object GetFieldValue(Dictionary<string, object> data, string key)
    {
        if (data != null && data.ContainsKey(key))
        {
            return data[key];
        }
        return null;
    }

    private List<string> GetEnrolledClassesList(Dictionary<string, object> userData)
    {
        if (userData != null && userData.ContainsKey("enrolledClasses"))
        {
            try
            {
                var enrolledClassesObj = userData["enrolledClasses"];
                if (enrolledClassesObj is List<object> objectList)
                {
                    return objectList.Select(obj => obj.ToString()).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing enrolled classes: {ex.Message}");
            }
        }
        return new List<string>();
    }

    private string ConvertTimestamp(object ts)
    {
        if (ts == null) return "N/A";

        try
        {
            if (ts is Timestamp timestamp)
            {
                return timestamp.ToDateTime().ToLocalTime().ToString("g");
            }
            return "Invalid Timestamp";
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
                Debug.LogError($"Error converting timestamp: {ex.Message}");
            return "Invalid Timestamp";
        }
    }

    private void DisplayNoUserMessage()
    {
        SetAllTextsToValue("No user logged in");
        SetDefaultProfileImage(); // Ensure default image is shown
    }

    private void DisplayErrorMessage(string message)
    {
        SetAllTextsToValue(message);
        SetDefaultProfileImage(); // Ensure default image is shown
    }

    private void DisplayLogError()
    {
        if (historyContentParent != null && historyEntryPrefab != null)
        {
            GameObject entryGO = Instantiate(historyEntryPrefab, historyContentParent);
            TMP_Text entryText = entryGO.GetComponentInChildren<TMP_Text>();
            if (entryText != null)
            {
                entryText.text = "Failed to load activity history";
                entryText.color = Color.red;
            }
        }
    }

    private void DisplayNoLogsMessage()
    {
        if (historyContentParent != null && historyEntryPrefab != null)
        {
            GameObject entryGO = Instantiate(historyEntryPrefab, historyContentParent);
            TMP_Text entryText = entryGO.GetComponentInChildren<TMP_Text>();
            if (entryText != null)
            {
                entryText.text = "No activity history available";
                entryText.color = Color.gray;
            }
        }
    }

    private void SetAllTextsToValue(string value)
    {
        if (companyText != null) companyText.text = value;
        if (createdAtText != null) createdAtText.text = value;
        if (firstNameText != null) firstNameText.text = value;
        if (lastNameText != null) lastNameText.text = value;
        if (fullNameText != null) fullNameText.text = value;
        if (emailText != null) emailText.text = value;
        if (lastLoginText != null) lastLoginText.text = value;
        if (roleText != null) roleText.text = value;
        if (uidText != null) uidText.text = value;
        if (enrolledClassesText != null) enrolledClassesText.text = value;
    }

    System.Collections.IEnumerator LoadProfileImage(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (enableDebugLogging)
                Debug.Log("Profile image URL is empty, keeping default image");
            yield break;
        }

        using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (enableDebugLogging)
                    Debug.LogWarning("Failed to load profile image: " + uwr.error + ". Using default image.");
                // Keep the default image on failure
                SetDefaultProfileImage();
            }
            else
            {
                if (profileImage != null)
                {
                    Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                    profileImage.texture = tex;

                    if (enableDebugLogging)
                        Debug.Log("Profile image loaded successfully from URL");
                }
            }
        }
    }

    #region Public Methods

    public void RefreshUserInfo()
    {
        LoadUserInfo();
    }

    public void RefreshUserLogs()
    {
        LoadUserLogs();
    }

    public void RefreshAll()
    {
        LoadUserInfo();
        LoadUserLogs();
    }

    #endregion
}