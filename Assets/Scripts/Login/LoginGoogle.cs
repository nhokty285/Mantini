using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using System.IO;
#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase.Auth;
#endif


public class LoginController : MonoBehaviour
{
    public class GoogleConfig
    {
        public string clientId;
        public string clientSecret;
    }
    public event Action<PlayerProfile> OnSignedIn;

    [Header("Google OAuth Config (Editor - web client)")]
    [SerializeField] private string clientId;
    [SerializeField] private string clientSecret;
    private const string RedirectUri = "http://localhost:8080/"; // chỉ dùng trong Editor

    [Header("Google OAuth Config (Android - deep link)")]
    // Client ID dùng cho Android (loại client hỗ trợ custom scheme). Để trống thì dùng clientId chung.
    [SerializeField] private string androidClientId = "600744707529-q6pkvqgpg41ekroqortlvlkfboq0g7aq.apps.googleusercontent.com";
    // Redirect deep link: PHẢI khớp với intent-filter trong AndroidManifest.xml VÀ scheme phải khớp package name
    // (client OAuth loại Android không có ô khai redirect_uri riêng như client Web, nên Google dùng package name để validate scheme).
    [SerializeField] private string androidRedirectUri = "cm.googlesignin.com.unity.template.urpblanko:/oauth2redirect";

    private PlayerInfo playerInfo;
    private PlayerProfile playerProfile;
    public PlayerProfile PlayerProfile => playerProfile;

    private HttpListener _httpListener;

#if UNITY_ANDROID && !UNITY_EDITOR
    // PKCE: trên thiết bị KHÔNG dùng client_secret. Sinh code_verifier mỗi lần login,
    // gửi code_challenge (SHA256) khi xin code, rồi gửi lại code_verifier khi đổi token.
    private string _codeVerifier;
#endif

    // --- Main Thread marshalling ---
    // Toàn bộ luồng OAuth (HttpClient/HttpListener) chạy trên BACKGROUND THREAD.
    // Mọi Unity API (AuthenticationService, event UI...) BẮT BUỘC chạy Main Thread.
    // Background thread chỉ làm network + xử lý chuỗi .NET thuần rồi set cờ;
    // Update() (Main Thread) nhặt cờ và gọi các Unity API. O(1)/frame, không alloc.

    private volatile bool _tokenReady = false;
    private string _pendingIdToken;
    private string _pendingAccessToken;
    private string _pendingEmail;

    private volatile bool _signInCompleted = false;
    private PlayerProfile _pendingProfile;

#if UNITY_ANDROID && !UNITY_EDITOR
    // Đảm bảo chỉ subscribe OnFirebaseSignedIn đúng 1 lần (tránh subscribe trùng / leak).
    private bool _firebaseSubscribed = false;
#endif

    private async void Awake()
    {
        // Khởi tạo dịch vụ Unity Services khi vào game
        await UnityServices.InitializeAsync();
    }

    private void OnEnable()
    {
        // Android: lắng nghe deep link trả code về app sau khi đăng nhập trên trình duyệt.
        Application.deepLinkActivated += OnDeepLinkActivated;
    }

