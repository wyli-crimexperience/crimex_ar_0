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
using System.Linq;
using static StatisticsManager;

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
    public TMP_InputField SignupCompany; // NEW: Company input field
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

    [Header("Company Settings")]
    [SerializeField] private bool requireCompanyInput = true; // NEW: Option to make company field required
    [SerializeField] private string defaultCompany = "University of the Cordilleras"; // NEW: Default company if not provided

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
    private const string EMAIL_REGEX = @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$";
    private const string PASSWORD_REQUIREMENTS_MESSAGE = "Password must be at least {0} characters long";
    private const string COLLECTION_USERS = "users";
    private const string COLLECTION_CLASSES = "classes";
    private const string COMPANY_NAME = "University of the Cordilleras"; // This is now used as fallback

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
        // Cancel any pending tasks
        if (auth != null)
        {
            auth = null;
        }

        if (db != null)
        {
            db = null;
        }

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

    // NEW: Company validation method
    private bool ValidateCompany(string company)
    {
        if (requireCompanyInput && string.IsNullOrWhiteSpace(company))
        {
            ShowNotification("Company name is required", NotificationType.Error);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(company) && company.Trim().Length < 2)
        {
            ShowNotification("Company name must be at least 2 characters long", NotificationType.Error);
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
            string company = GetCompanyValue(); // NEW: Get company value

            var result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            string fullName = $"{firstName} {lastName}".Trim();

            // Update user profile
            await result.User.UpdateUserProfileAsync(new UserProfile { DisplayName = fullName });

            // Send verification email
            await result.User.SendEmailVerificationAsync();

            // Create user document in Firestore with new schema
            await CreateUserDocument(result.User, firstName, lastName, company);

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

        // Check brute force protection and basic input validation
        if (!CheckBruteForceProtection() || !ValidateLoginInputs()) return;

        // ✅ VALIDATE CLASS CODE FIRST (before authentication)
        if (!await ValidateClassCode())
        {
            // Class code validation failed - don't proceed with authentication
            return;
        }

        isProcessing = true;
        loadingScreen?.SetActive(true);

        try
        {
            string email = LoginEmail.text.Trim();
            string password = loginPassword.text.Trim();

            // Now authenticate only if class code is valid (or empty/optional)
            var result = await auth.SignInWithEmailAndPasswordAsync(email, password);

            // Check email verification
            if (!result.User.IsEmailVerified)
            {
                ShowNotification("Please verify your email before logging in!", NotificationType.Warning);
                await result.User.SendEmailVerificationAsync();
                ShowNotification("Verification email sent. Please check your inbox.", NotificationType.Info);
                return;
            }

            // Update user login data and handle class enrollment
            await UpdateUserLoginData(result.User);

            // Show success notification and UI
            ShowNotification("Login successful! Welcome back.", NotificationType.Success);
            await ShowSuccessUI(result.User);

            // Invoke success event
            OnLoginSuccess.Invoke();

            // Reset retry attempts on successful login
            currentRetryAttempts = 0;

            // Delay before scene transition for better UX
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
        if (string.IsNullOrWhiteSpace(SignupFirstName?.text))
        {
            ShowNotification("First name is required", NotificationType.Error);
            return false;
        }

        if (string.IsNullOrWhiteSpace(SignupLastName?.text))
        {
            ShowNotification("Last name is required", NotificationType.Error);
            return false;
        }

        return ValidateEmail(SignupEmail.text) &&
               ValidatePassword(SignupPassword.text) &&
               ValidatePasswordMatch(SignupPassword.text, SignupPasswordConfirm.text) &&
               ValidateCompany(SignupCompany?.text); // NEW: Added company validation
    }

    // NEW: Helper method to get company value with fallback
    private string GetCompanyValue()
    {
        string companyInput = SignupCompany?.text?.Trim();

        if (!string.IsNullOrWhiteSpace(companyInput))
        {
            return companyInput;
        }

        return !string.IsNullOrWhiteSpace(defaultCompany) ? defaultCompany : COMPANY_NAME;
    }

    private bool ValidateLoginInputs()
    {
        return ValidateEmail(LoginEmail.text) &&
               !string.IsNullOrWhiteSpace(loginPassword.text);
    }

    private async Task<bool> ValidateClassCode()
    {
        // If no class code input field or empty input, treat as optional
        if (classCodeInput == null || string.IsNullOrWhiteSpace(classCodeInput.text))
        {
            if (enableDebugLogging)
            {
                Debug.Log("No class code provided - proceeding without class enrollment");
            }
            return true; // Allow login without class code
        }

        // Check if database is initialized
        if (db == null)
        {
            ShowNotification("Database not initialized. Please try again.", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError("Firestore database is null during class code validation");
            }
            return false;
        }

        try
        {
            string classCode = classCodeInput.text.Trim();

            if (enableDebugLogging)
            {
                Debug.Log($"Validating class code: {classCode}");
            }

            // ✅ QUERY BY CODE FIELD (since document ID is random)
            Query query = db.Collection("classes")
                .WhereEqualTo("code", classCode)
                .Limit(1); // Only need to find one matching document

            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();

            // Check if any documents were found
            if (querySnapshot.Count == 0)
            {
                ShowNotification("Invalid class code. Please check and try again.", NotificationType.Error);
                if (enableDebugLogging)
                {
                    Debug.LogWarning($"Class code '{classCode}' not found in database");
                }
                return false;
            }

            // Get the first (and should be only) matching document
            DocumentSnapshot classSnapshot = querySnapshot.Documents.First();

            // Log the found class for debugging
            if (enableDebugLogging)
            {
                string className = classSnapshot.ContainsField("name") ? classSnapshot.GetValue<string>("name") : "Unknown";
                string documentId = classSnapshot.Id;
                Debug.Log($"Found class: {className} with code: {classCode} (Document ID: {documentId})");
            }

            // Check if class has expired (due date validation)
            if (classSnapshot.ContainsField("dueDate"))
            {
                var dueDate = classSnapshot.GetValue<Timestamp>("dueDate");
                if (dueDate.ToDateTime() < DateTime.UtcNow)
                {
                    ShowNotification("This class has expired and is no longer accepting new students.", NotificationType.Warning);
                    if (enableDebugLogging)
                    {
                        Debug.LogWarning($"Class '{classCode}' has expired. Due date: {dueDate.ToDateTime()}");
                    }
                    return false;
                }
            }

            // Check if class is active/enabled
            if (classSnapshot.ContainsField("isActive"))
            {
                bool isActive = classSnapshot.GetValue<bool>("isActive");
                if (!isActive)
                {
                    ShowNotification("This class is currently inactive and not accepting new students.", NotificationType.Warning);
                    if (enableDebugLogging)
                    {
                        Debug.LogWarning($"Class '{classCode}' is inactive");
                    }
                    return false;
                }
            }

            // Check enrollment capacity if available
            if (classSnapshot.ContainsField("maxStudents") && classSnapshot.ContainsField("currentEnrollment"))
            {
                int maxStudents = classSnapshot.GetValue<int>("maxStudents");
                int currentEnrollment = classSnapshot.GetValue<int>("currentEnrollment");

                if (currentEnrollment >= maxStudents)
                {
                    ShowNotification("This class is full and cannot accept more students.", NotificationType.Warning);
                    if (enableDebugLogging)
                    {
                        Debug.LogWarning($"Class '{classCode}' is full. {currentEnrollment}/{maxStudents} students enrolled");
                    }
                    return false;
                }
            }

            // Check for auto-assessment setting (informational only)
            if (classSnapshot.ContainsField("autoAssessOnLogin"))
            {
                bool autoAssess = classSnapshot.GetValue<bool>("autoAssessOnLogin");
                if (autoAssess && enableDebugLogging)
                {
                    Debug.Log($"Auto-assessment is enabled for class: {classCode}");
                }
            }

            if (enableDebugLogging)
            {
                Debug.Log($"Class code '{classCode}' validation successful");
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowNotification("Error validating class code. Please try again.", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError($"Class code validation error: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
            return false;
        }
    }
    private async Task CreateUserDocument(FirebaseUser user, string firstName, string lastName, string company)
    {
        string[] userTypes = { "Student", "Teacher", "Faculty Staff" };
        string selectedUserType = userTypes[Mathf.Clamp(userTypeDropdown.value, 0, userTypes.Length - 1)];

        var userData = new Dictionary<string, object>
        {
            { "company", company }, // NEW: Use the provided company value
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "email", user.Email },
            { "enrolledClasses", new List<string>() }, // Empty array initially
            { "firstName", firstName },
            { "lastName", lastName },
            { "role", selectedUserType },
            { "uid", user.UserId }
        };

        DocumentReference docRef = db.Collection(COLLECTION_USERS).Document(user.UserId);
        await docRef.SetAsync(userData, SetOptions.MergeAll);

        if (enableDebugLogging)
        {
            Debug.Log($"User document created for {firstName} {lastName} at {company} with role {selectedUserType}");
        }
    }

    private async Task UpdateUserLoginData(FirebaseUser user)
    {
        if (user == null || db == null) return;

        var updateData = new Dictionary<string, object>
    {
        { "lastLogin", Timestamp.GetCurrentTimestamp() }
    };

        // If class code is provided, handle enrollment
        if (!string.IsNullOrWhiteSpace(classCodeInput?.text))
        {
            string classCode = classCodeInput.text.Trim();

            // Add student to class and update user's enrolled classes
            bool enrollmentSuccess = await EnrollStudentInClass(user.UserId, classCode);

            if (enrollmentSuccess)
            {
                // Update user's enrolledClasses array
                DocumentReference userDocRef = db.Collection(COLLECTION_USERS).Document(user.UserId);
                DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

                if (userSnapshot.Exists)
                {
                    var enrolledClasses = new List<string>();

                    // Get existing enrolled classes
                    if (userSnapshot.ContainsField("enrolledClasses"))
                    {
                        var existingClasses = userSnapshot.GetValue<List<object>>("enrolledClasses");
                        enrolledClasses = existingClasses?.Select(c => c.ToString()).ToList() ?? new List<string>();
                    }

                    // Add new class if not already enrolled
                    if (!enrolledClasses.Contains(classCode))
                    {
                        enrolledClasses.Add(classCode);
                        updateData["enrolledClasses"] = enrolledClasses;

                        if (enableDebugLogging)
                        {
                            Debug.Log($"Adding class {classCode} to user's enrolled classes");
                        }
                    }
                }
            }
        }

        DocumentReference docRef = db.Collection(COLLECTION_USERS).Document(user.UserId);
        await docRef.SetAsync(updateData, SetOptions.MergeAll);
    }
    private async Task<bool> EnrollStudentInClass(string studentId, string classCode)
    {
        try
        {
            // ✅ FIND CLASS BY CODE FIELD (not document ID)
            Query query = db.Collection(COLLECTION_CLASSES)
                .WhereEqualTo("code", classCode)
                .Limit(1);

            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();

            if (querySnapshot.Count == 0)
            {
                ShowNotification("Class not found.", NotificationType.Error);
                return false;
            }

            DocumentSnapshot classSnapshot = querySnapshot.Documents.First();
            DocumentReference classDocRef = classSnapshot.Reference; // Get the actual document reference

            // Get current students array
            var currentStudents = new List<string>();
            if (classSnapshot.ContainsField("students"))
            {
                var existingStudents = classSnapshot.GetValue<List<object>>("students");
                currentStudents = existingStudents?.Select(s => s.ToString()).ToList() ?? new List<string>();
            }

            // Check if student is already enrolled
            if (currentStudents.Contains(studentId))
            {
                ShowNotification("You are already enrolled in this class.", NotificationType.Warning);
                return true; // Return true because student is enrolled
            }

            // Add student to the class
            currentStudents.Add(studentId);
            await classDocRef.UpdateAsync("students", currentStudents);

            // Get class name for notification
            string className = classSnapshot.ContainsField("name")
                ? classSnapshot.GetValue<string>("name")
                : classCode;

            ShowNotification($"Successfully enrolled in {className}!", NotificationType.Success);

            if (enableDebugLogging)
            {
                Debug.Log($"Student {studentId} enrolled in class {classCode} ({className})");
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowNotification("Failed to enroll in class. Please try again.", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError($"Class enrollment error: {ex.Message}");
            }
            return false;
        }
    }
    public async Task<ClassData> GetClassData(string classCode)
    {
        try
        {
            // ✅ FIND CLASS BY CODE FIELD
            Query query = db.Collection(COLLECTION_CLASSES)
                .WhereEqualTo("code", classCode)
                .Limit(1);

            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();

            if (querySnapshot.Count > 0)
            {
                DocumentSnapshot classSnapshot = querySnapshot.Documents.First();

                return new ClassData
                {
                    code = classCode,
                    documentId = classSnapshot.Id, // Store the actual document ID
                    name = classSnapshot.GetValue<string>("name"),
                    description = classSnapshot.GetValue<string>("description"),
                    instructorId = classSnapshot.GetValue<string>("instructorId"),
                    assignmentType = classSnapshot.GetValue<string>("assignmentType"),
                    type = classSnapshot.GetValue<string>("type"),
                    title = classSnapshot.GetValue<string>("title"),
                    linkedCrimeSceneId = classSnapshot.GetValue<string>("linkedCrimeSceneId"),
                    linkedCrimeSceneName = classSnapshot.GetValue<string>("linkedCrimeSceneName"),
                    dueDate = classSnapshot.ContainsField("dueDate") ? classSnapshot.GetValue<Timestamp>("dueDate") : null,
                    autoAssessOnLogin = classSnapshot.ContainsField("autoAssessOnLogin") ? classSnapshot.GetValue<bool>("autoAssessOnLogin") : false,
                    createdAt = classSnapshot.ContainsField("createdAt") ? classSnapshot.GetValue<Timestamp>("createdAt") : null,
                    students = classSnapshot.ContainsField("students")
                        ? classSnapshot.GetValue<List<object>>("students")?.Select(s => s.ToString()).ToList() ?? new List<string>()
                        : new List<string>()
                };
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"Error getting class data: {ex.Message}");
            }
        }

        return null;
    }
    public async Task<bool> IsUserEnrolledInClass(string classCode)
    {
        if (auth.CurrentUser == null) return false;

        try
        {
            var classData = await GetClassData(classCode);
            return classData?.students?.Contains(auth.CurrentUser.UserId) ?? false;
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"Error checking class enrollment: {ex.Message}");
            }
            return false;
        }
    }

    // NEW: Method to get all classes a user is enrolled in with full class data
    public async Task<List<ClassData>> GetUserEnrolledClassesData()
    {
        if (auth.CurrentUser == null) return new List<ClassData>();

        try
        {
            var enrolledClasses = await GetEnrolledClasses();
            var classDataList = new List<ClassData>();

            foreach (string classCode in enrolledClasses)
            {
                var classData = await GetClassData(classCode);
                if (classData != null)
                {
                    classDataList.Add(classData);
                }
            }

            return classDataList;
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"Error getting user enrolled classes data: {ex.Message}");
            }
            return new List<ClassData>();
        }
    }

    private async Task<UserData> GetUserData(string userId)
    {
        try
        {
            DocumentReference userDocRef = db.Collection(COLLECTION_USERS).Document(userId);
            DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

            if (userSnapshot.Exists)
            {
                return new UserData
                {
                    company = userSnapshot.GetValue<string>("company"),
                    email = userSnapshot.GetValue<string>("email"),
                    firstName = userSnapshot.GetValue<string>("firstName"),
                    lastName = userSnapshot.GetValue<string>("lastName"),
                    role = userSnapshot.GetValue<string>("role"),
                    uid = userSnapshot.GetValue<string>("uid"),
                    enrolledClasses = userSnapshot.ContainsField("enrolledClasses")
                        ? userSnapshot.GetValue<List<object>>("enrolledClasses")?.Select(c => c.ToString()).ToList() ?? new List<string>()
                        : new List<string>()
                };
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"Error getting user data: {ex.Message}");
            }
        }

        return null;
    }

    private async Task ShowSuccessUI(FirebaseUser user)
    {
        loginUi?.SetActive(false);
        SuccessUi?.SetActive(true);

        if (successDescriptionText != null)
        {
            // Try to get user data from Firestore for complete information
            var userData = await GetUserData(user.UserId);

            string displayName;
            string companyName = "";

            if (userData != null)
            {
                displayName = $"{userData.firstName} {userData.lastName}".Trim();
                companyName = !string.IsNullOrWhiteSpace(userData.company) ? $"\nCompany: {userData.company}" : "";
            }
            else
            {
                displayName = user.DisplayName ?? user.Email;
            }

            successDescriptionText.text = $"Welcome, {displayName}!{companyName}\nUser ID: {user.UserId}";
        }
    }

    private void RecordFailedAttempt()
    {
        currentRetryAttempts++;
        lastFailedAttempt = DateTime.Now;
    }

    private void HandleFirebaseAuthError(FirebaseException ex)
    {
        if (ex == null) return;

        var error = (AuthError)ex.ErrorCode;
        string message = GetErrorMessage(error);
        ShowNotification(message, NotificationType.Error);

        // Log the full exception for debugging
        if (enableDebugLogging)
        {
            Debug.LogError($"Firebase Auth Error: {error} - {ex.Message}\nStack Trace: {ex.StackTrace}");
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
        if (SignupCompany != null) SignupCompany.text = ""; // NEW: Clear company field
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

    // New method to handle class enrollment
    public async Task<bool> EnrollInClass(string classCode)
    {
        if (auth.CurrentUser == null)
        {
            ShowNotification("Please log in to enroll in classes.", NotificationType.Error);
            return false;
        }

        return await EnrollStudentInClass(auth.CurrentUser.UserId, classCode);
    }

    // New method to unenroll from a class
    public async Task<bool> UnenrollFromClass(string classCode)
    {
        if (auth.CurrentUser == null) return false;

        try
        {
            string userId = auth.CurrentUser.UserId;

            // Remove from class document
            DocumentReference classDocRef = db.Collection(COLLECTION_CLASSES).Document(classCode);
            DocumentSnapshot classSnapshot = await classDocRef.GetSnapshotAsync();

            if (classSnapshot.Exists && classSnapshot.ContainsField("students"))
            {
                var currentStudents = classSnapshot.GetValue<List<object>>("students");
                var studentsList = currentStudents?.Select(s => s.ToString()).ToList() ?? new List<string>();

                if (studentsList.Contains(userId))
                {
                    studentsList.Remove(userId);
                    await classDocRef.UpdateAsync("students", studentsList);
                }
            }

            // Remove from user document
            DocumentReference userDocRef = db.Collection(COLLECTION_USERS).Document(userId);
            DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

            if (userSnapshot.Exists && userSnapshot.ContainsField("enrolledClasses"))
            {
                var existingClasses = userSnapshot.GetValue<List<object>>("enrolledClasses");
                var enrolledClasses = existingClasses?.Select(c => c.ToString()).ToList() ?? new List<string>();

                if (enrolledClasses.Contains(classCode))
                {
                    enrolledClasses.Remove(classCode);
                    await userDocRef.UpdateAsync("enrolledClasses", enrolledClasses);

                    ShowNotification($"Successfully unenrolled from class: {classCode}", NotificationType.Success);
                    return true;
                }
            }

            ShowNotification("Not enrolled in this class", NotificationType.Warning);
            return false;
        }
        catch (Exception ex)
        {
            ShowNotification("Failed to unenroll from class", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError($"Class unenrollment error: {ex.Message}");
            }
            return false;
        }
    }
    // Get current user's enrolled classes
    public async Task<List<string>> GetEnrolledClasses()
    {
        if (auth.CurrentUser == null) return new List<string>();

        try
        {
            var userData = await GetUserData(auth.CurrentUser.UserId);
            return userData?.enrolledClasses ?? new List<string>();
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"Error getting enrolled classes: {ex.Message}");
            }
            return new List<string>();
        }
    }

    // NEW: Method to update user's company information
    public async Task<bool> UpdateUserCompany(string newCompany)
    {
        if (auth.CurrentUser == null) return false;

        try
        {
            if (string.IsNullOrWhiteSpace(newCompany))
            {
                ShowNotification("Company name cannot be empty", NotificationType.Error);
                return false;
            }

            DocumentReference userDocRef = db.Collection(COLLECTION_USERS).Document(auth.CurrentUser.UserId);
            await userDocRef.UpdateAsync("company", newCompany.Trim());

            ShowNotification("Company information updated successfully", NotificationType.Success);

            if (enableDebugLogging)
            {
                Debug.Log($"Updated company for user {auth.CurrentUser.UserId} to: {newCompany}");
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowNotification("Failed to update company information", NotificationType.Error);
            if (enableDebugLogging)
            {
                Debug.LogError($"Company update error: {ex.Message}");
            }
            return false;
        }
    }

    // NEW: Method to get user's current company
    public async Task<string> GetUserCompany()
    {
        if (auth.CurrentUser == null) return "";

        try
        {
            var userData = await GetUserData(auth.CurrentUser.UserId);
            return userData?.company ?? "";
        }
        catch (Exception ex)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"Error getting user company: {ex.Message}");
            }
            return "";
        }
    }

    // NEW: Method to populate company field with default value
    public void SetDefaultCompany()
    {
        if (SignupCompany != null && string.IsNullOrWhiteSpace(SignupCompany.text))
        {
            SignupCompany.text = defaultCompany;
        }
    }

    // NEW: Method to clear only the company field
    public void ClearCompanyField()
    {
        if (SignupCompany != null)
        {
            SignupCompany.text = "";
        }
    }
    private DocumentReference GetUserDocumentReference(string userId)
    {
        return db.Collection(COLLECTION_USERS).Document(userId);
    }

    private DocumentReference GetClassDocumentReference(string classCode)
    {
        return db.Collection(COLLECTION_CLASSES).Document(classCode);
    }
    #endregion

    #region Updated Data Classes

    [System.Serializable]
    public class UserData
    {
        public string company;
        public string email;
        public List<string> enrolledClasses;
        public string firstName;
        public string lastName;
        public string role;
        public string uid;
    }

    // NEW: Class data structure matching your Firestore schema
    [System.Serializable]
    public class ClassData
    {
        public string code; // The class code (e.g., "ABC123")
        public string documentId; // The actual Firestore document ID (e.g., "kDwmgajiYEKrtrKV8vs4")
        public string name;
        public string description;
        public string instructorId;
        public string assignmentType;
        public string type;
        public string title;
        public string linkedCrimeSceneId;
        public string linkedCrimeSceneName;
        public Timestamp? dueDate;
        public bool autoAssessOnLogin;
        public Timestamp? createdAt;
        public List<string> students;
    }

    #endregion
}