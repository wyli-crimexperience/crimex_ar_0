using UnityEngine;
using Firebase.Auth;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;

public class LogoutManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject logoutButton; // Optional: Assign in Inspector
    public TextMeshProUGUI logoutText;

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
    }

    private IEnumerator InitializeLogoutManager()
    {
        // Wait until Firebase Auth is ready
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
            // Force-set the color using full RGBA to avoid alpha issues
            logoutText.color = showButton ? new Color32(255, 255, 255, 255) : new Color32(128, 128, 128, 255); // white if logged in, gray otherwise

            // Optional: Change the text itself for clarity
            logoutText.text = showButton ? "Logout" : "";

            logoutText.ForceMeshUpdate();
            Debug.Log($"[LogoutManager] Logout text color set to: {logoutText.color}");
        }
    }


    public void Logout()
    {
        if (auth.CurrentUser != null)
            Debug.Log($"[LogoutManager] Logging out user: {auth.CurrentUser.Email}");

        auth.SignOut();
        PlayerPrefs.SetInt("AutoLogin", 0);
        PlayerPrefs.Save();

        Debug.Log("[LogoutManager] User signed out. Redirecting to login scene...");
        SceneManager.LoadScene(loginSceneName);
    }
}
