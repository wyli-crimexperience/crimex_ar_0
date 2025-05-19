
using UnityEngine;
using Firebase.Auth;

public class LogoutManager : MonoBehaviour
{
    public GameObject logoutButton; // Assign the logout button GameObject in the Inspector

    private void Start()
    {
        // Check the user's authentication state at the start
        UpdateLogoutButtonState();
    }

    private void UpdateLogoutButtonState()
    {
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null && user.IsEmailVerified)
        {
            logoutButton.SetActive(true);
        }
        else
        {
            logoutButton.SetActive(false);
        }
    }

    public void Logout()
    {
        FirebaseAuth.DefaultInstance.SignOut();
        PlayerPrefs.SetInt("AutoLogin", 0);
        PlayerPrefs.Save();

        // Update the logout button state after logging out
        UpdateLogoutButtonState();
    }
}
