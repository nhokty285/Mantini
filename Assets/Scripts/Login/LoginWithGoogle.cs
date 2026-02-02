using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class GoogleEmailLogin : MonoBehaviour
{
    async void StartGoogleLogin()
    {
        await UnityServices.InitializeAsync();

        try
        {
            // ✅ GOOGLE SIGN-IN (KHÔNG phải Unity popup)
            string idToken = await GetGoogleIdToken();
            await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);

            Debug.Log($"✅ PlayerID: {AuthenticationService.Instance.PlayerId}");
            DecodeToken(); // Email ở đây!
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Google Login fail: {ex.Message}");
        }
    }

    async System.Threading.Tasks.Task<string> GetGoogleIdToken()
    {
        // Implement your Google ID token retrieval logic here
        // This should call Google's native sign-in on your platform
        return await System.Threading.Tasks.Task.FromResult("");
    }

    void DecodeToken()
    {
        string token = AuthenticationService.Instance.AccessToken;
        string[] parts = token.Split('.');
        if (parts.Length == 3)
        {
            string payload = parts[1];
            while (payload.Length % 4 != 0) payload += "=";
            byte[] data = Convert.FromBase64String(payload);
            string json = System.Text.Encoding.UTF8.GetString(data);

            Debug.Log($"🔍 GOOGLE TOKEN: {json}");
            // ← SẼ THẤY: "email": "your@gmail.com"
        }
    }
}