    private void OnDisable()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Hủy đăng ký Firebase event nếu đã subscribe (tránh memory leak).
        if (_firebaseSubscribed && FirebaseAuthTester.Instance != null)
        {
            FirebaseAuthTester.Instance.OnFirebaseSignedIn -= OnFirebaseSignedIn;
            _firebaseSubscribed = false;
        }
#endif
    }

    void Start()
    {
        string filePath = Path.Combine(Application.dataPath, "appsettings.json");

        if (File.Exists(filePath))
        {
            string jsonText = File.ReadAllText(filePath);
            GoogleConfig config = JsonUtility.FromJson<GoogleConfig>(jsonText);

            clientId = config.clientId;
            clientSecret = config.clientSecret;

            Debug.Log("Đã nạp Google API Keys thành công từ file cục bộ!");
        }
        else
        {
            Debug.LogError("Không tìm thấy file appsettings.json cấu hình!");
        }

        // Cold start: app bị mở LẦN ĐẦU bằng chính deep link (chưa kịp đăng ký event ở trên).
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Subscribe OnFirebaseSignedIn ngay tại thời điểm cần dùng (lazy).
    // Lý do: FirebaseAuthTester.Instance được gán trong Awake() của nó, có thể CHƯA tồn tại
    // khi LoginController.OnEnable() chạy (thứ tự Awake/OnEnable giữa các GameObject không đảm bảo).
    // Subscribe trong OnEnable dễ bị skip vĩnh viễn -> event không bao giờ về tới đây.
    // Gọi hàm này ngay trước khi đăng nhập Firebase, khi Instance chắc chắn đã có.
    private void EnsureFirebaseSubscribed()
    {
        if (_firebaseSubscribed) return;
        if (FirebaseAuthTester.Instance == null) return;

        FirebaseAuthTester.Instance.OnFirebaseSignedIn += OnFirebaseSignedIn;
        _firebaseSubscribed = true;
        Debug.Log("[LoginController] [DEBUG] (Android) Đã subscribe OnFirebaseSignedIn.");
    }
#endif

    private void Update()
    {
        // Chạy trên Main Thread.

        // (1) Background thread đã đổi token xong -> đăng nhập trên Main Thread.
        if (_tokenReady)
        {
            _tokenReady = false;
            Debug.Log("[LoginController] [DEBUG] Update phát hiện _tokenReady");

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: CHỈ đăng nhập Firebase. Không gọi Unity Authentication (gây "invalid audience").
            if (FirebaseAuthTester.Instance != null)
            {
                // Subscribe ngay tại đây: lúc này Instance chắc chắn đã được khởi tạo.
                EnsureFirebaseSubscribed();
                FirebaseAuthTester.Instance.SignInWithGoogleIdToken(_pendingIdToken, _pendingAccessToken);
            }
            else
            {
                Debug.LogError("[LoginController] FirebaseAuthTester.Instance null - không thể đăng nhập trên Android. " +
                               "Đảm bảo đã gắn FirebaseAuthTester vào 1 GameObject trong scene.");
            }
#else
            // Editor/Desktop: dùng Unity Authentication như cũ.
            _ = SignInWithGoogleAsync(_pendingIdToken, _pendingEmail);
#endif
        }

        // (2) SignIn xong -> bắn event login để UI chuyển panel / vào Onboarding.
        if (_signInCompleted)
        {
            _signInCompleted = false;
            playerProfile = _pendingProfile;
            int listeners = OnSignedIn == null ? 0 : OnSignedIn.GetInvocationList().Length;
            Debug.Log($"[LoginController] [DEBUG] Bắn OnSignedIn (số listener = {listeners})");
            OnSignedIn?.Invoke(playerProfile);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android: Firebase đăng nhập xong -> dựng PlayerProfile và báo Update() bắn OnSignedIn.
    // Callback đã ở Main Thread (ContinueWithOnMainThread), nhưng vẫn đi qua cờ để
    // giữ một điểm bắn OnSignedIn duy nhất ở Update(), nhất quán với luồng Editor.
    private void OnFirebaseSignedIn(FirebaseUser user)
    {
        if (user == null)
        {
            Debug.LogError("[LoginController] OnFirebaseSignedIn nhận user null.");
            return;
        }

        // Android không qua Unity Auth nên playerInfo = null.
        // UILogin/Onboarding hiện không dùng playerInfo nên an toàn.
        PlayerProfile profile = new PlayerProfile
        {
            playerInfo = null,
            Name = string.IsNullOrEmpty(user.DisplayName)
                   ? $"Player_{user.UserId.Substring(Mathf.Max(0, user.UserId.Length - 8))}"
                   : user.DisplayName,
            Email = user.Email
        };

        _pendingProfile = profile;
        _signInCompleted = true;
        Debug.Log("[LoginController] [DEBUG] (Android) Firebase OK -> set _signInCompleted=true");
    }
#endif

    // Redirect URI theo nền tảng.
    private string GetRedirectUri()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return androidRedirectUri;
#else
        return RedirectUri;
#endif
    }

    // Client ID theo nền tảng (Android có thể dùng client riêng).
    private string GetClientId()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return string.IsNullOrEmpty(androidClientId) ? clientId : androidClientId;
#else
        return clientId;
#endif
    }

    // Gắn hàm này vào nút bấm Login Google.
    public async Task InitSignIn()
    {
        string redirectUri = GetRedirectUri();
        string extraParams = string.Empty;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: tạo PKCE thay cho client_secret.
        _codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(_codeVerifier);
        extraParams = $"&code_challenge={codeChallenge}&code_challenge_method=S256";
#endif

        // QUAN TRỌNG: phải có scope "openid" thì Google mới trả về id_token.
        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                         $"client_id={GetClientId()}&" +
                         $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                         $"response_type=code&" +
                         $"scope=openid%20email%20profile&" +
                         $"prompt=select_account" + extraParams;

        // Mở trình duyệt để người dùng chọn tài khoản.
        Application.OpenURL(authUrl);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: KHÔNG mở HttpListener. Chờ Google redirect về deep link -> OnDeepLinkActivated.
        Debug.Log("[LoginController] (Android) Chờ deep link trả 'code' về app...");
        await Task.CompletedTask;
#else
        // Editor/Desktop: mở server local lắng nghe redirect.
        Task.Run(() => StartLocalServerAsync());
        await Task.CompletedTask;
#endif
    }

    // Android: nhận code từ deep link (vd cm.googlesignin.com.unity.template.urpblanko:/oauth2redirect?code=XYZ).
    private void OnDeepLinkActivated(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        Debug.Log($"[LoginController] Deep link nhận được: {url}");

        string code = ParseUrlQueryParam(url, "code");
        if (!string.IsNullOrEmpty(code))
        {
            // deepLinkActivated chạy trên Main Thread; ExchangeCodeForTokenAsync await network nên ok.
            _ = ExchangeCodeForTokenAsync(code);
        }
        else
        {
            Debug.LogWarning("[LoginController] Deep link không chứa 'code'. Bỏ qua.");
        }
    }

    private async Task StartLocalServerAsync()
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(RedirectUri);
            _httpListener.Start();
            Debug.Log("[LoginController] Chờ bạn chọn tài khoản Gmail trên trình duyệt...");

            HttpListenerContext context = await _httpListener.GetContextAsync();
            string code = context.Request.QueryString["code"];

            HttpListenerResponse response = context.Response;
            string responseString = "<html><body style='text-align:center;font-family:sans-serif;padding-top:50px;'>" +
                                    "<h2>Dang nhap Google thanh cong!</h2><p>Ban co the quay lai Game.</p></body></html>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            _httpListener.Stop();

            if (!string.IsNullOrEmpty(code))
            {
                await ExchangeCodeForTokenAsync(code);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoginController] Lỗi Local Server: {ex.Message}");
        }
    }

    private async Task ExchangeCodeForTokenAsync(string code)
    {
        // Chạy trên background thread (hoặc main thread khi gọi từ deep link).
        // Chỉ dùng .NET thuần. KHÔNG gọi Unity API ở đây.
        using (HttpClient client = new HttpClient())
        {
            var values = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", GetClientId() },
                { "redirect_uri", GetRedirectUri() },
                { "grant_type", "authorization_code" }
            };

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: dùng PKCE (code_verifier), KHÔNG gửi client_secret.
            values.Add("code_verifier", _codeVerifier);
#else
            // Editor: dùng client_secret như cũ.
            values.Add("client_secret", clientSecret);
#endif

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
            var responseString = await response.Content.ReadAsStringAsync();

            Debug.Log($"[LoginController] [DEBUG] Raw token response = {responseString}");

            if (response.IsSuccessStatusCode)
            {
                string accessToken = ExtractJsonStringValue(responseString, "access_token");
                string idToken = ExtractJsonStringValue(responseString, "id_token");

                Debug.Log($"[LoginController] [DEBUG] access_token = {accessToken}");
                Debug.Log($"[LoginController] [DEBUG] id_token (length={idToken.Length})");

                if (string.IsNullOrEmpty(idToken))
                {
                    Debug.LogError("[LoginController] id_token RỖNG - kiểm tra scope 'openid' và OAuth client config.");
                    return;
                }

                string gmail = ExtractEmailFromJWT(idToken);
                Debug.LogWarning($"[LoginController] [THÀNH CÔNG] Đã nhận diện Gmail: {gmail}");

                _pendingIdToken = idToken;
                _pendingAccessToken = accessToken;
                _pendingEmail = gmail;
                _tokenReady = true;
            }
            else
            {
                Debug.LogError($"[LoginController] Lỗi đổi Token từ Google: {responseString}");
            }
        }
    }

    private async Task SignInWithGoogleAsync(string googleIdToken, string gmail)
    {
        // Được gọi từ Update() => Main Thread => Unity Services hợp lệ.
        // CHỈ dùng cho Editor/Desktop. Android đã chuyển sang Firebase.
        try
        {
            Debug.Log("[LoginController] [DEBUG] Bắt đầu SignInWithGoogleAsync...");
            await AuthenticationService.Instance.SignInWithGoogleAsync(googleIdToken);
            Debug.Log("[LoginController] SignIn với Google vào Unity Authentication thành công!");

            playerInfo = AuthenticationService.Instance.PlayerInfo;

            string name;
            try
            {
                name = await AuthenticationService.Instance.GetPlayerNameAsync(true);
            }
            catch (RequestFailedException)
            {
                name = $"Player_{playerInfo.Id.Substring(playerInfo.Id.Length - 8)}";
            }

            PlayerProfile profile = new PlayerProfile
            {
                playerInfo = playerInfo,
                Name = name,
                Email = gmail
            };

            _pendingProfile = profile;
            _signInCompleted = true;
            Debug.Log("[LoginController] [DEBUG] SignInWithGoogleAsync xong, đã set _signInCompleted=true");
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError("[LoginController] AuthenticationException khi SignIn Google:");
            Debug.LogException(ex);
        }
        catch (Exception ex)
        {
            Debug.LogError("[LoginController] Exception KHÁC khi SignIn Google:");
            Debug.LogException(ex);
        }
    }

    // Lấy giá trị 1 tham số query trong URL (vd ...?code=XYZ&scope=...).
    private string ParseUrlQueryParam(string url, string key)
    {
        try
        {
            int q = url.IndexOf('?');
            if (q < 0) return string.Empty;
            string query = url.Substring(q + 1);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq < 0) continue;
                if (pair.Substring(0, eq) == key)
                {
                    return Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginController] Lỗi parse deep link: {e.Message}");
        }
        return string.Empty;
    }

    // Trích giá trị string của 1 key trong JSON, chịu được cả khoảng trắng sau dấu ':'.
    private string ExtractJsonStringValue(string json, string key)
    {
        try
        {
            string keyPattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(keyPattern);
            if (keyIndex < 0) return string.Empty;

            int i = keyIndex + keyPattern.Length;
            int colon = json.IndexOf(':', i);
            if (colon < 0) return string.Empty;

            int valueStart = json.IndexOf('"', colon + 1);
            if (valueStart < 0) return string.Empty;
            valueStart += 1;

            int valueEnd = json.IndexOf('"', valueStart);
            if (valueEnd < 0) return string.Empty;

            return json.Substring(valueStart, valueEnd - valueStart);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginController] Lỗi parse JSON key '{key}': {e.Message}");
            return string.Empty;
        }
    }

    private string ExtractEmailFromJWT(string jwtToken)
    {
        try
        {
            string[] parts = jwtToken.Split('.');
            if (parts.Length > 1)
            {
                string payload = parts[1];
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                byte[] decodedBytes = Convert.FromBase64String(payload);
                string jsonPayload = System.Text.Encoding.UTF8.GetString(decodedBytes);

                string email = ExtractJsonStringValue(jsonPayload, "email");
                if (!string.IsNullOrEmpty(email)) return email;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginController] Lỗi giải mã Email: {e.Message}");
        }
        return "Không tìm thấy Email";
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // ----- PKCE helpers (chỉ dùng trên Android) -----
    private static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
#endif

    private void OnDestroy()
    {
        if (_httpListener != null && _httpListener.IsListening)
        {
            _httpListener.Stop();
        }
    }
}

[Serializable]
public struct TokenResponse
{
    public string access_token;
    public string id_token;
}
