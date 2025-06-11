using System.Collections;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine.Networking;
using UnityEngine.UI;
using Firebase.Extensions;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class UserProfileData
{
    public string displayName = "Guest";
    public string userType = "Guest Account";
    public string profileImageUrl = "";
    public string company = "";
    public string firstName = "";
    public string lastName = "";
    public string email = "";
    public string role = "";
    public List<string> enrolledClasses = new List<string>();
    public string enrolledClassNames = "No classes assigned"; // For display purposes

    public UserProfileData() { }

    public UserProfileData(DocumentSnapshot snapshot)
    {
        if (snapshot.Exists)
        {
            // Get basic user info
            firstName = snapshot.ContainsField("firstName") ? snapshot.GetValue<string>("firstName") : "";
            lastName = snapshot.ContainsField("lastName") ? snapshot.GetValue<string>("lastName") : "";
            displayName = !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName)
                ? $"{firstName} {lastName}".Trim()
                : "User";

            email = snapshot.ContainsField("email") ? snapshot.GetValue<string>("email") : "";
            company = snapshot.ContainsField("company") ? snapshot.GetValue<string>("company") : "";
            role = snapshot.ContainsField("role") ? snapshot.GetValue<string>("role") : "";

            // Set user type based on role
            userType = !string.IsNullOrEmpty(role) ? char.ToUpper(role[0]) + role.Substring(1) : "User";

            // Handle enrolled classes
            if (snapshot.ContainsField("enrolledClasses"))
            {
                var classesArray = snapshot.GetValue<List<object>>("enrolledClasses");
                enrolledClasses = new List<string>();
                if (classesArray != null)
                {
                    foreach (var classId in classesArray)
                    {
                        enrolledClasses.Add(classId.ToString());
                    }
                }
            }

            // Profile image URL (if you have this field in your schema)
            profileImageUrl = snapshot.ContainsField("profileImageUrl") ? snapshot.GetValue<string>("profileImageUrl") : "";
        }
    }
}

[System.Serializable]
public class ProfileUIReferences
{
    [Header("Text Components")]
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI userType;
    public TextMeshProUGUI classroomCodeText;
    public TextMeshProUGUI companyText; // New field for company
    public TextMeshProUGUI emailText; // New field for email

    [Header("Image Components")]
    public RawImage profileImage;
    public Texture2D defaultProfileImage;

    [Header("Optional Loading Elements")]
    public GameObject loadingIndicator;
    public Button refreshButton;
}

