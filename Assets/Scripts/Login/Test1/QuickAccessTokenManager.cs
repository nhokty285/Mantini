/*// QuickAccessTokenManager.cs
using UnityEngine;

public class QuickAccessTokenManager : MonoBehaviour
{
    [Header("🔑 Quick Access Token")]
    [TextArea(3, 10)]
    [SerializeField] private string myAccessToken = ""; // Paste access token của bạn ở đây

    [Header("Auto Login Settings")]
    [SerializeField] private bool autoLoginOnStart = true;
    [SerializeField] private bool showLoginPanel = false;

    private void Start()
    {
        if (autoLoginOnStart && !string.IsNullOrEmpty(myAccessToken))
        {
            Debug.Log("🚀 Auto-login with stored access token...");
            LoginWithStoredToken();
        }
    }

    [ContextMenu("Login with Stored Token")]
    public void LoginWithStoredToken()
    {
        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.LoginWithAccessToken(myAccessToken);
        }
        else
        {
            Debug.LogError("GoogleAuthService not found!");
        }
    }

    [ContextMenu("Clear Token")]
    public void ClearStoredToken()
    {
        myAccessToken = "";
        Debug.Log("🗑️ Access token cleared");
    }

    // Method để set token từ code khác
    public void SetAccessToken(string token)
    {
        myAccessToken = token;
        Debug.Log("🔑 Access token updated");
    }

    // Method để login ngay lập tức từ UI button
    public void QuickLogin()
    {
        if (!string.IsNullOrEmpty(myAccessToken))
        {
            LoginWithStoredToken();
        }
        else
        {
            Debug.LogWarning("⚠️ No access token provided!");
        }
    }
}
*/