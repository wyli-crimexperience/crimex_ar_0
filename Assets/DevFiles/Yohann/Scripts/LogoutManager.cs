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
    public GameObject logoutButton; // Optional: Assign in Inspector
    public TextMeshProUGUI logoutText;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel; // Assign in Inspector
    public Button yesButton;             // Assign in Inspector
    public Button noButton;              // Assign in Inspector

    [Header("Scene Navigation")]
    public string loginSceneName = "LogInPage"; // Editable in Inspector

    private FirebaseAuth auth;

    private void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    private void Start()
    {
        StartCoroutine(InitializeLogoutManager());

        // Hook up confirmation button events
        if (yesButton != null)
            yesButton.onClick.AddListener(ConfirmLogout);

        if (noButton != null)
            noButton.onClick.AddListener(CancelLogout);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false); // Hide by default
    }

    private IEnumerator InitializeLogoutManager()
    {
        while (auth.CurrentUser == null)
        {
            yield return null;
        }

        Debug.Log($"[LogoutManager] Firebase Auth initialized. Current user: {auth.CurrentUser.Email}");

        UpdateLogoutUI();
        auth.StateChanged += OnAuthStateChanged;
    }

    private void OnDestroy()
    {
        if (auth != null)
            auth.StateChanged -= OnAuthStateChanged;

        // Clean up button listeners
        if (yesButton != null)
            yesButton.onClick.RemoveListener(ConfirmLogout);

        if (noButton != null)
            noButton.onClick.RemoveListener(CancelLogout);
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        Debug.Log("[LogoutManager] Auth state changed.");
        UpdateLogoutUI();
    }

    private void UpdateLogoutUI()
    {
        var user = auth.CurrentUser;
        bool showButton = user != null && user.IsEmailVerified;

        if (logoutButton != null)
            logoutButton.SetActive(showButton);

        if (logoutText != null)
        {
            logoutText.color = showButton ? new Color32(255, 255, 255, 255) : new Color32(128, 128, 128, 255);
            logoutText.text = showButton ? "Logout" : "";
            logoutText.ForceMeshUpdate();

            Debug.Log($"[LogoutManager] Logout text color set to: {logoutText.color}");
        }
    }

    // Called when the Logout button is clicked
    public void Logout()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    // Called when the user clicks "Yes" on the confirmation panel
    public void ConfirmLogout()
    {
        if (auth.CurrentUser != null)
            Debug.Log($"[LogoutManager] Logging out user: {auth.CurrentUser.Email}");

        auth.SignOut();
        PlayerPrefs.SetInt("AutoLogin", 0);
        PlayerPrefs.Save();

        Debug.Log("[LogoutManager] User signed out. Redirecting to login scene...");
        SceneManager.LoadScene(loginSceneName);
    }

    // Called when the user clicks "No" on the confirmation panel
    public void CancelLogout()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
    }
}
