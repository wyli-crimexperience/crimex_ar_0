using System.Collections;
using UnityEngine;
using TMPro;
using Firebase.Extensions;
using Firebase.Auth;
using Firebase;

public class EmailPassLogin : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField LoginEmail;
    public TMP_InputField loginPassword;
    public TMP_InputField SignupEmail;
    public TMP_InputField SignupPassword;
    public TMP_InputField SignupPasswordConfirm;
    public GameObject loadingScreen;
    public TextMeshProUGUI logTxt;
    public GameObject loginUi, signupUi, SuccessUi;

    [Header("Notification Settings")]
    public FadeTextScript fadeText;
    public float notificationDuration = 3.0f;
    public Color errorColor = Color.red;        //Placeholder colors
    public Color successColor = Color.green;
    public Color warningColor = Color.yellow;

    // Add the NotificationType enum
    private enum NotificationType
    {
        Error,
        Success,
        Warning
    }

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

    //6 characters long for Firebase conditions
    #region Validation Methods
    private const int MIN_PASSWORD_LENGTH = 6;
    private const string PASSWORD_ERROR_MESSAGE = "Password must be at least {0} characters long";      

    private bool ValidatePassword(string password)
    {
        return password.Length >= MIN_PASSWORD_LENGTH;      //verifying password length
    }
    #endregion

    #region Sign-up
    public void SignUp()
    {
        // Input validation
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

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        string email = SignupEmail.text;
        string password = SignupPassword.text;

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync was canceled.");
                loadingScreen.SetActive(false);
                return;
            }

            if (task.IsFaulted)
            {
                FirebaseException firebaseException = task.Exception.GetBaseException() as FirebaseException;
                AuthError error = (AuthError)firebaseException.ErrorCode;

                switch (error)      //Error catching for registering users
                {
                    case AuthError.WeakPassword:
                        ShowNotification("Password is too weak", NotificationType.Error);
                        break;
                    case AuthError.InvalidEmail:
                        ShowNotification("Invalid email address", NotificationType.Error);
                        break;
                    case AuthError.EmailAlreadyInUse:
                        ShowNotification("This email is already in use", NotificationType.Error);
                        break;
                    default:
                        ShowNotification("An error occurred: " + task.Exception.Message, NotificationType.Error);
                        break;
                }

                loadingScreen.SetActive(false);
                return;
            }

            loadingScreen.SetActive(false);
            AuthResult result = task.Result;
            Debug.LogFormat("Firebase user created successfully: {0} ({1})",
                result.User.DisplayName, result.User.UserId);

            SignupEmail.text = "";
            SignupPassword.text = "";
            SignupPasswordConfirm.text = "";

            if (result.User.IsEmailVerified)
            {
                ShowNotification("Sign up Successful", NotificationType.Success);
            }
            else
            {
                ShowNotification("A verification email has been sent to activate your account", NotificationType.Warning);
                SendEmailVerification();
            }
        });
    }

    public void SendEmailVerification()
    {
        StartCoroutine(SendEmailForVerificationAsync());
    }

    IEnumerator SendEmailForVerificationAsync()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null)
        {
            var sendEmailTask = user.SendEmailVerificationAsync();
            yield return new WaitUntil(() => sendEmailTask.IsCompleted);

            if (sendEmailTask.Exception != null)
            {
                FirebaseException firebaseException = sendEmailTask.Exception.GetBaseException() as FirebaseException;
                AuthError error = (AuthError)firebaseException.ErrorCode;

                switch (error)
                {
                    case AuthError.WeakPassword:
                        ShowNotification("Password is too weak", NotificationType.Error);
                        break;
                    case AuthError.InvalidEmail:
                        ShowNotification("Invalid email address", NotificationType.Error);
                        break;
                    case AuthError.EmailAlreadyInUse:
                        ShowNotification("This email is already in use", NotificationType.Error);
                        break;
                    default:
                        ShowNotification("An error occurred: " + sendEmailTask.Exception.Message, NotificationType.Error);
                        break;
                }
            }
        }
    }
    #endregion

    #region Login
    public void Login()
    {
        loadingScreen.SetActive(true);

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        string email = LoginEmail.text;
        string password = loginPassword.text;

        Credential credential = EmailAuthProvider.GetCredential(email, password);
        auth.SignInAndRetrieveDataWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInAndRetrieveDataWithCredentialAsync was canceled.");
                loadingScreen.SetActive(false);
                return;
            }

            if (task.IsFaulted)
            {
                loadingScreen.SetActive(false);
                ShowNotification("Invalid email or password", NotificationType.Error);
                return;
            }

            loadingScreen.SetActive(false);
            AuthResult result = task.Result;
            Debug.LogFormat("User signed in successfully: {0} ({1})",
                result.User.DisplayName, result.User.UserId);

            if (result.User.IsEmailVerified)
            {
                ShowNotification("Log in Successful", NotificationType.Success);

                loginUi.SetActive(false);
                SuccessUi.SetActive(true);
                SuccessUi.transform.Find("Desc").GetComponent<TextMeshProUGUI>().text = "Id: " + result.User.UserId;
            }
            else
            {
                ShowNotification("Please verify your email!", NotificationType.Warning);
            }
        });
    }
    #endregion

    #region Extra
    private void Start()
    {
        // Initialize fadeText if not set in Inspector
        if (fadeText == null)
        {
            fadeText = GetComponent<FadeTextScript>();
        }
    }

    private void ShowValidationError(string message)
    {
        ShowNotification(message, NotificationType.Error);
    }
    #endregion
}