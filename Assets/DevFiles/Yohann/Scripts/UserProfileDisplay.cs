
using System.Collections;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserProfileDisplay : MonoBehaviour
{
    public TextMeshProUGUI usernameText;
    public RawImage profileImage;
    public TextMeshProUGUI userType;

    void Start()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null)
        {
            usernameText.text = user.DisplayName ?? "Guest";
            userType.text = user.DisplayName ?? "Guest Account";
            if (user.PhotoUrl != null)
            {
                StartCoroutine(LoadProfilePicture(user.PhotoUrl.ToString()));
            }
        }
        else
        {
            usernameText.text = "Guest";
            userType.text = "Guest Account";
        }
    }

    IEnumerator LoadProfilePicture(string url)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            profileImage.texture = texture;
        }
        else
        {
            Debug.LogWarning("Failed to load profile picture: " + request.error);
        }
    }
}
