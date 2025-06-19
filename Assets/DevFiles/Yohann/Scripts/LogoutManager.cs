using UnityEngine;
using Firebase.Auth;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using UnityEngine.UI;

public class LogoutManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject logoutButton;
    [SerializeField] private TextMeshProUGUI logoutText;

    [Header("Confirmation Panel")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Scene Navigation")]
    [SerializeField] private string loginSceneName = "LogInPage";

    [Header("Settings")]
    [SerializeField] private bool requireEmailVerification = true;
    [SerializeField] private float logoutDelay = 0.5f;

    // UI Colors
    private readonly Color32 activeColor = new Color32(255, 255, 255, 255);
    private readonly Color32 inactiveColor = new Color32(128, 128, 128, 255);

    private FirebaseAuth auth;
    private bool isLoggingOut = false;

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeFirebase();
    }

    private void Start()
    {
        StartCoroutine(InitializeLogoutManagerAsync());
        SetupUIEventListeners();
        HideUIElements();
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
    }

    #endregion

    #region Initialization

    private void InitializeFirebase()
    {
        try
        {
            auth = FirebaseAuth.DefaultInstance;
            if (auth == null)
            {
                Debug.LogError("[LogoutManager] Failed to initialize Firebase Auth");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LogoutManager] Firebase initialization error: {e.Message}");
        }
    }

    private IEnumerator InitializeLogoutManagerAsync()
    {
        if (auth == null)
        {
            Debug.LogError("[LogoutManager] Firebase Auth is null. Cannot initialize.");
            yield break;
        }

        // Wait for Firebase to be ready
        float timeout = 5f;
        float elapsed = 0f;

        while (auth.CurrentUser == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (auth.CurrentUser == null)
        {
            Debug.LogWarning("[LogoutManager] No user logged in after timeout");
            UpdateLogoutUI();
            yield break;
        }

        Debug.Log($"[LogoutManager] Firebase Auth initialized. Current user: {auth.CurrentUser.Email}");
        UpdateLogoutUI();

        // Subscribe to auth state changes
        auth.StateChanged += OnAuthStateChanged;
    }

    private void SetupUIEventListeners()
    {
        if (yesButton != null)
            yesButton.onClick.AddListener(ConfirmLogout);
        else
            Debug.LogWarning("[LogoutManager] Yes button not assigned");

        if (noButton != null)
            noButton.onClick.AddListener(CancelLogout);
        else
            Debug.LogWarning("[LogoutManager] No button not assigned");
    }

    private void HideUIElements()
    {
        SetPanelActive(confirmationPanel, false);
        SetPanelActive(loadingPanel, false);
    }

    #endregion

    #region Event Handlers

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        Debug.Log("[LogoutManager] Auth state changed");

        // Use main thread for UI updates
        if (this != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(UpdateUIOnMainThread());
        }
    }

    private IEnumerator UpdateUIOnMainThread()
    {
        yield return null; // Wait one frame to ensure we're on main thread
        UpdateLogoutUI();
    }

    #endregion

    #region UI Updates

    private void UpdateLogoutUI()
    {
        if (auth?.CurrentUser == null)
        {
            SetLogoutButtonState(false, "");
            return;
        }

        var user = auth.CurrentUser;
        bool canLogout = !requireEmailVerification || user.IsEmailVerified;

        string buttonText = canLogout ? "Logout" :
                          !user.IsEmailVerified ? "Email Not Verified" : "Logout";

        SetLogoutButtonState(canLogout, buttonText);

        Debug.Log($"[LogoutManager] UI updated - Can logout: {canLogout}, User: {user.Email}");
    }

    private void SetLogoutButtonState(bool isActive, string text)
    {
        if (logoutButton != null)
            logoutButton.SetActive(isActive);

        if (logoutText != null)
        {
            logoutText.text = text;
            logoutText.color = isActive ? activeColor : inactiveColor;
            logoutText.ForceMeshUpdate();
        }
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initiates the logout process with confirmation dialog
    /// </summary>
    public void Logout()
    {
        if (isLoggingOut)
        {
            Debug.LogWarning("[LogoutManager] Logout already in progress");
            return;
        }

        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("[LogoutManager] No user to logout");
            return;
        }

        Debug.Log("[LogoutManager] Logout requested");
        SetPanelActive(confirmationPanel, true);
    }

    /// <summary>
    /// Confirms logout and proceeds with signing out
    /// </summary>
    public void ConfirmLogout()
    {
        if (isLoggingOut) return;

        StartCoroutine(PerformLogoutAsync());
    }

    /// <summary>
    /// Cancels the logout process
    /// </summary>
    public void CancelLogout()
    {
        Debug.Log("[LogoutManager] Logout cancelled");
        SetPanelActive(confirmationPanel, false);
    }

    #endregion

    #region Logout Process

    private IEnumerator PerformLogoutAsync()
    {
        isLoggingOut = true;

        // Hide confirmation panel and show loading
        SetPanelActive(confirmationPanel, false);
        ShowLoadingUI("Signing out...");

        bool logoutSuccess = false;
        string errorMessage = "";

        try
        {
            var currentUser = auth.CurrentUser;
            if (currentUser != null)
            {
                Debug.Log($"[LogoutManager] Signing out user: {currentUser.Email}");
            }

            // Sign out from Firebase
            auth.SignOut();

            // Clear auto-login preference
            PlayerPrefs.SetInt("AutoLogin", 0);
            PlayerPrefs.Save();

            Debug.Log("[LogoutManager] User successfully signed out");
            logoutSuccess = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LogoutManager] Logout error: {e.Message}");
            errorMessage = e.Message;
            logoutSuccess = false;
        }

        // Handle the result after try-catch
        if (logoutSuccess)
        {
            // Optional delay for better UX (outside try-catch)
            if (logoutDelay > 0)
            {
                UpdateLoadingText("Redirecting...");
                yield return new WaitForSeconds(logoutDelay);
            }

            // Navigate to login scene
            LoadLoginScene();
        }
        else
        {
            Debug.LogError($"[LogoutManager] Logout failed: {errorMessage}, but proceeding to login scene");
            HandleLogoutError();
        }

        // Cleanup
        isLoggingOut = false;
        SetPanelActive(loadingPanel, false);
    }

    private void ShowLoadingUI(string message)
    {
        SetPanelActive(loadingPanel, true);
        UpdateLoadingText(message);
    }

    private void UpdateLoadingText(string message)
    {
        if (loadingText != null)
            loadingText.text = message;
    }

    private void HandleLogoutError()
    {
        Debug.LogError("[LogoutManager] Logout failed, but proceeding to login scene");
        // Even if logout fails, we should still redirect to login
        LoadLoginScene();
    }

    private void LoadLoginScene()
    {
        if (string.IsNullOrEmpty(loginSceneName))
        {
            Debug.LogError("[LogoutManager] Login scene name not set");
            return;
        }

        try
        {
            Debug.Log($"[LogoutManager] Loading scene: {loginSceneName}");
            SceneManager.LoadScene(loginSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LogoutManager] Failed to load scene '{loginSceneName}': {e.Message}");
        }
    }

    #endregion

    #region Cleanup

    private void CleanupEventListeners()
    {
        // Unsubscribe from Firebase events
        if (auth != null)
            auth.StateChanged -= OnAuthStateChanged;

        // Remove button listeners
        if (yesButton != null)
            yesButton.onClick.RemoveListener(ConfirmLogout);

        if (noButton != null)
            noButton.onClick.RemoveListener(CancelLogout);
    }

    #endregion
}