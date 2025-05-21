using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Firebase.Extensions;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class EmailPassLogin : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField LoginEmail;
    public TMP_InputField loginPassword;
    public TMP_InputField SignupEmail;
    public TMP_InputField SignupPassword;
    public TMP_InputField SignupPasswordConfirm;

    public TMP_InputField SignupFirstName;
    public TMP_InputField SignupLastName;
    public TMP_Dropdown userTypeDropdown;
    public TMP_InputField classCodeInput;

    public GameObject loadingScreen;
    public GameObject loginUi;
    public GameObject signupUi;
    public GameObject SuccessUi;
    public TextMeshProUGUI logTxt;

    [Header("Notification Settings")]
    public FadeTextScript fadeText;
    public float notificationDuration = 3.0f;
    public Color errorColor = Color.red;
    public Color successColor = Color.green;
    public Color warningColor = Color.yellow;

    [Header("Success Description")]
    public TextMeshProUGUI successDescriptionText;

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenuScene";

    private const int MIN_PASSWORD_LENGTH = 6;
    private const string PASSWORD_ERROR_MESSAGE = "Password must be at least {0} characters long";
    private const int MaxRetryAttempts = 3;

    private enum NotificationType { Error, Success, Warning }

    private void ShowNotification(string message, NotificationType type)
    {
        logTxt.text = message;
        logTxt.color = type switch
        {
            NotificationType.Error => errorColor,
            NotificationType.Success => successColor,
            NotificationType.Warning => warningColor,
            _ => Color.white
        };
        fadeText.StartFade();
    }

    private bool ValidatePassword(string password) => password.Length >= MIN_PASSWORD_LENGTH;

    private void Start()
    {
        if (fadeText == null)
        {
            fadeText = GetComponent<FadeTextScript>();
        }
        loadingScreen.SetActive(true);
        loginUi.SetActive(false);
        signupUi.SetActive(false);
        SuccessUi.SetActive(false);
        InitializeFirebaseAndCheckUser();
    }

    private async void InitializeFirebaseAndCheckUser()
    {
        int retryCount = 0;
        const int maxRetries = 3;

        while (retryCount < maxRetries)
        {
            var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
            await dependencyTask;

            if (dependencyTask.Result == DependencyStatus.Available)
            {
                FirebaseAuth auth = FirebaseAuth.DefaultInstance;
                var user = auth.CurrentUser;

                if (user != null && user.IsEmailVerified)
                {
                    Debug.Log("User logged in and verified. Loading MainMenu...");
                    SceneManager.LoadScene(mainMenuSceneName);
                }
                else
                {
                    Debug.Log("No user logged in or email not verified. Showing login UI...");
                    loadingScreen.SetActive(false);
                    loginUi.SetActive(true);
                }
                return;
            }
            else
            {
                retryCount++;
                ShowNotification($"Firebase init failed: {dependencyTask.Result}. Retrying... ({retryCount}/{maxRetries})", NotificationType.Warning);
                await Task.Delay(2000); // Wait before retry
            }
        }

        ShowNotification("Unable to initialize Firebase. Please check your internet connection or reinstall the app.", NotificationType.Error);
        loadingScreen.SetActive(false);
    }

    public async void SignUp()
    {
        if (!ValidatePassword(SignupPassword.text))
        {
            ShowNotification(string.Format(PASSWORD_ERROR_MESSAGE, MIN_PASSWORD_LENGTH), NotificationType.Error);
            return;
        }

        if (SignupPassword.text != SignupPasswordConfirm.text)
        {
            ShowNotification("Passwords do not match", NotificationType.Error);
            return;
        }

        loadingScreen.SetActive(true);

        try
        {
            var auth = FirebaseAuth.DefaultInstance;
            var result = await auth.CreateUserWithEmailAndPasswordAsync(SignupEmail.text.Trim(), SignupPassword.text.Trim());

            string fullName = $"{SignupFirstName.text.Trim()} {SignupLastName.text.Trim()}".Trim();

            await result.User.UpdateUserProfileAsync(new UserProfile { DisplayName = fullName });

            SignupEmail.text = SignupPassword.text = SignupPasswordConfirm.text = "";
            SignupFirstName.text = SignupLastName.text = "";

            if (result.User.IsEmailVerified)
            {
                ShowNotification("Sign up Successful", NotificationType.Success);
            }
            else
            {
                ShowNotification("A verification email has been sent to activate your account", NotificationType.Warning);
                await result.User.SendEmailVerificationAsync();
            }

            string[] userTypes = { "Student", "Teacher", "Faculty Staff" };
            string selectedUserType = userTypes[Mathf.Clamp(userTypeDropdown.value, 0, userTypes.Length - 1)];

            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            DocumentReference docRef = db.Collection("Students").Document(result.User.UserId);

            Dictionary<string, object> userData = new Dictionary<string, object>
            {
                { "displayName", string.IsNullOrWhiteSpace(fullName) ? "Guest" : fullName },
                { "email", result.User.Email },
                { "userType", selectedUserType },
                { "profileImageUrl", result.User.PhotoUrl?.ToString() ?? "" },
                { "createdAt", Timestamp.GetCurrentTimestamp() }
            };

            await docRef.SetAsync(userData, SetOptions.MergeAll);
        }
        catch (FirebaseException ex)
        {
            var error = (AuthError)ex.ErrorCode;
            ShowNotification(GetErrorMessage(error), NotificationType.Error);
        }
        finally
        {
            loadingScreen.SetActive(false);
        }
    }

    private string GetErrorMessage(AuthError error) => error switch
    {
        AuthError.WeakPassword => "Password is too weak",
        AuthError.InvalidEmail => "Invalid email address",
        AuthError.EmailAlreadyInUse => "This email is already in use",
        _ => "Wrong pass code or an unknown error occurred"
    };

    public async void Login()
    {
        loadingScreen.SetActive(true);
        try
        {
            var auth = FirebaseAuth.DefaultInstance;
            var result = await auth.SignInWithEmailAndPasswordAsync(LoginEmail.text.Trim(), loginPassword.text.Trim());

            if (result.User.IsEmailVerified)
            {
                string enteredClassCode = classCodeInput.text.Trim();
                FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
                DocumentReference classDocRef = db.Collection("Classes").Document(enteredClassCode);
                DocumentSnapshot classSnapshot = await classDocRef.GetSnapshotAsync();

                if (!classSnapshot.Exists)
                {
                    ShowNotification("Invalid class code. Please try again.", NotificationType.Error);
                    loadingScreen.SetActive(false);
                    return;
                }

                ShowNotification("Log in Successful", NotificationType.Success);
                loginUi.SetActive(false);
                SuccessUi.SetActive(true);
                successDescriptionText.text = "Id: " + result.User.UserId;

                DocumentReference userDocRef = db.Collection("Students").Document(result.User.UserId);
                await userDocRef.SetAsync(new Dictionary<string, object>
                {
                    { "classCode", enteredClassCode },
                    { "lastLogin", Timestamp.GetCurrentTimestamp() }
                }, SetOptions.MergeAll);

                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                ShowNotification("Please verify your email!", NotificationType.Warning);
                loadingScreen.SetActive(false);
            }
        }
        catch (FirebaseException ex)
        {
            var error = (AuthError)ex.ErrorCode;
            ShowNotification(GetErrorMessage(error), NotificationType.Error);
            loadingScreen.SetActive(false);
        }
    }
}