public class UserProfileDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ProfileUIReferences uiRefs;

    [Header("Settings")]
    [SerializeField] private bool autoRefreshOnEnable = true;
    [SerializeField] private float refreshCooldown = 2f;
    [SerializeField] private int maxImageSize = 512;
    [SerializeField] private bool cacheProfileImages = true;

    [Header("Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color errorTextColor = Color.red;
    [SerializeField] private Color loadingTextColor = Color.gray;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;

    // Private fields
    private UserProfileData currentProfileData;
    private FirebaseUser currentUser;
    private bool isLoading = false;
    private float lastRefreshTime = 0f;
    private readonly Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
    private Coroutine currentImageLoadCoroutine;

    // Events
    public System.Action<UserProfileData> OnProfileLoaded;
    public System.Action<string> OnProfileLoadFailed;

    // Properties
    public bool IsLoading => isLoading;
    public UserProfileData CurrentProfile => currentProfileData;
    public bool HasValidUser => currentUser != null && currentUser.IsEmailVerified;

    void Awake()
    {
        ValidateReferences();
        InitializeComponents();
    }

    void Start()
    {
        LoadUserProfileAsync();
    }

    void OnEnable()
    {
        if (autoRefreshOnEnable && Time.time - lastRefreshTime > refreshCooldown)
        {
            LoadUserProfileAsync();
        }
    }

    void OnDisable()
    {
        StopAllImageLoading();
    }

    private void ValidateReferences()
    {
        if (uiRefs.usernameText == null)
            LogWarning("Username text reference is missing!");
        if (uiRefs.userType == null)
            LogWarning("User type text reference is missing!");
        if (uiRefs.classroomCodeText == null)
            LogWarning("Classroom code text reference is missing!");
        if (uiRefs.profileImage == null)
            LogWarning("Profile image reference is missing!");
        if (uiRefs.defaultProfileImage == null)
            LogWarning("Default profile image is missing!");
    }

    private void InitializeComponents()
    {
        if (uiRefs.refreshButton != null)
        {
            uiRefs.refreshButton.onClick.AddListener(() => RefreshProfile());
        }

        currentProfileData = new UserProfileData();
    }

    public async void LoadUserProfileAsync()
    {
        if (isLoading)
        {
            LogDebug("Profile loading already in progress.");
            return;
        }

        isLoading = true;
        SetLoadingState(true);
        lastRefreshTime = Time.time;

        try
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;

            if (currentUser != null && currentUser.IsEmailVerified)
            {
                LogDebug($"Authenticated user: {currentUser.Email}, ID: {currentUser.UserId}");
                await LoadUserProfileFromFirestore(currentUser.UserId);
            }
            else
            {
                LogWarning("User is not logged in or email not verified.");
                DisplayGuestProfile();
            }
        }
        catch (Exception ex)
        {
            LogError($"Error loading user profile: {ex.Message}");
            DisplayErrorProfile("Failed to load profile");
        }
        finally
        {
            isLoading = false;
            SetLoadingState(false);
        }
    }

    private async Task LoadUserProfileFromFirestore(string userId)
    {
        try
        {
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            DocumentReference docRef = db.Collection("users").Document(userId); // Changed from "Students" to "users"

            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                currentProfileData = new UserProfileData(snapshot);
                LogDebug($"Profile loaded: {currentProfileData.displayName}");

                // Load class names if enrolled classes exist
                if (currentProfileData.enrolledClasses != null && currentProfileData.enrolledClasses.Count > 0)
                {
                    await LoadClassNames(currentProfileData.enrolledClasses);
                }

                DisplayUserProfile();
                OnProfileLoaded?.Invoke(currentProfileData);
            }
            else
            {
                LogWarning("User profile not found in Firestore.");
                DisplayGuestProfile();
                OnProfileLoadFailed?.Invoke("Profile not found");
            }
        }
        catch (Exception ex)
        {
            LogError($"Firestore error: {ex.Message}");
            DisplayErrorProfile("Database connection failed");
            OnProfileLoadFailed?.Invoke(ex.Message);
        }
    }

    private async Task LoadClassNames(List<string> classIds)
    {
        try
        {
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            List<string> classNames = new List<string>();

            if (classIds.Count == 0)
            {
                currentProfileData.enrolledClassNames = "No classes assigned";
                return;
            }

            // Use batch query for better performance
            Query query = db.Collection("classes").WhereIn("__name__", classIds);
            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();

            foreach (DocumentSnapshot document in querySnapshot.Documents)
            {
                if (document.ContainsField("name"))
                {
                    string className = document.GetValue<string>("name");
                    classNames.Add(className); // Only add valid class names
                    LogDebug($"Class name loaded: {className} (ID: {document.Id})");
                }
            }

            // Log any missing classes for debugging (but don't display them)
            var foundClassIds = querySnapshot.Documents.Select(doc => doc.Id).ToHashSet();
            var missingClassIds = classIds.Where(id => !foundClassIds.Contains(id)).ToList();

            if (missingClassIds.Count > 0)
            {
                LogWarning($"Missing class documents for IDs: {string.Join(", ", missingClassIds)}");
            }

            currentProfileData.enrolledClassNames = classNames.Count > 0
                ? string.Join(", ", classNames)
                : "No classes assigned";

            LogDebug($"Loaded {classNames.Count} valid class names: {currentProfileData.enrolledClassNames}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to load class names: {ex.Message}");
            currentProfileData.enrolledClassNames = "Error loading classes";
        }
    }
    private void DisplayUserProfile()
    {
        UpdateTextComponent(uiRefs.usernameText, currentProfileData.displayName, normalTextColor);
        UpdateTextComponent(uiRefs.userType, currentProfileData.userType, normalTextColor);
        UpdateTextComponent(uiRefs.classroomCodeText, currentProfileData.enrolledClassNames, normalTextColor);

        // Update new fields if UI references exist
        if (uiRefs.companyText != null)
            UpdateTextComponent(uiRefs.companyText, currentProfileData.company, normalTextColor);

        if (uiRefs.emailText != null)
            UpdateTextComponent(uiRefs.emailText, currentProfileData.email, normalTextColor);

        LoadProfileImage(currentProfileData.profileImageUrl);
    }

    private void DisplayGuestProfile()
    {
        LogDebug("Displaying guest profile.");
        currentProfileData = new UserProfileData();

        UpdateTextComponent(uiRefs.usernameText, "Guest", normalTextColor);
        UpdateTextComponent(uiRefs.userType, "Guest Account", normalTextColor);
        UpdateTextComponent(uiRefs.classroomCodeText, "No classes assigned", normalTextColor);

        if (uiRefs.companyText != null)
            UpdateTextComponent(uiRefs.companyText, "", normalTextColor);

        if (uiRefs.emailText != null)
            UpdateTextComponent(uiRefs.emailText, "", normalTextColor);

        SetDefaultProfileImage();
        OnProfileLoaded?.Invoke(currentProfileData);
    }

    private void DisplayErrorProfile(string errorMessage)
    {
        UpdateTextComponent(uiRefs.usernameText, "Error", errorTextColor);
        UpdateTextComponent(uiRefs.userType, errorMessage, errorTextColor);
        UpdateTextComponent(uiRefs.classroomCodeText, "Please try again", errorTextColor);

        if (uiRefs.companyText != null)
            UpdateTextComponent(uiRefs.companyText, "", errorTextColor);

        if (uiRefs.emailText != null)
            UpdateTextComponent(uiRefs.emailText, "", errorTextColor);

        SetDefaultProfileImage();
    }

    private void UpdateTextComponent(TextMeshProUGUI textComponent, string text, Color color)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
            textComponent.color = color;
            textComponent.ForceMeshUpdate();
        }
    }

    private void LoadProfileImage(string imageUrl)
    {
        StopAllImageLoading();

        if (string.IsNullOrEmpty(imageUrl))
        {
            LogDebug("No profile image URL. Using default.");
            SetDefaultProfileImage();
            return;
        }

        if (cacheProfileImages && imageCache.TryGetValue(imageUrl, out Texture2D cachedTexture))
        {
            LogDebug("Using cached profile image.");
            SetProfileImage(cachedTexture);
            return;
        }

        currentImageLoadCoroutine = StartCoroutine(LoadProfileImageCoroutine(imageUrl));
    }

    private IEnumerator LoadProfileImageCoroutine(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;

                if (texture.width > maxImageSize || texture.height > maxImageSize)
                {
                    texture = ResizeTexture(texture, maxImageSize);
                }

                SetProfileImage(texture);

                if (cacheProfileImages)
                {
                    imageCache[url] = texture;
                }

                LogDebug("Profile picture successfully loaded and cached.");
            }
            else
            {
                LogWarning($"Failed to load profile picture: {request.error}");
                SetDefaultProfileImage();
            }
        }
    }

    private Texture2D ResizeTexture(Texture2D originalTexture, int maxSize)
    {
        float ratio = Mathf.Min((float)maxSize / originalTexture.width, (float)maxSize / originalTexture.height);
        int newWidth = Mathf.RoundToInt(originalTexture.width * ratio);
        int newHeight = Mathf.RoundToInt(originalTexture.height * ratio);

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(originalTexture, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return resizedTexture;
    }

    private void SetProfileImage(Texture2D texture)
    {
        if (uiRefs.profileImage != null)
        {
            uiRefs.profileImage.texture = texture;
        }
    }

    private void SetDefaultProfileImage()
    {
        if (uiRefs.profileImage != null && uiRefs.defaultProfileImage != null)
        {
            uiRefs.profileImage.texture = uiRefs.defaultProfileImage;
        }
    }

    private void SetLoadingState(bool loading)
    {
        if (uiRefs.loadingIndicator != null)
        {
            uiRefs.loadingIndicator.SetActive(loading);
        }

        if (uiRefs.refreshButton != null)
        {
            uiRefs.refreshButton.interactable = !loading;
        }

        if (loading)
        {
            UpdateTextComponent(uiRefs.usernameText, "Loading...", loadingTextColor);
        }
    }

    private void StopAllImageLoading()
    {
        if (currentImageLoadCoroutine != null)
        {
            StopCoroutine(currentImageLoadCoroutine);
            currentImageLoadCoroutine = null;
        }
    }

    // Public Methods
    public void RefreshProfile()
    {
        if (Time.time - lastRefreshTime < refreshCooldown)
        {
            LogDebug($"Refresh cooldown active. Wait {refreshCooldown - (Time.time - lastRefreshTime):F1} seconds.");
            return;
        }

        LoadUserProfileAsync();
    }

    public void ClearImageCache()
    {
        foreach (var texture in imageCache.Values)
        {
            if (texture != null && texture != uiRefs.defaultProfileImage)
            {
                DestroyImmediate(texture);
            }
        }
        imageCache.Clear();
        LogDebug("Image cache cleared.");
    }

    public void UpdateProfileData(UserProfileData newData)
    {
        currentProfileData = newData;
        DisplayUserProfile();
    }

    // Debug Methods
    private void LogDebug(string message)
    {
        if (enableDebugLogging)
            Debug.Log($"[UserProfileDisplay] {message}");
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogging)
            Debug.LogWarning($"[UserProfileDisplay] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[UserProfileDisplay] {message}");
    }

    // Context Menu Methods
    [ContextMenu("Refresh Profile")]
    private void ContextRefreshProfile()
    {
        RefreshProfile();
    }

    [ContextMenu("Clear Image Cache")]
    private void ContextClearCache()
    {
        ClearImageCache();
    }

    [ContextMenu("Display Guest Profile")]
    private void ContextDisplayGuest()
    {
        DisplayGuestProfile();
    }

    void OnDestroy()
    {
        ClearImageCache();
        StopAllImageLoading();
    }
}