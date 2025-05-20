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
    public TextMeshProUGUI classroomCodeText;
    public Texture2D defaultProfileImage;

    void Start()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null && user.IsEmailVerified)
        {
            Debug.Log($"Authenticated user: {user.Email}, ID: {user.UserId}");
            LoadUserProfile(user.UserId);
        }
        else
        {
            Debug.LogWarning("User is not logged in or email not verified.");
            DisplayGuestProfile();
        }
    }

    void DisplayGuestProfile()
    {
        Debug.Log("Displaying guest profile.");
        usernameText.text = "Guest";
        userType.text = "Guest Account";
        classroomCodeText.text = "No class assigned";
        profileImage.texture = defaultProfileImage;
    }

    void LoadUserProfile(string userId)
    {
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference docRef = db.Collection("Students").Document(userId);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(async (Task<DocumentSnapshot> task) =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                DocumentSnapshot snapshot = task.Result;
                Debug.Log("Firestore document found for user ID: " + userId);

                string name = snapshot.ContainsField("displayName") ? snapshot.GetValue<string>("displayName") : "Guest";
                string type = snapshot.ContainsField("userType") ? snapshot.GetValue<string>("userType") : "Registered Account";
                string imageUrl = snapshot.ContainsField("profileImageUrl") ? snapshot.GetValue<string>("profileImageUrl") : "";
                string classCode = snapshot.ContainsField("classCode") ? snapshot.GetValue<string>("classCode") : "";

                Debug.Log("Retrieved from Firestore:");
                Debug.Log($"Display Name: {name}");
                Debug.Log($"User Type: {type}");
                Debug.Log($"Profile Image URL: {imageUrl}");
                Debug.Log($"Class Code: {classCode}");

                usernameText.text = name;
                usernameText.color = Color.white;
                usernameText.ForceMeshUpdate();

                userType.text = type;
                userType.color = Color.white;
                userType.ForceMeshUpdate();

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    StartCoroutine(LoadProfilePicture(imageUrl));
                }
                else
                {
                    Debug.Log("No profile image URL found. Using default.");
                    profileImage.texture = defaultProfileImage;
                }

                // Load class name from Classes collection
                if (!string.IsNullOrEmpty(classCode))
                {
                    DocumentReference classDocRef = db.Collection("Classes").Document(classCode);
                    DocumentSnapshot classSnapshot = await classDocRef.GetSnapshotAsync();

                    if (classSnapshot.Exists && classSnapshot.ContainsField("className"))
                    {
                        string className = classSnapshot.GetValue<string>("className");
                        classroomCodeText.text = className;
                    }
                    else
                    {
                        classroomCodeText.text = "Class not found";
                    }
                }
                else
                {
                    classroomCodeText.text = "No class assigned";
                }
            }
            else
            {
                Debug.LogWarning("User profile not found in Firestore or task failed.");
                if (task.Exception != null)
                {
                    Debug.LogError("Firestore task exception: " + task.Exception);
                }
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
            Debug.Log("Profile picture successfully loaded.");
        }
        else
        {
            Debug.LogWarning("Failed to load profile picture: " + request.error);
            profileImage.texture = defaultProfileImage;
        }
    }
}
