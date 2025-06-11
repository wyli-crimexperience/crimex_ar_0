using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Threading.Tasks;
using System;
using System.Linq;
using TMPro;

[System.Serializable]
public class CourseButton
{
    [Header("Course Configuration")]
    public string courseName;
    public Button courseButton;
    public GameObject lockedOverlay;
    public GameObject unlockedIndicator;

    [Header("Visual States")]
    public Color lockedColor = Color.gray;
    public Color unlockedColor = Color.white;
    public Sprite lockedIcon;
    public Sprite unlockedIcon;

    [Header("Optional Components")]
    public Image courseIcon;
    public TextMeshProUGUI statusText;
}

public class ClassUnlockerManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UserProfileDisplay userProfileDisplay; // Reference to your profile script

    [Header("Course Buttons")]
    [SerializeField] private List<CourseButton> courseButtons = new List<CourseButton>();

    [Header("Settings")]
    [SerializeField] private bool autoCheckOnStart = false; // Changed to false - will wait for profile
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private float checkCooldown = 5f;
    [SerializeField] private float maxWaitTime = 30f; // Maximum time to wait for profile
    [SerializeField] private float profileCheckInterval = 0.5f; // How often to check if profile is ready

    [Header("UI Feedback")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private TextMeshProUGUI statusText; // Shows current status

    // Private fields
    private FirebaseUser currentUser;
    private bool isChecking = false;
    private float lastCheckTime = 0f;
    private Dictionary<string, List<string>> classUnlockData = new Dictionary<string, List<string>>();
    private bool hasCheckedInitially = false;

    // Events
    public System.Action<string> OnCourseUnlocked;
    public System.Action<string> OnCourseLocked;
    public System.Action<List<string>> OnUnlockDataLoaded;
    public System.Action OnAllCoursesLocked;

    // Properties
    public bool IsChecking => isChecking;
    public int UnlockedCourseCount => GetAllUnlockedCourses().Count;
    public bool HasUnlockedCourses => UnlockedCourseCount > 0;

    void Start()
    {
        ValidateReferences();
        InitializeButtons();
        SetupProfileIntegration();

        // Show initial status
        UpdateStatusText("Waiting for user profile...");

        // Start the profile-aware checking process
        StartCoroutine(WaitForProfileThenCheck());
    }

    void OnDestroy()
    {
        CleanupProfileIntegration();
    }

    private void ValidateReferences()
    {
        if (userProfileDisplay == null)
        {
            LogWarning("UserProfileDisplay reference is missing! Searching for it automatically...");
            userProfileDisplay = FindObjectOfType<UserProfileDisplay>();

            if (userProfileDisplay == null)
            {
                LogError("Could not find UserProfileDisplay in scene. Course unlocking may not work properly.");
            }
            else
            {
                LogDebug("Found UserProfileDisplay automatically.");
            }
        }

        if (courseButtons == null || courseButtons.Count == 0)
        {
            LogWarning("No course buttons configured!");
        }
    }

    private void SetupProfileIntegration()
    {
        if (userProfileDisplay != null)
        {
            // Subscribe to profile events
            userProfileDisplay.OnProfileLoaded += OnUserProfileLoaded;
            userProfileDisplay.OnProfileLoadFailed += OnUserProfileLoadFailed;
            LogDebug("Subscribed to UserProfileDisplay events.");
        }
    }

    private void CleanupProfileIntegration()
    {
        if (userProfileDisplay != null)
        {
            // Unsubscribe from events to prevent memory leaks
            userProfileDisplay.OnProfileLoaded -= OnUserProfileLoaded;
            userProfileDisplay.OnProfileLoadFailed -= OnUserProfileLoadFailed;
        }
    }

    private void InitializeButtons()
    {
        foreach (var courseButton in courseButtons)
        {
            if (courseButton.courseButton != null)
            {
                // Initially lock all courses
                SetCourseButtonState(courseButton, false);

                // Store the course name for the button click handler
                string courseName = courseButton.courseName;

                // Add click listener that checks if course is unlocked
                courseButton.courseButton.onClick.AddListener(() => {
                    if (IsCourseUnlocked(courseName))
                    {
                        OnCourseButtonClicked(courseName);
                    }
                    else
                    {
                        OnLockedCourseClicked(courseName);
                    }
                });
            }
            else
            {
                LogWarning($"Course button is null for course: {courseButton.courseName}");
            }
        }

        LogDebug($"Initialized {courseButtons.Count} course buttons.");
    }

    private IEnumerator WaitForProfileThenCheck()
    {
        float waitTime = 0f;
        UpdateStatusText("Waiting for user authentication...");

        // Wait for user profile to load or timeout
        while (waitTime < maxWaitTime)
        {
            if (userProfileDisplay != null)
            {
                // Check if profile is loaded and user is valid
                if (!userProfileDisplay.IsLoading && userProfileDisplay.HasValidUser)
                {
                    LogDebug("User profile loaded and valid. Checking unlocked courses.");
                    UpdateStatusText("Loading course data...");
                    CheckUnlockedCoursesAsync();
                    yield break;
                }
                // Check if profile loading failed or user is guest
                else if (!userProfileDisplay.IsLoading && !userProfileDisplay.HasValidUser)
                {
                    LogDebug("User profile indicates guest or invalid user. Locking courses.");
                    UpdateStatusText("Guest user - courses locked");
                    LockAllCourses();
                    yield break;
                }
            }

            yield return new WaitForSeconds(profileCheckInterval);
            waitTime += profileCheckInterval;
        }

        // If we timeout, handle gracefully
        LogWarning($"Timed out waiting for user profile after {maxWaitTime} seconds.");
        UpdateStatusText("Connection timeout - retrying...");

        // Try one more time in case there was a delay
        yield return new WaitForSeconds(1f);
        CheckUnlockedCoursesAsync();
    }

    private void OnUserProfileLoaded(UserProfileData profileData)
    {
        LogDebug($"User profile loaded event received for: {profileData.displayName}");
        LogDebug($"User enrolled in classes: {profileData.enrolledClassNames}");

        UpdateStatusText("Profile loaded - checking courses...");

        // Small delay to ensure all profile data is processed
        StartCoroutine(DelayedCourseCheck(0.5f));
    }

    private void OnUserProfileLoadFailed(string error)
    {
        LogWarning($"User profile load failed: {error}. Locking all courses.");
        UpdateStatusText($"Profile load failed: {error}");
        LockAllCourses();
    }

    private IEnumerator DelayedCourseCheck(float delay)
    {
        yield return new WaitForSeconds(delay);
        CheckUnlockedCoursesAsync();
    }

    public async void CheckUnlockedCoursesAsync()
    {
        if (isChecking)
        {
            LogDebug("Course unlock check already in progress.");
            return;
        }

        if (Time.time - lastCheckTime < checkCooldown)
        {
            LogDebug($"Check cooldown active. Wait {checkCooldown - (Time.time - lastCheckTime):F1} seconds.");
            return;
        }

        isChecking = true;
        lastCheckTime = Time.time;
        SetLoadingState(true);
        UpdateStatusText("Checking course access...");

        try
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;

            if (currentUser != null && currentUser.IsEmailVerified)
            {
                LogDebug($"Authenticated user: {currentUser.Email}, ID: {currentUser.UserId}");
                await LoadUserUnlockData(currentUser.UserId);
                UpdateCourseButtonStates();
                hasCheckedInitially = true;
            }
            else
            {
                LogWarning("User is not logged in or email not verified.");
                UpdateStatusText("User not authenticated");
                LockAllCourses();
            }
        }
        catch (Exception ex)
        {
            LogError($"Error checking unlocked courses: {ex.Message}");
            UpdateStatusText($"Error: {ex.Message}");
            LockAllCourses();
        }
        finally
        {
            isChecking = false;
            SetLoadingState(false);
        }
    }

    private async Task LoadUserUnlockData(string userId)
    {
        try
        {
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;

            // Get user's enrolled classes
            DocumentReference userDocRef = db.Collection("users").Document(userId);
            DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

            if (!userSnapshot.Exists)
            {
                LogWarning("User document not found in Firestore.");
                UpdateStatusText("User data not found");
                return;
            }

            List<string> enrolledClasses = new List<string>();
            if (userSnapshot.ContainsField("enrolledClasses"))
            {
                var classesArray = userSnapshot.GetValue<List<object>>("enrolledClasses");
                if (classesArray != null)
                {
                    enrolledClasses = classesArray.Select(c => c.ToString()).ToList();
                }
            }

            LogDebug($"User enrolled in {enrolledClasses.Count} classes: {string.Join(", ", enrolledClasses)}");

            if (enrolledClasses.Count == 0)
            {
                LogDebug("User not enrolled in any classes.");
                UpdateStatusText("Not enrolled in any classes");
                LockAllCourses();
                return;
            }

            // Load unlock data for each enrolled class
            classUnlockData.Clear();
            foreach (string classId in enrolledClasses)
            {
                await LoadClassUnlockData(classId);
            }

            var allUnlockedCourses = GetAllUnlockedCourses();
            OnUnlockDataLoaded?.Invoke(allUnlockedCourses);

            UpdateStatusText($"Found {allUnlockedCourses.Count} unlocked courses");
            LogDebug($"Total unlocked courses: {string.Join(", ", allUnlockedCourses)}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to load user unlock data: {ex.Message}");
            UpdateStatusText("Failed to load course data");
            throw;
        }
    }

    private async Task LoadClassUnlockData(string classId)
    {
        try
        {
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            DocumentReference classDocRef = db.Collection("classes").Document(classId);
            DocumentSnapshot classSnapshot = await classDocRef.GetSnapshotAsync();

            if (classSnapshot.Exists && classSnapshot.ContainsField("unlockCourse"))
            {
                var unlockArray = classSnapshot.GetValue<List<object>>("unlockCourse");
                if (unlockArray != null)
                {
                    List<string> unlockedCourses = unlockArray.Select(c => c.ToString().ToLower()).ToList();
                    classUnlockData[classId] = unlockedCourses;

                    LogDebug($"Class {classId} unlocks: {string.Join(", ", unlockedCourses)}");
                }
                else
                {
                    LogDebug($"Class {classId} has empty unlockCourse array");
                }
            }
            else
            {
                LogDebug($"No unlock data found for class {classId}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load unlock data for class {classId}: {ex.Message}");
        }
    }

    private void UpdateCourseButtonStates()
    {
        int unlockedCount = 0;

        foreach (var courseButton in courseButtons)
        {
            bool isUnlocked = IsCourseUnlocked(courseButton.courseName);
            SetCourseButtonState(courseButton, isUnlocked);

            if (isUnlocked)
            {
                unlockedCount++;
                OnCourseUnlocked?.Invoke(courseButton.courseName);
                LogDebug($"Course unlocked: {courseButton.courseName}");
            }
            else
            {
                OnCourseLocked?.Invoke(courseButton.courseName);
            }
        }

        UpdateDebugText();

        if (unlockedCount == 0)
        {
            UpdateStatusText("No courses available");
            OnAllCoursesLocked?.Invoke();
        }
        else
        {
            UpdateStatusText($"{unlockedCount}/{courseButtons.Count} courses available");
        }

        LogDebug($"Updated course states: {unlockedCount}/{courseButtons.Count} unlocked");
    }

    private bool IsCourseUnlocked(string courseName)
    {
        if (string.IsNullOrEmpty(courseName))
            return false;

        string courseNameLower = courseName.ToLower().Trim();

        foreach (var classUnlocks in classUnlockData.Values)
        {
            if (classUnlocks.Contains(courseNameLower))
            {
                return true;
            }
        }

        return false;
    }

    private void SetCourseButtonState(CourseButton courseButton, bool isUnlocked)
    {
        if (courseButton.courseButton != null)
        {
            courseButton.courseButton.interactable = isUnlocked;

            // Update visual state
            var buttonImage = courseButton.courseButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isUnlocked ? courseButton.unlockedColor : courseButton.lockedColor;
            }
        }

        // Update overlay
        if (courseButton.lockedOverlay != null)
        {
            courseButton.lockedOverlay.SetActive(!isUnlocked);
        }

        if (courseButton.unlockedIndicator != null)
        {
            courseButton.unlockedIndicator.SetActive(isUnlocked);
        }

        // Update icon
        if (courseButton.courseIcon != null)
        {
            if (isUnlocked && courseButton.unlockedIcon != null)
            {
                courseButton.courseIcon.sprite = courseButton.unlockedIcon;
            }
            else if (!isUnlocked && courseButton.lockedIcon != null)
            {
                courseButton.courseIcon.sprite = courseButton.lockedIcon;
            }
        }

        // Update status text
        if (courseButton.statusText != null)
        {
            courseButton.statusText.text = isUnlocked ? "Available" : "Locked";
            courseButton.statusText.color = isUnlocked ? Color.green : Color.red;
        }
    }

    private void LockAllCourses()
    {
        foreach (var courseButton in courseButtons)
        {
            SetCourseButtonState(courseButton, false);
        }

        classUnlockData.Clear();
        UpdateDebugText();
        OnAllCoursesLocked?.Invoke();

        if (!hasCheckedInitially)
        {
            UpdateStatusText("All courses locked");
        }

        LogDebug("All courses locked.");
    }

    private List<string> GetAllUnlockedCourses()
    {
        HashSet<string> allUnlocked = new HashSet<string>();

        foreach (var classUnlocks in classUnlockData.Values)
        {
            foreach (var course in classUnlocks)
            {
                allUnlocked.Add(course);
            }
        }

        return allUnlocked.ToList();
    }

    private void OnCourseButtonClicked(string courseName)
    {
        LogDebug($"Course button clicked: {courseName}");
        UpdateStatusText($"Opening {courseName}...");

        // Add your course navigation logic here
        // For example: 
        // SceneManager.LoadScene($"{courseName}Scene");
        // Or show course content panel, etc.
    }

    private void OnLockedCourseClicked(string courseName)
    {
        LogDebug($"Locked course clicked: {courseName}");
        UpdateStatusText($"{courseName} is locked");

        // Show locked course message or requirements
        // For example: ShowLockedCoursePopup(courseName);
    }

    private void SetLoadingState(bool isLoading)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(isLoading);
        }
    }

    private void UpdateDebugText()
    {
        if (debugText != null && enableDebugLogging)
        {
            var unlockedCourses = GetAllUnlockedCourses();
            debugText.text = unlockedCourses.Count > 0
                ? $"Unlocked: {string.Join(", ", unlockedCourses)}"
                : "No courses unlocked";
        }
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        LogDebug($"Status: {message}");
    }

    // Public Methods
    public bool IsCourseLocked(string courseName)
    {
        return !IsCourseUnlocked(courseName);
    }

    public List<string> GetUnlockedCourses()
    {
        return GetAllUnlockedCourses();
    }

    public List<string> GetLockedCourses()
    {
        var allCourses = courseButtons.Select(cb => cb.courseName).ToList();
        var unlockedCourses = GetAllUnlockedCourses();
        return allCourses.Where(course => !unlockedCourses.Contains(course.ToLower())).ToList();
    }

    public void RefreshUnlockStatus()
    {
        if (userProfileDisplay != null && userProfileDisplay.HasValidUser)
        {
            UpdateStatusText("Refreshing course access...");
            CheckUnlockedCoursesAsync();
        }
        else
        {
            LogWarning("Cannot refresh - user profile not ready");
            UpdateStatusText("Cannot refresh - please wait");
        }
    }

    public void ForceCheck()
    {
        lastCheckTime = 0f; // Reset cooldown
        CheckUnlockedCoursesAsync();
    }

    public void UnlockCourseForTesting(string courseName)
    {
        if (enableDebugLogging)
        {
            LogDebug($"Manually unlocking course for testing: {courseName}");

            // Add to a test class unlock data
            if (!classUnlockData.ContainsKey("test"))
            {
                classUnlockData["test"] = new List<string>();
            }

            classUnlockData["test"].Add(courseName.ToLower());
            UpdateCourseButtonStates();
        }
    }

    // Debug Methods
    private void LogDebug(string message)
    {
        if (enableDebugLogging)
            Debug.Log($"[ClassUnlockerManager] {message}");
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogging)
            Debug.LogWarning($"[ClassUnlockerManager] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ClassUnlockerManager] {message}");
    }

    // Context Menu Methods for Testing
    [ContextMenu("Check Unlocked Courses")]
    private void ContextCheckCourses()
    {
        CheckUnlockedCoursesAsync();
    }

    [ContextMenu("Force Refresh")]
    private void ContextForceRefresh()
    {
        ForceCheck();
    }

    [ContextMenu("Lock All Courses")]
    private void ContextLockAll()
    {
        LockAllCourses();
    }

    [ContextMenu("Test Unlock Photography")]
    private void ContextUnlockPhotography()
    {
        UnlockCourseForTesting("photography");
    }

    [ContextMenu("Show Debug Info")]
    private void ContextShowDebugInfo()
    {
        LogDebug($"Is Checking: {isChecking}");
        LogDebug($"Has Valid User: {userProfileDisplay?.HasValidUser}");
        LogDebug($"Profile Loading: {userProfileDisplay?.IsLoading}");
        LogDebug($"Unlock Data Count: {classUnlockData.Count}");
        LogDebug($"Unlocked Courses: {string.Join(", ", GetAllUnlockedCourses())}");
    }
}