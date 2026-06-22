/*using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;

public class LoginController : MonoBehaviour
{
    public event Action<PlayerProfile> OnSignedIn;
    public event Action<PlayerProfile> OnAvatarUpdate;

    private PlayerInfo playerInfo;

    private PlayerProfile playerProfile;
    public PlayerProfile PlayerProfile => playerProfile;


    private async void Awake()
    {
        await UnityServices.InitializeAsync();
        PlayerAccountService.Instance.SignedIn += SignedIn;
    }

    private async void SignedIn()
    {
        try
        {
            var accessToken = PlayerAccountService.Instance.AccessToken;
            await SignInWithUnityAsync(accessToken);



        }
        catch (Exception ex)
        {
            GameLog.Info(ex.Message);
        }
    }

    public async Task InitSignIn()
    {
        await PlayerAccountService.Instance.StartSignInAsync();
    }

    private async Task SignInWithUnityAsync(string accessToken)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithUnityAsync(accessToken);
            GameLog.Info("SignIn is successful.");

            playerInfo = AuthenticationService.Instance.PlayerInfo;

            string name;
            try
            {
                // Ưu tiên: auto-gen nếu chưa có name
                name = await AuthenticationService.Instance.GetPlayerNameAsync(true);
            }
            catch (RequestFailedException)  // Bắt riêng 500 server error
            {
                // Fallback siêu an toàn: dùng PlayerId ngắn gọn
                name = $"Player_{playerInfo.Id.Substring(playerInfo.Id.Length - 8)}";
                GameLog.Warn("Server name fail, dùng fallback: " + name);
            }

            playerProfile.playerInfo = playerInfo;
            playerProfile.Name = name;

            OnSignedIn?.Invoke(playerProfile);
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
    }

    private void OnDestroy()
    {
        PlayerAccountService.Instance.SignedIn -= SignedIn;
    }
}


[Serializable]
public struct PlayerProfile
{
    public PlayerInfo playerInfo;
    public string Name;
}*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class LoginController : MonoBehaviour
{
    public event Action<PlayerProfile> OnSignedIn;

    [Header("Google OAuth Config")]
    [SerializeField] private string clientId = "";
    [SerializeField] private string clientSecret = "";
    private const string RedirectUri = "http://localhost:8080/";

    private PlayerInfo playerInfo;
    private PlayerProfile playerProfile;
    public PlayerProfile PlayerProfile => playerProfile;

    private HttpListener _httpListener;

    // --- Main Thread marshalling ---
    // Toàn bộ luồng OAuth (HttpListener + HttpClient) chạy trên BACKGROUND THREAD.
    // Mọi Unity API (JsonUtility, AuthenticationService, event UI...) BẮT BUỘC chạy Main Thread.
    // Nguyên tắc: background thread CHỈ làm network + xử lý chuỗi bằng .NET thuần,
    // rồi set cờ; Update() (Main Thread) nhặt cờ và gọi các Unity API.
    // Tất cả O(1)/frame, không alloc, login chỉ chạy 1 lần nên cost không đáng kể.

    // Cờ 1: token đã đổi xong từ Google -> Main Thread gọi SignInWithGoogleAsync.
    private volatile bool _tokenReady = false;
    private string _pendingIdToken;
    private string _pendingEmail;

    // Cờ 2: đã SignIn vào Unity Authentication xong -> Main Thread bắn OnSignedIn.
    private volatile bool _signInCompleted = false;
    private PlayerProfile _pendingProfile;

    private async void Awake()
    {
        // Khởi tạo dịch vụ Unity Services khi vào game
        await UnityServices.InitializeAsync();
    }

    private void Update()
    {
        // Chạy trên Main Thread.

        // (1) Background thread đã đổi token xong -> gọi Unity Authentication trên Main Thread.
        if (_tokenReady)
        {
            _tokenReady = false;
            Debug.Log("[LoginController] [DEBUG] Update phát hiện _tokenReady -> gọi SignInWithGoogleAsync");
            // Fire-and-forget, nhưng chạy từ Main Thread nên các Unity API bên trong hợp lệ.
            _ = SignInWithGoogleAsync(_pendingIdToken, _pendingEmail);
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

    // Hàm này gắn vào nút bấm Login bằng Google của bạn
    public async Task InitSignIn()
    {
        // prompt=select_account để luôn hiện danh sách chọn Gmail
        // QUAN TRỌNG: phải có scope "openid" thì Google mới trả về id_token
        // (SignInWithGoogleAsync cần id_token, không phải access_token).
        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                          $"client_id={clientId}&" +
                          $"redirect_uri={RedirectUri}&" +
                          $"response_type=code&" +
                          $"scope=openid%20email%20profile&" +
                          $"prompt=select_account";

        // Mở trình duyệt để người dùng chọn tài khoản
        Application.OpenURL(authUrl);

        // Chạy server ngầm để chờ hứng mã Code từ Google trả về
        Task.Run(() => StartLocalServerAsync());
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

            // Trả về giao diện web thông báo thành công cho người dùng thấy
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
                // Đổi mã Code lấy id_token
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
        // CHÚ Ý: hàm này chạy trên BACKGROUND THREAD.
        // Chỉ được dùng .NET thuần (HttpClient, string). KHÔNG gọi Unity API ở đây.
        using (HttpClient client = new HttpClient())
        {
            var values = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "redirect_uri", RedirectUri },
                { "grant_type", "authorization_code" }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
            var responseString = await response.Content.ReadAsStringAsync();

            // [DEBUG] In nguyên response thô từ Google để soi chính xác Google trả về gì.
            // CHÚ Ý: chứa token nhạy cảm - chỉ dùng khi debug, gỡ trước khi build production.
            Debug.Log($"[LoginController] [DEBUG] Raw token response = {responseString}");

            if (response.IsSuccessStatusCode)
            {
                // Parse bằng .NET thuần thay cho JsonUtility (JsonUtility chỉ chạy Main Thread).
                string accessToken = ExtractJsonStringValue(responseString, "access_token");
                string idToken = ExtractJsonStringValue(responseString, "id_token");

                // [DEBUG] In access_token + id_token để kiểm tra.
                Debug.Log($"[LoginController] [DEBUG] access_token = {accessToken}");
                Debug.Log($"[LoginController] [DEBUG] id_token (length={idToken.Length}) = {idToken}");

                // Nếu không có id_token -> KHÔNG gọi SignIn (sẽ gây 'external token not provided').
                // Báo lỗi rõ ràng để biết đường sửa (thường do thiếu scope openid).
                if (string.IsNullOrEmpty(idToken))
                {
                    Debug.LogError("[LoginController] id_token RỖNG - Google không trả id_token. " +
                                   "Kiểm tra scope có 'openid' chưa, và OAuth client config.");
                    return;
                }

                // Giải mã id_token lấy email (toàn .NET thuần nên an toàn ở background thread).
                string gmail = ExtractEmailFromJWT(idToken);
                Debug.LogWarning($"[LoginController] [THÀNH CÔNG] Đã nhận diện Gmail: {gmail}");

                // Đẩy id_token + email sang Main Thread; Update() sẽ gọi SignInWithGoogleAsync.
                _pendingIdToken = idToken;
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
        // Hàm này được gọi từ Update() => đang ở Main Thread => Unity Services hợp lệ.
        try
        {
            Debug.Log("[LoginController] [DEBUG] Bắt đầu SignInWithGoogleAsync...");

            // Đăng nhập chuẩn vào Unity bằng Google Id Token
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

            // Gói profile vào buffer; Update() sẽ bắn OnSignedIn ở frame kế tiếp.
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
            // Bắt mọi exception còn lại để KHÔNG bị nuốt im (hàm này là fire-and-forget).
            Debug.LogError("[LoginController] Exception KHÁC khi SignIn Google (trước đây bị giấu):");
            Debug.LogException(ex);
        }
    }

    // Trích giá trị string của 1 key trong JSON, bằng .NET thuần (an toàn mọi thread).
    // Chịu được cả JSON dính liền ("key":"value") lẫn JSON có format đẹp ("key": "value")
    // bằng cách nhảy qua dấu ':' và mọi khoảng trắng trước dấu '"' mở giá trị.
    private string ExtractJsonStringValue(string json, string key)
    {
        try
        {
            // Tìm vị trí của khóa "key" (kèm dấu nháy 2 đầu để tránh khớp nhầm).
            string keyPattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(keyPattern);
            if (keyIndex < 0) return string.Empty;

            // Bắt đầu duyệt ngay sau khóa, tìm dấu ':'.
            int i = keyIndex + keyPattern.Length;
            int colon = json.IndexOf(':', i);
            if (colon < 0) return string.Empty;

            // Sau dấu ':' có thể có khoảng trắng -> nhảy qua tới dấu '"' mở giá trị.
            int valueStart = json.IndexOf('"', colon + 1);
            if (valueStart < 0) return string.Empty;
            valueStart += 1; // bỏ qua chính dấu '"' mở

            // Tìm dấu '"' đóng giá trị (token Google không chứa dấu '"' bên trong).
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

                // Dùng chung hàm parse để chịu được cả khoảng trắng (an toàn hơn).
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

    private void OnDestroy()
    {
        // Dọn dẹp listener nếu người chơi thoát giữa chừng khi đang chờ OAuth.
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
    public string id_token; // Bắt buộc phải có chuỗi này cho SignInWithGoogleAsync
}

[Serializable]
public struct PlayerProfile
{
    public PlayerInfo playerInfo;
    public string Name;
    public string Email;
}