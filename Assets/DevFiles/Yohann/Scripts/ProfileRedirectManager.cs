using UnityEngine;
using Firebase.Auth;
using UnityEngine.SceneManagement;

public class ProfileRedirectManager : MonoBehaviour
{
    public string loginSceneName = "LoginScene"; // Name of the login scene
    public string userStatisticsSceneName = "UserStatisticsScene"; // Name of the user statistics scene

    // Call this method when the user taps on their profile
    public void OnProfileTapped()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (user != null && user.IsEmailVerified)
        {
            // Redirect to user statistics scene
            SceneManager.LoadScene(userStatisticsSceneName);
        }
        else
        {
            // Redirect to login scene
            SceneManager.LoadScene(loginSceneName);
        }
    }
}
