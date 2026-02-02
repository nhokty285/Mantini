/*using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AuthenticationUI : MonoBehaviour
{
    [Header("Login Panel")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private Button appleLoginButton;
    [SerializeField] private GameObject loginProcessingIndicator;

    [Header("User Profile Panel")]
    [SerializeField] private GameObject userProfilePanel;
    [SerializeField] private TextMeshProUGUI userNameText;
    [SerializeField] private TextMeshProUGUI userEmailText;
    [SerializeField] private TextMeshProUGUI tenantInfoText;
    [SerializeField] private Button logoutButton;

    [Header("Additional Info Panel")]
    [SerializeField] private GameObject additionalInfoPanel;
    [SerializeField] private TMP_InputField phoneInputField;
    [SerializeField] private Button submitInfoButton;

    [Header("Error Display")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button closeErrorButton;

    public static AuthenticationUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetupButtonEvents();
        InitializeUI();

        // Subscribe to auth events
        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.OnLoginSuccess += OnLoginSuccess;
            GoogleAuthService.Instance.OnLoginFailed += OnLoginFailed;
            GoogleAuthService.Instance.OnLogoutComplete += OnLogoutComplete;
        }

        // Update UI based on current auth state
        UpdateAuthenticationState(GoogleAuthService.Instance?.IsLoggedIn ?? false,
                                  GoogleAuthService.Instance?.CurrentUser);
    }

    private void SetupButtonEvents()
    {
        googleLoginButton?.onClick.AddListener(OnGoogleLoginClicked);
        appleLoginButton?.onClick.AddListener(OnAppleLoginClicked);
        logoutButton?.onClick.AddListener(OnLogoutClicked);
        closeErrorButton?.onClick.AddListener(CloseErrorPanel);
        submitInfoButton?.onClick.AddListener(OnSubmitAdditionalInfo);
    }

    private void InitializeUI()
    {
        if (loginProcessingIndicator != null)
            loginProcessingIndicator.SetActive(false);

        if (errorPanel != null)
            errorPanel.SetActive(false);

        if (additionalInfoPanel != null)
            additionalInfoPanel.SetActive(false);

        // Disable Apple login for now
        if (appleLoginButton != null)
            appleLoginButton.interactable = false;
    }

    private void OnGoogleLoginClicked()
    {
        Debug.Log("🔍 Google Login button clicked");

        if (GoogleAuthService.Instance != null)
        {
            ShowProcessingState(true);
            GoogleAuthService.Instance.InitiateGoogleLogin();
        }
        else
        {
            ShowError("GoogleAuthService not available");
        }
    }

    private void OnAppleLoginClicked()
    {
        Debug.Log("🍎 Apple Login - Coming soon!");
        ShowError("Apple Login not implemented yet");
    }

    private void OnLogoutClicked()
    {
        Debug.Log("🔓 Logout button clicked");

        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.Logout();
        }
    }

    private void OnSubmitAdditionalInfo()
    {
        if (GoogleAuthService.Instance?.CurrentUser != null)
        {
            // Update phone number if provided
            string phone = phoneInputField?.text?.Trim();
            if (!string.IsNullOrEmpty(phone))
            {
                GoogleAuthService.Instance.CurrentUser.phone = phone;
                // Có thể gọi API để update thông tin này
                Debug.Log($"📞 Updated phone: {phone}");
            }

            // Hide additional info panel
            if (additionalInfoPanel != null)
                additionalInfoPanel.SetActive(false);
        }
    }

    private void OnLoginSuccess(UserProfile user)
    {
        Debug.Log($"✅ Login success callback for: {user.displayName}");

        ShowProcessingState(false);
        UpdateAuthenticationState(true, user);

        // Check if additional info is required
        if (user.requireToInputUserInfo)
        {
            ShowAdditionalInfoPanel();
        }

        ShowWelcomeMessage(user.displayName);
    }

    private void OnLoginFailed(string error)
    {
        Debug.LogError($"❌ Login failed callback: {error}");

        ShowProcessingState(false);
        ShowError($"Login failed: {error}");
        UpdateAuthenticationState(false, null);
    }

    private void OnLogoutComplete()
    {
        Debug.Log("🔓 Logout complete callback");
        UpdateAuthenticationState(false, null);
    }

    public void UpdateAuthenticationState(bool isLoggedIn, UserProfile user)
    {
        if (loginPanel != null)
            loginPanel.SetActive(!isLoggedIn);

        if (userProfilePanel != null)
            userProfilePanel.SetActive(isLoggedIn);

        if (isLoggedIn && user != null)
        {
            UpdateUserProfileDisplay(user);
        }
    }

    private void UpdateUserProfileDisplay(UserProfile user)
    {
        if (userNameText != null)
            userNameText.text = user.displayName ?? "Unknown User";

        if (userEmailText != null)
            userEmailText.text = user.email ?? "";

        if (tenantInfoText != null)
        {
            string tenantInfo = "";
            if (!string.IsNullOrEmpty(user.tenantName))
            {
                tenantInfo = $"Tenant: {user.tenantName}";
                if (user.isTenantOwner)
                    tenantInfo += " (Owner)";
            }
            tenantInfoText.text = tenantInfo;
        }
    }

    private void ShowAdditionalInfoPanel()
    {
        if (additionalInfoPanel != null)
        {
            additionalInfoPanel.SetActive(true);

            // Pre-fill phone if available
            if (phoneInputField != null && GoogleAuthService.Instance?.CurrentUser != null)
            {
                phoneInputField.text = GoogleAuthService.Instance.CurrentUser.phone ?? "";
            }
        }
    }

    private void ShowProcessingState(bool isProcessing)
    {
        if (loginProcessingIndicator != null)
            loginProcessingIndicator.SetActive(isProcessing);

        if (googleLoginButton != null)
            googleLoginButton.interactable = !isProcessing;
    }

    private void ShowError(string message)
    {
        Debug.LogError($"UI Error: {message}");

        if (errorPanel != null)
        {
            errorPanel.SetActive(true);

            if (errorMessageText != null)
                errorMessageText.text = message;
        }
    }

    private void CloseErrorPanel()
    {
        if (errorPanel != null)
            errorPanel.SetActive(false);
    }

    private void ShowWelcomeMessage(string userName)
    {
        Debug.Log($"🎉 Welcome {userName}!");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.OnLoginSuccess -= OnLoginSuccess;
            GoogleAuthService.Instance.OnLoginFailed -= OnLoginFailed;
            GoogleAuthService.Instance.OnLogoutComplete -= OnLogoutComplete;
        }
    }
}
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AuthenticationUI : MonoBehaviour
{
    [Header("Login Panel")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private Button quickLoginButton; // Thay vì googleLoginButton
    [SerializeField] private Button logoutButton;
    [SerializeField] private GameObject loginProcessingIndicator;

    [Header("User Profile Panel")]
    [SerializeField] private GameObject userProfilePanel;
    [SerializeField] private TextMeshProUGUI userNameText;
    [SerializeField] private TextMeshProUGUI userEmailText;
    [SerializeField] private TextMeshProUGUI tenantInfoText;
    [SerializeField] private TextMeshProUGUI accountIdText; // Hiển thị account ID

    [Header("Game Interface")]
    [SerializeField] private GameObject gameMainPanel; // Main game UI

    [Header("Error Display")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button closeErrorButton;

    public static AuthenticationUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetupButtonEvents();
        InitializeUI();

        // Subscribe to auth events
        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.OnLoginSuccess += OnLoginSuccess;
            GoogleAuthService.Instance.OnLoginFailed += OnLoginFailed;
            GoogleAuthService.Instance.OnLogoutComplete += OnLogoutComplete;
        }

        // Check current auth state
        bool isLoggedIn = GoogleAuthService.Instance?.IsLoggedIn ?? false;
        UpdateAuthenticationState(isLoggedIn, GoogleAuthService.Instance?.CurrentUser);
    }

    private void SetupButtonEvents()
    {
        quickLoginButton?.onClick.AddListener(OnQuickLoginClicked);
        logoutButton?.onClick.AddListener(OnLogoutClicked);
        closeErrorButton?.onClick.AddListener(CloseErrorPanel);
    }

    private void InitializeUI()
    {
        if (loginProcessingIndicator != null)
            loginProcessingIndicator.SetActive(false);

        if (errorPanel != null)
            errorPanel.SetActive(false);

        if (gameMainPanel != null)
            gameMainPanel.SetActive(false);
    }

    private void OnQuickLoginClicked()
    {
        Debug.Log("⚡ Quick Login button clicked");

        if (GoogleAuthService.Instance != null)
        {
            ShowProcessingState(true);
            GoogleAuthService.Instance.QuickLogin();
        }
        else
        {
            ShowError("GoogleAuthService not available");
        }
    }

    private void OnLogoutClicked()
    {
        Debug.Log("🔓 Logout button clicked");

        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.Logout();
        }
    }

    private void OnLoginSuccess(UserProfile user)
    {
        Debug.Log($"✅ Login success callback for: {user.displayName}");

        ShowProcessingState(false);
        UpdateAuthenticationState(true, user);
        ShowWelcomeMessage(user.displayName);

        // Show game interface
        ShowGameInterface();
    }

    private void OnLoginFailed(string error)
    {
        Debug.LogError($"❌ Login failed callback: {error}");

        ShowProcessingState(false);
        ShowError($"Login failed: {error}");
        UpdateAuthenticationState(false, null);
    }

    private void OnLogoutComplete()
    {
        Debug.Log("🔓 Logout complete callback");
        UpdateAuthenticationState(false, null);
        HideGameInterface();
    }

    public void UpdateAuthenticationState(bool isLoggedIn, UserProfile user)
    {
        if (loginPanel != null)
            loginPanel.SetActive(!isLoggedIn);

        if (userProfilePanel != null)
            userProfilePanel.SetActive(isLoggedIn);

        if (isLoggedIn && user != null)
        {
            UpdateUserProfileDisplay(user);
        }
    }

    private void UpdateUserProfileDisplay(UserProfile user)
    {
        if (userNameText != null)
            userNameText.text = user.displayName ?? "Unknown User";

        if (userEmailText != null)
            userEmailText.text = user.email ?? "";

        if (accountIdText != null)
            accountIdText.text = $"ID: {user.accountId}";

        if (tenantInfoText != null)
        {
            string tenantInfo = "";
            if (!string.IsNullOrEmpty(user.tenantName))
            {
                tenantInfo = $"Tenant: {user.tenantName}";
                if (user.isTenantOwner)
                    tenantInfo += " (Owner)";
            }
            tenantInfoText.text = tenantInfo;
        }
    }

    private void ShowGameInterface()
    {
        if (gameMainPanel != null)
        {
            gameMainPanel.SetActive(true);
        }

        Debug.Log("🎮 Game interface shown!");
    }

    private void HideGameInterface()
    {
        if (gameMainPanel != null)
        {
            gameMainPanel.SetActive(false);
        }

        Debug.Log("🎮 Game interface hidden!");
    }

    private void ShowProcessingState(bool isProcessing)
    {
        if (loginProcessingIndicator != null)
            loginProcessingIndicator.SetActive(isProcessing);

        if (quickLoginButton != null)
            quickLoginButton.interactable = !isProcessing;
    }

    private void ShowError(string message)
    {
        Debug.LogError($"UI Error: {message}");

        if (errorPanel != null)
        {
            errorPanel.SetActive(true);

            if (errorMessageText != null)
                errorMessageText.text = message;
        }
    }

    private void CloseErrorPanel()
    {
        if (errorPanel != null)
            errorPanel.SetActive(false);
    }

    private void ShowWelcomeMessage(string userName)
    {
        Debug.Log($"🎉 Welcome {userName}! You're now logged in and ready to play!");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.OnLoginSuccess -= OnLoginSuccess;
            GoogleAuthService.Instance.OnLoginFailed -= OnLoginFailed;
            GoogleAuthService.Instance.OnLogoutComplete -= OnLogoutComplete;
        }
    }
}
