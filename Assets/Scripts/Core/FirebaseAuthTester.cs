using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;

public class FirebaseAuthTester : MonoBehaviour
{
    public static FirebaseAuthTester Instance { get; private set; }

    // Bắn ra khi Firebase đăng nhập thành công (chạy trên Main Thread nhờ ContinueWithOnMainThread).
    // LoginController (Android) lắng nghe event này để bắn OnSignedIn, thay cho Unity Authentication.
    public event Action<FirebaseUser> OnFirebaseSignedIn;

    [Header("Firebase State")]
    [SerializeField] private bool _isFirebaseReady = false;

    private FirebaseAuth _auth;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Init Firebase: kiểm tra & vá dependencies (Google Play services...) trước khi dùng.
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status == DependencyStatus.Available)
            {
                _auth = FirebaseAuth.DefaultInstance;
                _isFirebaseReady = true;
                Debug.Log("[FirebaseAuthTester] Firebase init thành công, sẵn sàng đăng nhập.");
            }
            else
            {
                Debug.LogError($"[FirebaseAuthTester] Không init được Firebase: {status}");
            }
        });
    }

    // Nạp idToken (và accessToken) lấy từ luồng OAuth sẵn có vào Firebase.
    public void SignInWithGoogleIdToken(string idToken, string accessToken)
    {
        Debug.Log($"[CheckToken] idToken Length: {idToken?.Length} | Content: {idToken}");
        if (!_isFirebaseReady || _auth == null)
        {
            Debug.LogError("[FirebaseAuthTester] Firebase chưa sẵn sàng, bỏ qua đăng nhập.");
            return;
        }

        Credential credential = GoogleAuthProvider.GetCredential(idToken, accessToken);
        _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled) { Debug.LogError("[FirebaseAuthTester] SignIn bị hủy."); return; }
            if (task.IsFaulted) { Debug.LogError($"[FirebaseAuthTester] SignIn lỗi: {task.Exception}"); return; }

            FirebaseUser user = task.Result;
            Debug.Log($"[FirebaseAuthTester] Firebase SignIn OK -> uid={user.UserId}, email={user.Email}, name={user.DisplayName}");

            // Callback chạy trên Main Thread => bắn event trực tiếp an toàn cho UI / Unity API.
            OnFirebaseSignedIn?.Invoke(user);
        });
    }
}
