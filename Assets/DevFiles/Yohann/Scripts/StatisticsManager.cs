using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class StatisticsManager : MonoBehaviour
{
    [Header("User Info UI")]
    public TMP_Text classCodeText;
    public TMP_Text createdAtText;
    public TMP_Text displayNameText;
    public TMP_Text emailText;
    public TMP_Text lastLoginText;
    public TMP_Text userTypeText;
    public RawImage profileImage;

    [Header("History Log UI")]
    public Transform historyContentParent; // Assign ScrollView/Viewport/Content here
    public GameObject historyEntryPrefab;  // Assign a prefab with TMP_Text child

    private FirebaseAuth auth;
    private FirebaseFirestore firestore;

    private void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        firestore = FirebaseFirestore.DefaultInstance;

        LoadUserInfo();
        LoadUserLogs();
    }

    private void LoadUserInfo()
    {
        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("No user is logged in.");
            return;
        }

        DocumentReference userDocRef = firestore.Collection("Students").Document(user.UserId);
        userDocRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                Dictionary<string, object> userData = task.Result.ToDictionary();

                classCodeText.text = userData.ContainsKey("classCode") ? userData["classCode"].ToString() : "N/A";
                createdAtText.text = userData.ContainsKey("createdAt") ? ConvertTimestamp(userData["createdAt"]) : "N/A";
                displayNameText.text = userData.ContainsKey("displayName") ? userData["displayName"].ToString() : "N/A";
                emailText.text = userData.ContainsKey("email") ? userData["email"].ToString() : "N/A";
                lastLoginText.text = userData.ContainsKey("lastLogin") ? ConvertTimestamp(userData["lastLogin"]) : "N/A";
                userTypeText.text = userData.ContainsKey("userType") ? userData["userType"].ToString() : "N/A";

                if (userData.ContainsKey("profileImageUrl"))
                {
                    string imageUrl = userData["profileImageUrl"].ToString();
                    StartCoroutine(LoadProfileImage(imageUrl));
                }
            }
            else
            {
                Debug.LogWarning("Failed to retrieve user info.");
            }
        });
    }

    private void LoadUserLogs()
    {
        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("No user is logged in.");
            return;
        }

        CollectionReference logsRef = firestore.Collection("Students").Document(user.UserId).Collection("LogsAR");
        logsRef.OrderByDescending("timestamp").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Failed to fetch user logs.");
                return;
            }

            foreach (Transform child in historyContentParent)
            {
                Destroy(child.gameObject);
            }

            foreach (DocumentSnapshot logDoc in task.Result.Documents)
            {
                Dictionary<string, object> logData = logDoc.ToDictionary();

                string eventType = logData.ContainsKey("eventType") ? logData["eventType"].ToString() : "Unknown Event";
                string contextInfo = "";

                Debug.Log($"Log Entry: {eventType} - Keys: {string.Join(", ", logData.Keys)}");

                // Determine context details based on eventType
                switch (eventType)
                {
                    case "SceneLoaded":
                        if (logData.TryGetValue("sceneName", out var scene))
                        {
                            contextInfo = ": " + logData["sceneName"].ToString();
                        }
                        else
                        {
                            Debug.LogWarning($"Missing 'sceneName' in log {logDoc.Id}. Keys: {string.Join(", ", logData.Keys)}");
                        }
                        break;

                    case "ARSceneOpened":
                        if (logData.TryGetValue("sceneName", out scene))
                        {
                            contextInfo = ": " + logData["sceneName"].ToString();
                        }
                        else
                        {
                            Debug.LogWarning($"Missing 'sceneName' in log {logDoc.Id}. Keys: {string.Join(", ", logData.Keys)}");
                        }
                        break;

                    case "ButtonClick":
                        if (logData.TryGetValue("buttonName", out var button))
                        {
                            contextInfo = ": " + button.ToString();
                        }
                        else
                        {
                            Debug.LogWarning($"Missing 'buttonName' in log {logDoc.Id}");
                        }
                        break;

                    case "AppQuit":
                        // No additional context needed
                        break;

                    default:
                        Debug.Log($"Unhandled eventType: {eventType} in log {logDoc.Id}");
                        break;
                }

                string timestamp = logData.ContainsKey("timestamp")
                    ? ((Timestamp)logData["timestamp"]).ToDateTime().ToLocalTime().ToString("g")
                    : "Unknown Time";

                GameObject entryGO = Instantiate(historyEntryPrefab, historyContentParent);
                TMP_Text entryText = entryGO.GetComponentInChildren<TMP_Text>();
                if (entryText != null)
                    entryText.text = $"{eventType}{contextInfo} - {timestamp}";
            }
        });
    }

    private string ConvertTimestamp(object ts)
    {
        try
        {
            return ((Timestamp)ts).ToDateTime().ToLocalTime().ToString("g");
        }
        catch
        {
            return "Invalid Timestamp";
        }
    }

    System.Collections.IEnumerator LoadProfileImage(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Failed to load profile image: " + uwr.error);
            }
            else
            {
                Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                profileImage.texture = tex;
            }
        }
    }
}
