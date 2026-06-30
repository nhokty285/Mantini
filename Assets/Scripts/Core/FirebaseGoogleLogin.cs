/*using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using GooglePlayGames.BasicApi;
using UnityEngine;
using UnityEngine.UI;*/

// Đăng nhập Google qua plugin Google Sign-In (native popup chọn tài khoản trong app)
// rồi nạp idToken vào Firebase Authentication.
// Chạy được trên Android THẬT (plugin native KHÔNG chạy trong Unity Editor).
// Luồng: GoogleSignIn.SignIn() -> idToken -> GoogleAuthProvider -> Firebase SignInWithCredentialAsync.
/*public class FirebaseGoogleLogin : MonoBehaviour
{
    public static FirebaseGoogleLogin Instance { get; private set; }

    // Bắn ra khi đăng nhập Firebase thành công, kèm FirebaseUser để UI/Onboarding dùng.
    public event System.Action<FirebaseUser> OnLoginSuccess;

    [Header("Google Sign-In Config")]
    // WEB client ID (server client ID) lấy trên Firebase Console - dùng để xin idToken.
    [SerializeField] private string webClientId = "677937019813-khcl8kb4iv0sjjgpdm9gu1sudkvlju97.apps.googleusercontent.com";

    [Header("UI (tùy chọn)")]
    [SerializeField] private Button loginButton;

    [Header("Firebase State")]
    [SerializeField] private bool _isFirebaseReady = false;

    private FirebaseAuth _auth;
    private GoogleSignInConfiguration _configuration;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Cấu hình Google Sign-In: yêu cầu idToken (bắt buộc cho Firebase) + email.
        _configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestIdToken = true,
            RequestEmail = true,
            UseGameSignIn = false
        };

        // Init Firebase trước khi dùng Auth.
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            DependencyStatus status = task.Result;
            if (status == DependencyStatus.Available)
            {
                _auth = FirebaseAuth.DefaultInstance;
                _isFirebaseReady = true;
                Debug.Log("[FirebaseGoogleLogin] Firebase init thành công, sẵn sàng đăng nhập.");
            }
            else
            {
                Debug.LogError($"[FirebaseGoogleLogin] Không init được Firebase: {status}");
            }
        });
    }

    private void OnEnable()
    {
        if (loginButton != null)
        {
            loginButton.onClick.RemoveAllListeners();
            loginButton.onClick.AddListener(StartGoogleSignIn);
        }
    }

    private void OnDisable()
    {
        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(StartGoogleSignIn);
        }
    }

    // Gắn hàm này vào nút Login, hoặc gọi từ code.
    public void StartGoogleSignIn()
    {
        if (!_isFirebaseReady)
        {
            Debug.LogError("[FirebaseGoogleLogin] Firebase chưa sẵn sàng, bỏ qua đăng nhập.");
            return;
        }

        GoogleSignIn.Configuration = _configuration;
        Debug.Log("[FirebaseGoogleLogin] Bắt đầu Google Sign-In...");

        // SignIn() mở popup chọn tài khoản native; continuation đẩy về Main Thread.
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(
            OnGoogleSignInFinished,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsCanceled)
        {
            Debug.LogError("[FirebaseGoogleLogin] Google Sign-In bị hủy.");
            return;
        }
        if (task.IsFaulted)
        {
            Debug.LogError($"[FirebaseGoogleLogin] Google Sign-In lỗi: {task.Exception}");
            return;
        }

        GoogleSignInUser googleUser = task.Result;
        Debug.Log($"[FirebaseGoogleLogin] Google OK -> email={googleUser.Email}, name={googleUser.DisplayName}");

        SignInToFirebase(googleUser.IdToken);
    }

    private void SignInToFirebase(string googleIdToken)
    {
        if (string.IsNullOrEmpty(googleIdToken))
        {
            Debug.LogError("[FirebaseGoogleLogin] idToken rỗng - kiểm tra RequestIdToken và web client ID.");
            return;
        }

        // Google không cần accessToken nên truyền null.
        Credential credential = GoogleAuthProvider.GetCredential(googleIdToken, null);
        _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("[FirebaseGoogleLogin] Firebase SignIn bị hủy.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError($"[FirebaseGoogleLogin] Firebase SignIn lỗi: {task.Exception}");
                return;
            }

            FirebaseUser user = task.Result;
            Debug.Log($"[FirebaseGoogleLogin] FIREBASE SIGN-IN THÀNH CÔNG -> uid={user.UserId}, email={user.Email}, name={user.DisplayName}");

            // Bắn event để UI/Onboarding xử lý vào game.
            OnLoginSuccess?.Invoke(user);
        });
    }
}
*/