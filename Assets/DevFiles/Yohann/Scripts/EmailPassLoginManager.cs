using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Firebase.Extensions;
using Firebase.Auth;
using Firebase.Firestore;
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
    public Color errorColor = Color.red;
    public Color successColor = Color.green;
    public Color warningColor = Color.yellow;

    [Header("Success Description")]
    public TextMeshProUGUI successDescriptionText;

    private const int MIN_PASSWORD_LENGTH = 6;
    private const string PASSWORD_ERROR_MESSAGE = "Password must be at least {0} characters long";

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

            SignupEmail.text = SignupPassword.text = SignupPasswordConfirm.text = "";

            if (result.User.IsEmailVerified)
            {
                ShowNotification("Sign up Successful", NotificationType.Success);
            }
            else
            {
                ShowNotification("A verification email has been sent to activate your account", NotificationType.Warning);
                await result.User.SendEmailVerificationAsync();
            }

            // Firestore user profile creation
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            DocumentReference docRef = db.Collection("users").Document(result.User.UserId);

            Dictionary<string, object> userData = new Dictionary<string, object>
            {
                { "displayName", result.User.DisplayName ?? "Guest" },
                { "email", result.User.Email },
                { "userType", "Registered" },
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
                ShowNotification("Log in Successful", NotificationType.Success);
                loginUi.SetActive(false);
                SuccessUi.SetActive(true);
                successDescriptionText.text = "Id: " + result.User.UserId;

                // Firestore user profile update
                FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
                DocumentReference docRef = db.Collection("Students").Document(result.User.UserId);

                Dictionary<string, object> userData = new Dictionary<string, object>
                {
                    { "displayName", result.User.DisplayName ?? "Guest" },
                    { "email", result.User.Email },
                    { "userType", "Registered" },
                    { "profileImageUrl", result.User.PhotoUrl?.ToString() ?? "" },
                    { "lastLogin", Timestamp.GetCurrentTimestamp() }
                };

                await docRef.SetAsync(userData, SetOptions.MergeAll);
            }
            else
            {
                ShowNotification("Please verify your email!", NotificationType.Warning);
            }
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

    private void Start()
    {
        if (fadeText == null)
        {
            fadeText = GetComponent<FadeTextScript>();
        }
    }
}
