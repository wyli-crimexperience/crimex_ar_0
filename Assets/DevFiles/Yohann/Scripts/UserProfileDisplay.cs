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

[System.Serializable]
public class UserProfileData
{
    public string displayName = "Guest";
    public string userType = "Guest Account";
    public string profileImageUrl = "";
    public string classCode = "";
    public string className = "No class assigned";

    public UserProfileData() { }

    public UserProfileData(DocumentSnapshot snapshot)
    {
        if (snapshot.Exists)
        {
            displayName = snapshot.ContainsField("displayName") ? snapshot.GetValue<string>("displayName") : "Guest";
            userType = snapshot.ContainsField("userType") ? snapshot.GetValue<string>("userType") : "Registered Account";
            profileImageUrl = snapshot.ContainsField("profileImageUrl") ? snapshot.GetValue<string>("profileImageUrl") : "";
            classCode = snapshot.ContainsField("classCode") ? snapshot.GetValue<string>("classCode") : "";
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
            DocumentReference docRef = db.Collection("Students").Document(userId);

            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                currentProfileData = new UserProfileData(snapshot);
                LogDebug($"Profile loaded: {currentProfileData.displayName}");

                // Load class name if classCode exists
                if (!string.IsNullOrEmpty(currentProfileData.classCode))
                {
                    await LoadClassName(currentProfileData.classCode);
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

    private async Task LoadClassName(string classCode)
    {
        try
        {
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            DocumentReference classDocRef = db.Collection("Classes").Document(classCode);
            DocumentSnapshot classSnapshot = await classDocRef.GetSnapshotAsync();

            if (classSnapshot.Exists && classSnapshot.ContainsField("className"))
            {
                currentProfileData.className = classSnapshot.GetValue<string>("className");
                LogDebug($"Class name loaded: {currentProfileData.className}");
            }
            else
            {
                currentProfileData.className = "Class not found";
                LogWarning($"Class document not found for code: {classCode}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load class name: {ex.Message}");
            currentProfileData.className = "Error loading class";
        }
    }

    private void DisplayUserProfile()
    {
        UpdateTextComponent(uiRefs.usernameText, currentProfileData.displayName, normalTextColor);
        UpdateTextComponent(uiRefs.userType, currentProfileData.userType, normalTextColor);
        UpdateTextComponent(uiRefs.classroomCodeText, currentProfileData.className, normalTextColor);

        LoadProfileImage(currentProfileData.profileImageUrl);
    }

    private void DisplayGuestProfile()
    {
        LogDebug("Displaying guest profile.");
        currentProfileData = new UserProfileData();

        UpdateTextComponent(uiRefs.usernameText, "Guest", normalTextColor);
        UpdateTextComponent(uiRefs.userType, "Guest Account", normalTextColor);
        UpdateTextComponent(uiRefs.classroomCodeText, "No class assigned", normalTextColor);

        SetDefaultProfileImage();
        OnProfileLoaded?.Invoke(currentProfileData);
    }

    private void DisplayErrorProfile(string errorMessage)
    {
        UpdateTextComponent(uiRefs.usernameText, "Error", errorTextColor);
        UpdateTextComponent(uiRefs.userType, errorMessage, errorTextColor);
        UpdateTextComponent(uiRefs.classroomCodeText, "Please try again", errorTextColor);

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
            // Set timeout and other options
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;

                // Resize if too large
                if (texture.width > maxImageSize || texture.height > maxImageSize)
                {
                    texture = ResizeTexture(texture, maxImageSize);
                }

                SetProfileImage(texture);

                // Cache the texture
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