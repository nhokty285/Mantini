/*using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using System.Threading.Tasks;
public class LoginControll_2 : MonoBehaviour
{
    public event Action<string> OnSignedIn; // Chỉ cần PlayerID + Email

    private async void Awake()
    {
        await UnityServices.InitializeAsync();

        // Kiểm tra đã login chưa
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await SignInWithGoogle(); // Google native!
        }
    }

    private async Task SignInWithGoogle()
    {
        try
        {
            Debug.Log("🔄 Google Sign-In...");
            await AuthenticationService.Instance.SignInWithGoogleAsync();

            string playerId = AuthenticationService.Instance.PlayerId;
            string token = AuthenticationService.Instance.AccessToken;

            // Decode lấy email
            string email = DecodeEmailFromToken(token);

            Debug.Log($"✅ GOOGLE LOGIN OK!");
            Debug.Log($"📧 Email: {email}");
            Debug.Log($"🆔 PlayerID: {playerId}");

            OnSignedIn?.Invoke(email); // Gửi email cho UI
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"❌ Google Auth fail: {ex.ErrorCode} - {ex.Message}");
        }
    }

    string DecodeEmailFromToken(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            string payload = parts[1];
            while (payload.Length % 4 != 0) payload += "=";
            byte[] data = Convert.FromBase64String(payload);
            string json = System.Text.Encoding.UTF8.GetString(data);

            // Parse đơn giản (tìm "email")
            int emailIndex = json.IndexOf("\"email\":\"");
            if (emailIndex > 0)
            {
                int start = emailIndex + 9;
                int end = json.IndexOf("\"", start);
                return json.Substring(start, end - start);
            }
        }
        catch { }
        return "Không tìm thấy email";
    }
}
*/