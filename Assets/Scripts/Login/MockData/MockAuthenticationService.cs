using System;
using UnityEngine;
using System.Collections;

public class MockAuthenticationService : MonoBehaviour
{
    [Header("🧪 MOCK TESTING")]
    public bool useMockAuth = true;
    public string mockUsername = "test@example.com";
    public string mockPassword = "123456";

    public void TestMockLogin()
    {
        if (useMockAuth)
        {
            Debug.Log("🧪 MOCK LOGIN: Testing authentication flow");

            // Tạo fake JWT token (giống thật)
            string fakeAccessToken = CreateMockJWTToken(mockUsername);
            string fakeRefreshToken = $"rt_{System.Guid.NewGuid()}_{DateTimeOffset.Now.ToUnixTimeSeconds()}_7d";

            // Lưu token giống system thật
            PlayerPrefs.SetString("access_token", fakeAccessToken);
            PlayerPrefs.SetString("refresh_token", fakeRefreshToken);
            PlayerPrefs.SetString("token_expiry", DateTime.Now.AddHours(1).ToBinary().ToString());
            PlayerPrefs.Save();

            Debug.Log("✅ MOCK LOGIN SUCCESS - Token saved!");
            Debug.Log($"Access Token: {fakeAccessToken.Substring(0, 50)}...");
            Debug.Log($"Refresh Token: {fakeRefreshToken}");
        }
    }

    private string CreateMockJWTToken(string email)
    {
        var header = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{{\"sub\":\"mock-user-id\",\"email\":\"{email}\",\"exp\":{DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds()},\"iss\":\"mock-auth\"}}"
        ));
        var signature = "mock_signature_for_testing";
        return $"{header}.{payload}.{signature}";
    }
}
