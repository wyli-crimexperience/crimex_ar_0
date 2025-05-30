using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Firebase.Extensions;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using UnityEngine.Events;

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

    [Header("UI Panels")]
    public GameObject loadingScreen;
    public GameObject loginUi;
    public GameObject signupUi;
    public GameObject SuccessUi;

    [Header("Notification System")]
    public TextMeshProUGUI logTxt;
    public FadeTextManager fadeTextManager; // Updated to use new FadeTextManager
    [SerializeField] private string errorFadeProfile = "Error";
    [SerializeField] private string successFadeProfile = "Success";
    [SerializeField] private string warningFadeProfile = "Warning";

    [Header("Notification Colors")]
    public Color errorColor = Color.red;
    public Color successColor = Color.green;
    public Color warningColor = Color.yellow;
    public Color infoColor = Color.white;

    [Header("Success Screen")]
    public TextMeshProUGUI successDescriptionText;

    [Header("Scene Management")]
    public string mainMenuSceneName = "MainMenuScene";
    [SerializeField] private float sceneTransitionDelay = 2.0f;

    [Header("Validation Settings")]
    [SerializeField] private int minPasswordLength = 6;
    [SerializeField] private int maxPasswordLength = 128;
    [SerializeField] private bool requireSpecialCharacters = false;
    [SerializeField] private bool requireNumbers = false;
    [SerializeField] private bool requireUppercase = false;

    [Header("Security Settings")]
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryDelay = 2.0f;
    [SerializeField] private bool enableBruteForceProtection = true;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = false;

    [Header("Events")]
    public UnityEvent OnLoginSuccess;
    public UnityEvent OnSignupSuccess;
    public UnityEvent OnAuthenticationFailed;
    public UnityEvent OnFirebaseInitialized;

    // Private variables
    private int currentRetryAttempts = 0;
    private DateTime lastFailedAttempt;
    private bool isProcessing = false;
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    // Constants
    private const string EMAIL_REGEX = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
    private const string PASSWORD_REQUIREMENTS_MESSAGE = "Password must be at least {0} characters long";
    private const string COLLECTION_STUDENTS = "Students";
    private const string COLLECTION_CLASSES = "Classes";

    private enum NotificationType { Error, Success, Warning, Info }

    #region Unity Lifecycle

    private void Start()
    {
        InitializeComponents();
        SetupUI();
        InitializeFirebaseAndCheckUser();
    }

    private void OnDestroy()
    {
        // Cleanup any running tasks or listeners
        StopAllCoroutines();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Initialize FadeTextManager if not assigned
        if (fadeTextManager == null)
        {
            fadeTextManager = FindFirstObjectByType<FadeTextManager>();
            if (fadeTextManager == null && logTxt != null)
            {
                // Create a FadeTextManager component if one doesn't exist
                fadeTextManager = logTxt.gameObject.AddComponent<FadeTextManager>();
                SetupDefaultFadeProfiles();
            }
        }
        // Validate required components
        ValidateRequiredComponents();
    }

    private void SetupDefaultFadeProfiles()
    {
        if (fadeTextManager == null) return;

        // Add default fade profiles for notifications
        var errorProfile = new FadeProfile
        {
            name = errorFadeProfile,
            duration = 4.0f,
            fadeIn = false,
            fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
        };

        var successProfile = new FadeProfile
        {
            name = successFadeProfile,
            duration = 3.0f,
            fadeIn = false,
            fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
        };

        var warningProfile = new FadeProfile
        {
            name = warningFadeProfile,
            duration = 5.0f,
            fadeIn = false,
            fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
        };

        fadeTextManager.AddProfile(errorProfile);
        fadeTextManager.AddProfile(successProfile);
        fadeTextManager.AddProfile(warningProfile);
    }

    private void ValidateRequiredComponents()
    {
        var missingComponents = new List<string>();

        if (LoginEmail == null) missingComponents.Add("LoginEmail");
        if (loginPassword == null) missingComponents.Add("loginPassword");
        if (SignupEmail == null) missingComponents.Add("SignupEmail");
        if (SignupPassword == null) missingComponents.Add("SignupPassword");
        if (logTxt == null) missingComponents.Add("logTxt");

        if (missingComponents.Count > 0)
        {
            Debug.LogError($"Missing required components: {string.Join(", ", missingComponents)}", this);
        }
    }

    private void SetupUI()
    {
        loadingScreen?.SetActive(true);
        loginUi?.SetActive(false);
        signupUi?.SetActive(false);
        SuccessUi?.SetActive(false);

        // Clear input fields
        ClearAllInputFields();
    }

    #endregion

    #region Firebase Initialization

    private async void InitializeFirebaseAndCheckUser()
    {
        int retryCount = 0;

        while (retryCount < maxRetryAttempts)
        {
            try
            {
                var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
                await dependencyTask;

                if (dependencyTask.Result == DependencyStatus.Available)
                {
                    auth = FirebaseAuth.DefaultInstance;
                    db = FirebaseFirestore.DefaultInstance;

                    OnFirebaseInitialized.Invoke();

                    var user = auth.CurrentUser;
                    if (user != null && user.IsEmailVerified)
                    {
                        if (enableDebugLogging)
                        {
                            Debug.Log("User logged in and verified. Loading MainMenu...");
                        }

                        ShowNotification("Welcome back! Loading main menu...", NotificationType.Success);
                        await Task.Delay(1000);
                        LoadMainMenuScene();
                    }
                    else
                    {
                        if (enableDebugLogging)
                        {
                            Debug.Log("No user logged in or email not verified. Showing login UI...");
                        }

                        ShowLoginUI();
                    }
                    return;
                }
                else
                {
                    retryCount++;
                    ShowNotification($"Firebase initialization failed. Retrying... ({retryCount}/{maxRetryAttempts})", NotificationType.Warning);
                    await Task.Delay((int)(retryDelay * 1000));
                }
            }
            catch (Exception ex)
            {
                retryCount++;
                if (enableDebugLogging)
                {
                    Debug.LogError($"Firebase initialization error: {ex.Message}");
                }

                ShowNotification($"Connection error. Retrying... ({retryCount}/{maxRetryAttempts})", NotificationType.Warning);
                await Task.Delay((int)(retryDelay * 1000));
            }
        }

        ShowNotification("Unable to connect to services. Please check your internet connection and restart the app.", NotificationType.Error);
        loadingScreen?.SetActive(false);
    }

    private void ShowLoginUI()
    {
        loadingScreen?.SetActive(false);
        loginUi?.SetActive(true);
    }

    #endregion

    #region Notification System

    private void ShowNotification(string message, NotificationType type)
    {
        if (logTxt == null) return;

        logTxt.text = message;
        logTxt.color = GetNotificationColor(type);

        // Make sure the text is visible before starting fade
        if (fadeTextManager != null)
        {
            fadeTextManager.SetAlpha(1f); // Ensure text is fully visible

            string profileName = type switch
            {
                NotificationType.Error => errorFadeProfile,
                NotificationType.Success => successFadeProfile,
                NotificationType.Warning => warningFadeProfile,
                _ => successFadeProfile
            };

            fadeTextManager.StartFade(profileName);
        }

        if (enableDebugLogging)
        {
            Debug.Log($"[{type}] {message}");
        }
    }

    private Color GetNotificationColor(NotificationType type) => type switch
    {
        NotificationType.Error => errorColor,
        NotificationType.Success => successColor,
        NotificationType.Warning => warningColor,
        NotificationType.Info => infoColor,
        _ => infoColor
    };

    #endregion

    #region Validation Methods

    private bool ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowNotification("Email address is required", NotificationType.Error);
            return false;
        }

        if (!Regex.IsMatch(email, EMAIL_REGEX))
        {
            ShowNotification("Please enter a valid email address", NotificationType.Error);
            return false;
        }

        return true;
    }

    private bool ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowNotification("Password is required", NotificationType.Error);
            return false;
        }

        if (password.Length < minPasswordLength)
        {
            ShowNotification(string.Format(PASSWORD_REQUIREMENTS_MESSAGE, minPasswordLength), NotificationType.Error);
            return false;
        }

        if (password.Length > maxPasswordLength)
        {
            ShowNotification($"Password must be less than {maxPasswordLength} characters", NotificationType.Error);
            return false;
        }

        if (requireNumbers && !Regex.IsMatch(password, @"\d"))
        {
            ShowNotification("Password must contain at least one number", NotificationType.Error);
            return false;
        }

        if (requireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
        {
            ShowNotification("Password must contain at least one uppercase letter", NotificationType.Error);
            return false;
        }

        if (requireSpecialCharacters && !Regex.IsMatch(password, @"[!@#$%^&*(),.?""{}|<>]"))
        {
            ShowNotification("Password must contain at least one special character", NotificationType.Error);
            return false;
        }

        return true;
    }

    private bool ValidatePasswordMatch(string password, string confirmPassword)
    {
        if (password != confirmPassword)
        {
            ShowNotification("Passwords do not match", NotificationType.Error);
            return false;
        }
        return true;
    }

    private bool CheckBruteForceProtection()
    {
        if (!enableBruteForceProtection) return true;

        if (currentRetryAttempts >= maxRetryAttempts)
        {
            var timeSinceLastAttempt = DateTime.Now - lastFailedAttempt;
            if (timeSinceLastAttempt.TotalMinutes < 5) // 5-minute lockout
            {
                var remainingTime = 5 - (int)timeSinceLastAttempt.TotalMinutes;
                ShowNotification($"Too many failed attempts. Please wait {remainingTime} minutes before trying again.", NotificationType.Error);
                return false;
            }
            else
            {
                currentRetryAttempts = 0; // Reset after cooldown
            }
        }

        return true;
    }

    #endregion

    #region Authentication Methods

    public async void SignUp()
    {
        if (isProcessing) return;

        if (!ValidateSignupInputs()) return;

        isProcessing = true;
        loadingScreen?.SetActive(true);

        try
        {
            string email = SignupEmail.text.Trim();
            string password = SignupPassword.text.Trim();
            string firstName = SignupFirstName.text.Trim();
            string lastName = SignupLastName.text.Trim();

            var result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            string fullName = $"{firstName} {lastName}".Trim();

            // Update user profile
            await result.User.UpdateUserProfileAsync(new UserProfile { DisplayName = fullName });

            // Send verification email
            await result.User.SendEmailVerificationAsync();

            // Create user document in Firestore
            await CreateUserDocument(result.User, fullName);

            // Clear form
            ClearSignupFields();

            ShowNotification("Account created successfully! Please check your email for verification.", NotificationType.Success);
            OnSignupSuccess.Invoke();

        }
        catch (FirebaseException ex)
        {
            HandleFirebaseAuthError(ex);
            OnAuthenticationFailed.Invoke();
        }
        catch (Exception ex)
        {
            ShowNotification("An unexpected error occurred. Please try again.", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError($"Signup error: {ex.Message}");
            }
        }
        finally
        {
            isProcessing = false;
            loadingScreen?.SetActive(false);
        }
    }

    public async void Login()
    {
        if (isProcessing) return;

        if (!CheckBruteForceProtection() || !ValidateLoginInputs()) return;

        isProcessing = true;
        loadingScreen?.SetActive(true);

        try
        {
            string email = LoginEmail.text.Trim();
            string password = loginPassword.text.Trim();

            var result = await auth.SignInWithEmailAndPasswordAsync(email, password);

            if (!result.User.IsEmailVerified)
            {
                ShowNotification("Please verify your email before logging in!", NotificationType.Warning);
                await result.User.SendEmailVerificationAsync();
                ShowNotification("Verification email sent. Please check your inbox.", NotificationType.Info);
                return;
            }

            // Validate class code if provided
            if (!await ValidateClassCode()) return;

            // Update user login data
            await UpdateUserLoginData(result.User);

            // Show success and transition
            ShowNotification("Login successful! Welcome back.", NotificationType.Success);
            ShowSuccessUI(result.User);

            OnLoginSuccess.Invoke();

            // Reset retry attempts on successful login
            currentRetryAttempts = 0;

            // Delay before scene transition
            await Task.Delay((int)(sceneTransitionDelay * 1000));
            LoadMainMenuScene();

        }
        catch (FirebaseException ex)
        {
            HandleFirebaseAuthError(ex);
            RecordFailedAttempt();
            OnAuthenticationFailed.Invoke();
        }
        catch (Exception ex)
        {
            ShowNotification("Login failed. Please try again.", NotificationType.Error);
            RecordFailedAttempt();
            if (enableDebugLogging)
            {
                Debug.LogError($"Login error: {ex.Message}");
            }
        }
        finally
        {
            isProcessing = false;
            loadingScreen?.SetActive(false);
        }
    }

    #endregion

    #region Helper Methods

    private bool ValidateSignupInputs()
    {
        return ValidateEmail(SignupEmail.text) &&
               ValidatePassword(SignupPassword.text) &&
               ValidatePasswordMatch(SignupPassword.text, SignupPasswordConfirm.text);
    }

    private bool ValidateLoginInputs()
    {
        return ValidateEmail(LoginEmail.text) &&
               !string.IsNullOrWhiteSpace(loginPassword.text);
    }

    private async Task<bool> ValidateClassCode()
    {
        if (classCodeInput == null || string.IsNullOrWhiteSpace(classCodeInput.text))
        {
            return true; // Class code is optional
        }

        try
        {
            string classCode = classCodeInput.text.Trim();
            DocumentReference classDocRef = db.Collection(COLLECTION_CLASSES).Document(classCode);
            DocumentSnapshot classSnapshot = await classDocRef.GetSnapshotAsync();

            if (!classSnapshot.Exists)
            {
                ShowNotification("Invalid class code. Please check and try again.", NotificationType.Error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowNotification("Error validating class code. Please try again.", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError($"Class code validation error: {ex.Message}");
            }
            return false;
        }
    }

    private async Task CreateUserDocument(FirebaseUser user, string displayName)
    {
        string[] userTypes = { "Student", "Teacher", "Faculty Staff" };
        string selectedUserType = userTypes[Mathf.Clamp(userTypeDropdown.value, 0, userTypes.Length - 1)];

        var userData = new Dictionary<string, object>
        {
            { "displayName", string.IsNullOrWhiteSpace(displayName) ? "Guest" : displayName },
            { "email", user.Email },
            { "userType", selectedUserType },
            { "profileImageUrl", user.PhotoUrl?.ToString() ?? "" },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "emailVerified", user.IsEmailVerified }
        };

        DocumentReference docRef = db.Collection(COLLECTION_STUDENTS).Document(user.UserId);
        await docRef.SetAsync(userData, SetOptions.MergeAll);
    }

    private async Task UpdateUserLoginData(FirebaseUser user)
    {
        var updateData = new Dictionary<string, object>
        {
            { "lastLogin", Timestamp.GetCurrentTimestamp() },
            { "emailVerified", user.IsEmailVerified }
        };

        if (!string.IsNullOrWhiteSpace(classCodeInput?.text))
        {
            updateData["classCode"] = classCodeInput.text.Trim();
        }

        DocumentReference userDocRef = db.Collection(COLLECTION_STUDENTS).Document(user.UserId);
        await userDocRef.SetAsync(updateData, SetOptions.MergeAll);
    }

    private void ShowSuccessUI(FirebaseUser user)
    {
        loginUi?.SetActive(false);
        SuccessUi?.SetActive(true);

        if (successDescriptionText != null)
        {
            successDescriptionText.text = $"Welcome, {user.DisplayName ?? user.Email}!\nUser ID: {user.UserId}";
        }
    }

    private void RecordFailedAttempt()
    {
        currentRetryAttempts++;
        lastFailedAttempt = DateTime.Now;
    }

    private void HandleFirebaseAuthError(FirebaseException ex)
    {
        var error = (AuthError)ex.ErrorCode;
        string message = GetErrorMessage(error);
        ShowNotification(message, NotificationType.Error);

        if (enableDebugLogging)
        {
            Debug.LogError($"Firebase Auth Error: {error} - {ex.Message}");
        }
    }

    private string GetErrorMessage(AuthError error) => error switch
    {
        // Common authentication errors
        AuthError.WeakPassword => "Password is too weak. Please use a stronger password.",
        AuthError.InvalidEmail => "Please enter a valid email address.",
        AuthError.EmailAlreadyInUse => "This email address is already registered.",
        AuthError.UserNotFound => "No account found with this email address.",
        AuthError.WrongPassword => "Incorrect password. Please try again.",

        // Rate limiting and security
        AuthError.TooManyRequests => "Too many failed attempts. Please try again later.",
        AuthError.UserDisabled => "This account has been disabled. Please contact support.",
        AuthError.RequiresRecentLogin => "For security, please log in again to continue.",

        // Network and connection issues
        AuthError.NetworkRequestFailed => "Network error. Please check your internet connection.",

        // Session and token issues
        AuthError.InvalidUserToken or
        AuthError.UserTokenExpired => "Your session has expired. Please log in again.",

        // Missing required fields
        AuthError.MissingEmail => "Email address is required.",
        AuthError.MissingPassword => "Password is required.",

        // Configuration issues
        AuthError.InvalidApiKey => "Configuration error. Please contact support.",
        AuthError.AppNotAuthorized => "This app is not authorized. Please contact support.",
        AuthError.OperationNotAllowed => "This operation is not allowed. Please contact support.",

        // Verification issues
        AuthError.InvalidVerificationCode => "Invalid verification code. Please try again.",
        AuthError.InvalidVerificationId => "Verification failed. Please try again.",
        AuthError.UnverifiedEmail => "Please verify your email address before continuing.",

        // Generic fallback
        _ => "Authentication failed. Please try again or contact support if the problem persists."
    };

    private void ClearAllInputFields()
    {
        ClearLoginFields();
        ClearSignupFields();
    }

    private void ClearLoginFields()
    {
        if (LoginEmail != null) LoginEmail.text = "";
        if (loginPassword != null) loginPassword.text = "";
        if (classCodeInput != null) classCodeInput.text = "";
    }

    private void ClearSignupFields()
    {
        if (SignupEmail != null) SignupEmail.text = "";
        if (SignupPassword != null) SignupPassword.text = "";
        if (SignupPasswordConfirm != null) SignupPasswordConfirm.text = "";
        if (SignupFirstName != null) SignupFirstName.text = "";
        if (SignupLastName != null) SignupLastName.text = "";
    }

    private void LoadMainMenuScene()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogError("Main menu scene name is not set!");
        }
    }

    #endregion

    #region Public Methods

    public void SwitchToSignup()
    {
        loginUi?.SetActive(false);
        signupUi?.SetActive(true);
        ClearAllInputFields();
    }

    public void SwitchToLogin()
    {
        signupUi?.SetActive(false);
        loginUi?.SetActive(true);
        ClearAllInputFields();
    }

    public async void SendPasswordResetEmail()
    {
        if (!ValidateEmail(LoginEmail.text)) return;

        try
        {
            await auth.SendPasswordResetEmailAsync(LoginEmail.text.Trim());
            ShowNotification("Password reset email sent. Please check your inbox.", NotificationType.Success);
        }
        catch (FirebaseException ex)
        {
            HandleFirebaseAuthError(ex);
        }
    }

    public void Logout()
    {
        if (auth != null)
        {
            auth.SignOut();
            ShowNotification("Logged out successfully.", NotificationType.Info);
        }
    }

    #endregion
}