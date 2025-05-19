using System.Collections;
using UnityEngine;
using TMPro;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine.Networking;
using UnityEngine.UI;
using Firebase.Extensions;
using System.Threading.Tasks;

public class UserProfileDisplay : MonoBehaviour
{
    public TextMeshProUGUI usernameText;
    public RawImage profileImage;
    public TextMeshProUGUI userType;
    public Texture2D defaultProfileImage;

    void Start()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (user != null && user.IsEmailVerified)
        {
            LoadUserProfile(user.UserId);
        }
        else
        {
            DisplayGuestProfile();
        }
    }

    void DisplayGuestProfile()
    {
        usernameText.text = "Guest";
        userType.text = "Guest Account";
        profileImage.texture = defaultProfileImage;
    }

    void LoadUserProfile(string userId)
    {
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference docRef = db.Collection("Students").Document(userId);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread((Task<DocumentSnapshot> task) =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                DocumentSnapshot snapshot = task.Result;
                string name = snapshot.ContainsField("displayName") ? snapshot.GetValue<string>("displayName") : "Guest";
                string type = snapshot.ContainsField("userType") ? snapshot.GetValue<string>("userType") : "Registered Account";
                string imageUrl = snapshot.ContainsField("profileImageUrl") ? snapshot.GetValue<string>("profileImageUrl") : "";

                usernameText.text = name;
                userType.text = type;

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    StartCoroutine(LoadProfilePicture(imageUrl));
                }
                else
                {
                    profileImage.texture = defaultProfileImage;
                }
            }
            else
            {
                Debug.LogWarning("User profile not found in Firestore.");
                DisplayGuestProfile();
            }
        });
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
            profileImage.texture = defaultProfileImage;
        }
    }
}
