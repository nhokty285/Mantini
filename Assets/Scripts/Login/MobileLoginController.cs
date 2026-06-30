using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using System.IO;

public class MobileLoginController : MonoBehaviour
{
    public class GoogleConfig
    {
        public string clientId; // Chỉ cần Client ID, hoàn toàn KHÔNG có clientSecret
    }

    public event Action<PlayerProfile> OnSignedIn;

    [Header("Google OAuth Config")]
    [SerializeField] private string clientId ="";

    // Chuỗi redirect tự động tạo khớp với Authorized redirect URIs dạng com.googleusercontent.apps.XXX:/oauth2callback
    private string RedirectUri;

    private PlayerInfo playerInfo;
    private PlayerProfile playerProfile;
    public PlayerProfile PlayerProfile => playerProfile;

    // --- Main Thread marshalling ---
    private volatile bool _tokenReady = false;
    private string _pendingIdToken;
    private string _pendingEmail;

    private volatile bool _signInCompleted = false;
    private PlayerProfile _pendingProfile;

    private async void Awake()
    {
        await UnityServices.InitializeAsync();

        // Đăng ký lắng nghe sự kiện Deep Link từ hệ điều hành
        Application.deepLinkActivated += OnDeepLinkActivated;

        // Xử lý nếu App đang đóng hoàn toàn và được gọi dậy trực tiếp bởi Deep Link
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
        }
    }

    void Start()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "appsettings.json");

        if (File.Exists(filePath))
        {
            string jsonText = File.ReadAllText(filePath);
            GoogleConfig config = JsonUtility.FromJson<GoogleConfig>(jsonText);

            clientId = config.clientId;

            // Tự động bóc tách tiền tố để dựng Redirect URI chuẩn di động
            string clientIdPrefix = clientId.Split('.')[0]; // Lấy cụm "600744707529-xxxx"
            RedirectUri = $"com.googleusercontent.apps.{clientIdPrefix}:/oauth2callback";

            Debug.Log($"[MobileLogin] Nạp Client ID thành công! Redirect URI: {RedirectUri}");
        }
        else
        {
            Debug.LogError("Không tìm thấy file appsettings.json trên thiết bị!");
        }
    }

    private void Update()
    {
        if (_tokenReady)
        {
            _tokenReady = false;
            _ = SignInWithGoogleAsync(_pendingIdToken, _pendingEmail);
        }

        if (_signInCompleted)
        {
            _signInCompleted = false;
            playerProfile = _pendingProfile;
            OnSignedIn?.Invoke(playerProfile);
        }
    }

    public async Task InitSignIn()
    {
        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogError("[MobileLogin] Chưa nạp được Client ID, không thể gọi Đăng nhập!");
            return;
        }

        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                          $"client_id={clientId}&" +
                          $"redirect_uri={RedirectUri}&" +
                          $"response_type=code&" +
                          $"scope=openid%20email%20profile&" +
                          $"prompt=select_account";

        // Bật trình duyệt Android xin cấp quyền
        Application.OpenURL(authUrl);
        Debug.Log("[MobileLogin] Đã mở trình duyệt Android...");
    }

    private void OnDeepLinkActivated(string url)
    {
        Debug.Log($"[MobileLogin] Nhận tín hiệu Deep Link: {url}");
        string code = ExtractCodeFromUrl(url);

        if (!string.IsNullOrEmpty(code))
        {
            // Đẩy việc xử lý HTTP Request sang Background Thread để mượt game
            Task.Run(() => ExchangeCodeForTokenAsync(code));
        }
        else
        {
            Debug.LogError("[MobileLogin] URL Deep Link trả về trống hoặc không chứa mã Code hợp lệ!");
        }
    }

    private string ExtractCodeFromUrl(string url)
    {
        try
        {
            int codeIndex = url.IndexOf("code=");
            if (codeIndex == -1) return null;
            codeIndex += 5; // Nhảy qua chữ "code="

            int ampersandIndex = url.IndexOf("&", codeIndex);
            if (ampersandIndex == -1)
            {
                return url.Substring(codeIndex);
            }
            return url.Substring(codeIndex, ampersandIndex - codeIndex);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MobileLogin] Lỗi bóc tách mã Code: {e.Message}");
            return null;
        }
    }

    private async Task ExchangeCodeForTokenAsync(string code)
    {
        // Chạy ngầm trên Background Thread công nghệ sạch
        using (HttpClient client = new HttpClient())
        {
            var values = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", clientId },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" }
                // ĐÃ XÓA SỔ HOÀN TOÀN DÒNG client_secret CHUẨN ANDROID MOBILE NATIVE!
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string idToken = ExtractJsonStringValue(responseString, "id_token");

                if (string.IsNullOrEmpty(idToken))
                {
                    Debug.LogError("[MobileLogin] Phản hồi thành công nhưng id_token trống rỗng!");
                    return;
                }

                string gmail = ExtractEmailFromJWT(idToken);

                _pendingIdToken = idToken;
                _pendingEmail = gmail;
                _tokenReady = true; // Báo hiệu cho Update nhặt việc
            }
            else
            {
                Debug.LogError($"[MobileLogin] Lỗi đổi Token từ Google API: {responseString}");
            }
        }
    }

    private async Task SignInWithGoogleAsync(string googleIdToken, string gmail)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithGoogleAsync(googleIdToken);
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

            _pendingProfile = new PlayerProfile
            {
                playerInfo = playerInfo,
                Name = name,
                Email = gmail
            };
            _signInCompleted = true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

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
        catch { return string.Empty; }
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
        catch { }
        return "Không tìm thấy Email";
    }

    private void OnDestroy()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;
    }
}

[Serializable]
public struct PlayerProfile
{
    public PlayerInfo playerInfo;
    public string Name;
    public string Email;
}