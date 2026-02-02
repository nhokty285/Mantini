/*using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using OpenAI;

public class GoogleAuthService : MonoBehaviour
{
    public static GoogleAuthService Instance { get; private set; }

    [Header("Google OAuth Settings")]
    [SerializeField] private string googleClientId = "YOUR_GOOGLE_CLIENT_ID";
    [SerializeField] private string backendLoginUrl = "https://api.staging.storims.com/api/v1/Account";

    [Header("Authentication Events")]
    public System.Action<UserProfile> OnLoginSuccess;
    public System.Action<string> OnLoginFailed;
    public System.Action OnLogoutComplete;

    private UserProfile currentUser;
    private string currentAccessToken;

    public bool IsLoggedIn => currentUser != null && !string.IsNullOrEmpty(currentUser.accountId);
    public UserProfile CurrentUser => currentUser;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Register service in ServiceContainer
        if (ServiceContainer.Instance != null)
        {
            ServiceContainer.Instance.Register<GoogleAuthService>(this);
            Debug.Log("✅ GoogleAuthService registered");
        }

        // Try to restore previous session
        RestoreUserSession();
    }

    public void InitiateGoogleLogin()
    {
        Debug.Log("🔍 Initiating Google Login...");

        // For different platforms
#if UNITY_EDITOR || UNITY_STANDALONE
        StartCoroutine(WebGLGoogleAuth());
#elif UNITY_ANDROID
            AndroidGoogleAuth();
#elif UNITY_IOS
            IOSGoogleAuth();
#else
            StartCoroutine(WebGLGoogleAuth());
#endif
    }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
    private IEnumerator WebGLGoogleAuth()
    {
        // Simulate getting Google ID Token
        yield return new WaitForSeconds(1f);

        string simulatedIdToken = "google_id_token_" + System.DateTime.Now.Ticks;
        yield return StartCoroutine(SendAuthTokenToBackend(simulatedIdToken));
    }
#endif

#if UNITY_ANDROID
    private void AndroidGoogleAuth()
    {
        Debug.Log("📱 Android Google Auth");
        StartCoroutine(SimulateAndroidAuth());
    }

    private IEnumerator SimulateAndroidAuth()
    {
        yield return new WaitForSeconds(1f);

        string mockIdToken = "android_google_token_" + System.DateTime.Now.Ticks;
        yield return StartCoroutine(SendAuthTokenToBackend(mockIdToken));
    }
#endif

#if UNITY_IOS
    private void IOSGoogleAuth()
    {
        Debug.Log("🍎 iOS Google Auth");
        StartCoroutine(SimulateIOSAuth());
    }
    
    private IEnumerator SimulateIOSAuth()
    {
        yield return new WaitForSeconds(1f);
        
        string mockIdToken = "ios_google_token_" + System.DateTime.Now.Ticks;
        yield return StartCoroutine(SendAuthTokenToBackend(mockIdToken));
    }
#endif

    private IEnumerator SendAuthTokenToBackend(string idToken)
    {
        var request = new GoogleAuthRequest
        {
            idToken = idToken,
            platform = GetPlatformString()
        };

        string jsonData = JsonConvert.SerializeObject(request);

        Debug.Log($"🚀 Sending Google auth request to: {backendLoginUrl}");
        Debug.Log($"📦 Request data: {jsonData}");

        using (UnityWebRequest webRequest = new UnityWebRequest(backendLoginUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Authorization", $"Bearer {idToken}");
            webRequest.timeout = 30;

            yield return webRequest.SendWebRequest();

            Debug.Log($"📡 Response Code: {webRequest.responseCode}");
            Debug.Log($"📄 Response Body: {webRequest.downloadHandler.text}");

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var backendResponse = JsonConvert.DeserializeObject<BackendAuthResponse>(webRequest.downloadHandler.text);

                    if (backendResponse != null && !string.IsNullOrEmpty(backendResponse.accountId))
                    {
                        HandleLoginSuccess(backendResponse);
                    }
                    else
                    {
                        HandleLoginError("Invalid response from backend");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse backend response: {e.Message}");
                    HandleLoginError($"Failed to parse response: {e.Message}");
                }
            }
            else
            {
                HandleLoginError($"Network error: {webRequest.error}");
            }
        }
    }

    private void HandleLoginSuccess(BackendAuthResponse backendResponse)
    {
        // Convert backend response to UserProfile
        currentUser = UserProfile.FromBackendResponse(backendResponse);

        // Save session data
        SaveUserSession();

        Debug.Log($"✅ Login successful! Welcome {currentUser.displayName}");
        Debug.Log($"Account ID: {currentUser.accountId}");
        Debug.Log($"Tenant: {currentUser.tenantName} (Owner: {currentUser.isTenantOwner})");

        OnLoginSuccess?.Invoke(currentUser);

        // Update MainMenuView if available
        UpdateMainMenuAuthState(true);
    }

    private void HandleLoginError(string error)
    {
        Debug.LogError($"❌ Login failed: {error}");
        OnLoginFailed?.Invoke(error);

        UpdateMainMenuAuthState(false);
    }

    public void Logout()
    {
        Debug.Log("🔓 Logging out...");

        currentUser = null;
        currentAccessToken = null;

        // Clear saved session
        ClearUserSession();

        OnLogoutComplete?.Invoke();
        UpdateMainMenuAuthState(false);

        Debug.Log("✅ Logout complete");
    }

    private void SaveUserSession()
    {
        if (currentUser != null)
        {
            PlayerPrefs.SetString("auth_account_id", currentUser.accountId);
            PlayerPrefs.SetString("auth_user_id", currentUser.userId);
            PlayerPrefs.SetString("auth_user_email", currentUser.email);
            PlayerPrefs.SetString("auth_user_name", currentUser.displayName);
            PlayerPrefs.SetString("auth_first_name", currentUser.firstName ?? "");
            PlayerPrefs.SetString("auth_last_name", currentUser.lastName ?? "");
            PlayerPrefs.SetString("auth_phone", currentUser.phone ?? "");
            PlayerPrefs.SetString("auth_tenant_id", currentUser.tenantId ?? "");
            PlayerPrefs.SetString("auth_tenant_name", currentUser.tenantName ?? "");
            PlayerPrefs.SetInt("auth_is_tenant_owner", currentUser.isTenantOwner ? 1 : 0);
            PlayerPrefs.SetInt("auth_require_user_info", currentUser.requireToInputUserInfo ? 1 : 0);
            PlayerPrefs.SetString("auth_provider", currentUser.provider);
            PlayerPrefs.Save();

            Debug.Log("💾 User session saved");
        }
    }

    private void RestoreUserSession()
    {
        if (PlayerPrefs.HasKey("auth_account_id"))
        {
            currentUser = new UserProfile
            {
                accountId = PlayerPrefs.GetString("auth_account_id"),
                userId = PlayerPrefs.GetString("auth_user_id"),
                email = PlayerPrefs.GetString("auth_user_email"),
                displayName = PlayerPrefs.GetString("auth_user_name"),
                firstName = PlayerPrefs.GetString("auth_first_name"),
                lastName = PlayerPrefs.GetString("auth_last_name"),
                phone = PlayerPrefs.GetString("auth_phone"),
                tenantId = PlayerPrefs.GetString("auth_tenant_id"),
                tenantName = PlayerPrefs.GetString("auth_tenant_name"),
                isTenantOwner = PlayerPrefs.GetInt("auth_is_tenant_owner", 0) == 1,
                requireToInputUserInfo = PlayerPrefs.GetInt("auth_require_user_info", 0) == 1,
                provider = PlayerPrefs.GetString("auth_provider", "google")
            };

            Debug.Log($"🔄 Restored session for: {currentUser.displayName}");
            Debug.Log($"Account: {currentUser.accountId}, Tenant: {currentUser.tenantName}");

            UpdateMainMenuAuthState(true);
        }
        else
        {
            Debug.Log("📝 No previous session found");
        }
    }

    private void ClearUserSession()
    {
        string[] keysToDelete = {
            "auth_account_id", "auth_user_id", "auth_user_email", "auth_user_name",
            "auth_first_name", "auth_last_name", "auth_phone", "auth_tenant_id",
            "auth_tenant_name", "auth_is_tenant_owner", "auth_require_user_info", "auth_provider"
        };

        foreach (string key in keysToDelete)
        {
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
        Debug.Log("🗑️ User session cleared");
    }

    private void UpdateMainMenuAuthState(bool isLoggedIn)
    {
        if (MainMenuView.Instance != null)
        {
            // Có thể thêm method này vào MainMenuView để update UI
            // MainMenuView.Instance.UpdateAuthenticationState(isLoggedIn, currentUser);
        }
    }

    private string GetPlatformString()
    {
#if UNITY_ANDROID
        return "android";
#elif UNITY_IOS
            return "ios";
#elif UNITY_WEBGL
            return "webgl";
#else
            return "desktop";
#endif
    }

    // Public method để check thông tin user
    public bool HasTenantInfo()
    {
        return currentUser != null && !string.IsNullOrEmpty(currentUser.tenantId);
    }

    public bool RequiresAdditionalInfo()
    {
        return currentUser?.requireToInputUserInfo ?? false;
    }

    // Method để get authorization header cho API calls khác
    public string GetAuthorizationHeader()
    {
        // Nếu backend trả về access token, dùng nó
        // Hiện tại sử dụng accountId như một identifier
        return IsLoggedIn ? $"Bearer {currentUser.accountId}" : "";
    }
}


[Serializable]
public class GoogleAuthRequest
{
    public string idToken;
    public string accessToken; // Thêm trường này nếu cần
    public string platform = "unity";
}

[Serializable]
public class BackendAuthResponse
{
    public string accountId;
    public string userId;
    public string tenantId;
    public string tenantCustomerId;
    public string email;
    public string phone;
    public string firstName;
    public string lastName;
    public string fullName;
    public bool isEmailConfirmed;
    public bool isPhoneConfirmed;
    public string loginSuccessfulDate;
    public string createdDate;
    public string tenantName;
    public bool isTenantOwner;
    public string contactEmail;
    public string contactPhone;
    public bool requireToInputUserInfo;
}

[Serializable]
public class UserProfile
{
    public string accountId;
    public string userId;
    public string email;
    public string displayName; // Sẽ map từ fullName
    public string firstName;
    public string lastName;
    public string phone;
    public string tenantId;
    public string tenantName;
    public bool isTenantOwner;
    public bool requireToInputUserInfo;
    public string provider = "google";

    // Constructor từ BackendAuthResponse
    public static UserProfile FromBackendResponse(BackendAuthResponse response)
    {
        return new UserProfile
        {
            accountId = response.accountId,
            userId = response.userId,
            email = response.email,
            displayName = response.fullName,
            firstName = response.firstName,
            lastName = response.lastName,
            phone = response.phone,
            tenantId = response.tenantId,
            tenantName = response.tenantName,
            isTenantOwner = response.isTenantOwner,
            requireToInputUserInfo = response.requireToInputUserInfo
        };
    }
}
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class GoogleAuthService : MonoBehaviour
{
    public static GoogleAuthService Instance { get; private set; }

    [Header("Quick Login Settings")]
    [SerializeField] private string presetAccessToken = "YOUR_ACCESS_TOKEN_HERE"; // Paste access token vào đây
    [SerializeField] private string backendLoginUrl = "https://api.staging.storims.com/api/v1/Account";
    [SerializeField] private bool autoLoginOnStart = true; // Tự động đăng nhập khi start

    [Header("Authentication Events")]
    public System.Action<UserProfile> OnLoginSuccess;
    public System.Action<string> OnLoginFailed;
    public System.Action OnLogoutComplete;

    private UserProfile currentUser;
    private string currentAccessToken;

    public bool IsLoggedIn => currentUser != null && !string.IsNullOrEmpty(currentUser.accountId);
    public UserProfile CurrentUser => currentUser;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Register service in ServiceContainer
        if (ServiceContainer.Instance != null)
        {
            ServiceContainer.Instance.Register<GoogleAuthService>(this);
            Debug.Log("✅ GoogleAuthService registered");
        }

        // Auto login with preset token
        if (autoLoginOnStart)
        {
            StartCoroutine(QuickLoginWithToken());
        }
        else
        {
            // Try to restore previous session
            RestoreUserSession();
        }
    }

    // Quick login method sử dụng access token có sẵn
    public void QuickLogin()
    {
        if (!string.IsNullOrEmpty(presetAccessToken))
        {
            StartCoroutine(QuickLoginWithToken());
        }
        else
        {
            Debug.LogError("❌ No preset access token found!");
            OnLoginFailed?.Invoke("No access token configured");
        }
    }

    private IEnumerator QuickLoginWithToken()
    {
        Debug.Log("🚀 Quick login with preset access token...");

        if (string.IsNullOrEmpty(presetAccessToken))
        {
            Debug.LogError("❌ Preset access token is empty!");
            OnLoginFailed?.Invoke("Access token not configured");
            yield break;
        }

        currentAccessToken = presetAccessToken;

        // Call backend API với access token
        yield return StartCoroutine(GetUserInfoFromBackend());
    }

    private IEnumerator GetUserInfoFromBackend()
    {
        Debug.Log($"🌐 Getting user info from: {backendLoginUrl}");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(backendLoginUrl))
        {
            webRequest.SetRequestHeader("Authorization", $"Bearer {currentAccessToken}");
            webRequest.timeout = 30;

            yield return webRequest.SendWebRequest();

            Debug.Log($"📡 Response Code: {webRequest.responseCode}");
            Debug.Log($"📄 Response Body: {webRequest.downloadHandler.text}");

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var backendResponse = JsonConvert.DeserializeObject<BackendAuthResponse>(webRequest.downloadHandler.text);

                    if (backendResponse != null && !string.IsNullOrEmpty(backendResponse.accountId))
                    {
                        HandleLoginSuccess(backendResponse);
                    }
                    else
                    {
                        HandleLoginError("Invalid response from backend");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse backend response: {e.Message}");
                    HandleLoginError($"Failed to parse response: {e.Message}");
                }
            }
            else
            {
                HandleLoginError($"Network error: {webRequest.error} (Code: {webRequest.responseCode})");
            }
        }
    }

    // Manual login method (giữ lại cho sau này)
    public void InitiateGoogleLogin()
    {
        Debug.Log("🔍 Initiating Google Login...");

        // Nếu có preset token, sử dụng nó
        if (!string.IsNullOrEmpty(presetAccessToken))
        {
            StartCoroutine(QuickLoginWithToken());
            return;
        }

        // Nếu không có preset token, dùng flow cũ (để làm sau)
        Debug.LogWarning("⚠️ No preset token, full OAuth flow not implemented yet");
        OnLoginFailed?.Invoke("Full OAuth login not implemented yet. Please set preset access token.");
    }

    private void HandleLoginSuccess(BackendAuthResponse backendResponse)
    {
        // Convert backend response to UserProfile
        currentUser = UserProfile.FromBackendResponse(backendResponse);

        // Save session data
        SaveUserSession();

        Debug.Log($"✅ Quick login successful! Welcome {currentUser.displayName}");
        Debug.Log($"📧 Email: {currentUser.email}");
        Debug.Log($"🆔 Account ID: {currentUser.accountId}");
        Debug.Log($"🏢 Tenant: {currentUser.tenantName} (Owner: {currentUser.isTenantOwner})");

        OnLoginSuccess?.Invoke(currentUser);

        // Update AuthenticationUI if available
        UpdateAuthenticationUI(true);

        // Auto-hide login panel và show game
        ShowGameInterface();
    }

    private void HandleLoginError(string error)
    {
        Debug.LogError($"❌ Quick login failed: {error}");
        OnLoginFailed?.Invoke(error);

        UpdateAuthenticationUI(false);
    }

    public void Logout()
    {
        Debug.Log("🔓 Logging out...");

        currentUser = null;
        currentAccessToken = null;

        // Clear saved session
        ClearUserSession();

        OnLogoutComplete?.Invoke();
        UpdateAuthenticationUI(false);

        Debug.Log("✅ Logout complete");
    }

    private void ShowGameInterface()
    {
        // Ẩn authentication panel và show game interface
        if (AuthenticationUI.Instance != null)
        {
            AuthenticationUI.Instance.UpdateAuthenticationState(true, currentUser);
        }

        // Bạn có thể thêm code để show main game UI ở đây
        Debug.Log("🎮 Game interface should be shown now!");
    }

    private void SaveUserSession()
    {
        if (currentUser != null)
        {
            PlayerPrefs.SetString("auth_account_id", currentUser.accountId);
            PlayerPrefs.SetString("auth_user_id", currentUser.userId);
            PlayerPrefs.SetString("auth_user_email", currentUser.email);
            PlayerPrefs.SetString("auth_user_name", currentUser.displayName);
            PlayerPrefs.SetString("auth_first_name", currentUser.firstName ?? "");
            PlayerPrefs.SetString("auth_last_name", currentUser.lastName ?? "");
            PlayerPrefs.SetString("auth_phone", currentUser.phone ?? "");
            PlayerPrefs.SetString("auth_tenant_id", currentUser.tenantId ?? "");
            PlayerPrefs.SetString("auth_tenant_name", currentUser.tenantName ?? "");
            PlayerPrefs.SetInt("auth_is_tenant_owner", currentUser.isTenantOwner ? 1 : 0);
            PlayerPrefs.SetInt("auth_require_user_info", currentUser.requireToInputUserInfo ? 1 : 0);
            PlayerPrefs.SetString("auth_provider", currentUser.provider);
            PlayerPrefs.SetString("auth_access_token", currentAccessToken ?? ""); // Save access token
            PlayerPrefs.Save();

            Debug.Log("💾 User session saved with access token");
        }
    }

    private void RestoreUserSession()
    {
        if (PlayerPrefs.HasKey("auth_account_id"))
        {
            currentUser = new UserProfile
            {
                accountId = PlayerPrefs.GetString("auth_account_id"),
                userId = PlayerPrefs.GetString("auth_user_id"),
                email = PlayerPrefs.GetString("auth_user_email"),
                displayName = PlayerPrefs.GetString("auth_user_name"),
                firstName = PlayerPrefs.GetString("auth_first_name"),
                lastName = PlayerPrefs.GetString("auth_last_name"),
                phone = PlayerPrefs.GetString("auth_phone"),
                tenantId = PlayerPrefs.GetString("auth_tenant_id"),
                tenantName = PlayerPrefs.GetString("auth_tenant_name"),
                isTenantOwner = PlayerPrefs.GetInt("auth_is_tenant_owner", 0) == 1,
                requireToInputUserInfo = PlayerPrefs.GetInt("auth_require_user_info", 0) == 1,
                provider = PlayerPrefs.GetString("auth_provider", "google")
            };

            currentAccessToken = PlayerPrefs.GetString("auth_access_token", "");

            Debug.Log($"🔄 Restored session for: {currentUser.displayName}");
            Debug.Log($"Account: {currentUser.accountId}, Tenant: {currentUser.tenantName}");

            UpdateAuthenticationUI(true);
            ShowGameInterface();
        }
        else
        {
            Debug.Log("📝 No previous session found");
            UpdateAuthenticationUI(false);
        }
    }

    private void ClearUserSession()
    {
        string[] keysToDelete = {
            "auth_account_id", "auth_user_id", "auth_user_email", "auth_user_name",
            "auth_first_name", "auth_last_name", "auth_phone", "auth_tenant_id",
            "auth_tenant_name", "auth_is_tenant_owner", "auth_require_user_info",
            "auth_provider", "auth_access_token"
        };

        foreach (string key in keysToDelete)
        {
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
        Debug.Log("🗑️ User session cleared");
    }

    private void UpdateAuthenticationUI(bool isLoggedIn)
    {
        if (AuthenticationUI.Instance != null)
        {
            AuthenticationUI.Instance.UpdateAuthenticationState(isLoggedIn, currentUser);
        }
    }

    // Public method để get authorization header cho API calls khác
    public string GetAuthorizationHeader()
    {
        return !string.IsNullOrEmpty(currentAccessToken) ? $"Bearer {currentAccessToken}" : "";
    }

    public bool HasValidToken()
    {
        return !string.IsNullOrEmpty(currentAccessToken);
    }

    public bool HasTenantInfo()
    {
        return currentUser != null && !string.IsNullOrEmpty(currentUser.tenantId);
    }

    public bool RequiresAdditionalInfo()
    {
        return currentUser?.requireToInputUserInfo ?? false;
    }

    // Method để set access token from code (nếu cần)
    public void SetAccessToken(string token)
    {
        presetAccessToken = token;
        Debug.Log("🔑 Access token updated");
    }
}

[Serializable]
public class BackendAuthResponse
{
    public string accountId;
    public string userId;
    public string tenantId;
    public string tenantCustomerId;
    public string email;
    public string phone;
    public string firstName;
    public string lastName;
    public string fullName;
    public bool isEmailConfirmed;
    public bool isPhoneConfirmed;
    public string loginSuccessfulDate;
    public string createdDate;
    public string tenantName;
    public bool isTenantOwner;
    public string contactEmail;
    public string contactPhone;
    public bool requireToInputUserInfo;
}

[Serializable]
public class UserProfile
{
    public string accountId;
    public string userId;
    public string email;
    public string displayName;
    public string firstName;
    public string lastName;
    public string phone;
    public string tenantId;
    public string tenantName;
    public bool isTenantOwner;
    public bool requireToInputUserInfo;
    public string provider = "google";

    // Constructor từ BackendAuthResponse
    public static UserProfile FromBackendResponse(BackendAuthResponse response)
    {
        return new UserProfile
        {
            accountId = response.accountId,
            userId = response.userId,
            email = response.email,
            displayName = response.fullName,
            firstName = response.firstName,
            lastName = response.lastName,
            phone = response.phone,
            tenantId = response.tenantId,
            tenantName = response.tenantName,
            isTenantOwner = response.isTenantOwner,
            requireToInputUserInfo = response.requireToInputUserInfo
        };
    }
}

