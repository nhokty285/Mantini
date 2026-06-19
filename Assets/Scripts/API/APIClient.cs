using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Centralized HTTP client cho toàn bộ Mantini API. Mọi script cần gọi HTTP
/// phải đi qua <see cref="Instance"/> — KHÔNG tự tạo UnityWebRequest riêng.
/// </summary>
public class APIClient : MonoBehaviour
{
    public static APIClient Instance { get; private set; }

    [Header("Auth")]
    [Tooltip("lay token cho API tai msvn-pro.fo.staging.k.hubcom.tech ")]
    [SerializeField] private string token = "PASTE_YOUR_BEARER_TOKEN";

    // Cache encoding 1 lần — UTF8Encoding(false) = no BOM
    private static readonly Encoding _utf8 = new UTF8Encoding(false);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC API — HTTP verbs
    // ═══════════════════════════════════════════════════════════════════════

    public void GetFull(string fullUrl, Action<string> onSuccess, Action<string> onError)
        => StartCoroutine(SendRequest(UnityWebRequest.Get(fullUrl), fullUrl, "GET", onSuccess, onError));

    public void PutJsonFull(string fullUrl, string jsonBody, Action<string> onSuccess, Action<string> onError)
        => StartCoroutine(SendBodyRequest(fullUrl, UnityWebRequest.kHttpVerbPUT, jsonBody, onSuccess, onError));

    public void PostJsonFull(string fullUrl, string jsonBody, Action<string> onSuccess, Action<string> onError)
        => StartCoroutine(SendBodyRequest(fullUrl, UnityWebRequest.kHttpVerbPOST, jsonBody, onSuccess, onError));

    public void DeleteFull(string fullUrl, Action<string> onSuccess, Action<string> onError)
    {
        var req = UnityWebRequest.Delete(fullUrl);
        req.downloadHandler = new DownloadHandlerBuffer(); // tránh NRE khi server trả body
        StartCoroutine(SendRequest(req, fullUrl, "DELETE", onSuccess, onError, allowEmptyBody: true));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INTERNAL — Coroutines & helpers
    // ═══════════════════════════════════════════════════════════════════════

    // Helper: set headers chung — đỡ duplicate ở mỗi verb
    private void SetCommonHeaders(UnityWebRequest req, bool hasBody = false)
    {
        req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Accept", "application/json");
        if (hasBody) req.SetRequestHeader("Content-Type", "application/json");
    }

    // Unified body request: PUT/POST dùng chung — Time: O(1), Space: O(n) với n = json size
    private IEnumerator SendBodyRequest(string url, string verb, string jsonBody,
        Action<string> onSuccess, Action<string> onError)
    {
        byte[] data = _utf8.GetBytes(jsonBody);
        using var req = new UnityWebRequest(url, verb)
        {
            uploadHandler = new UploadHandlerRaw(data),
            downloadHandler = new DownloadHandlerBuffer()
        };
        SetCommonHeaders(req, hasBody: true);

#if UNITY_EDITOR
        // Body có thể chứa token/PII — chỉ log khi build editor để tránh leak qua logcat trên Android
        GameLog.Info($"[APIClient] {verb} {url}\nBody: {jsonBody}");
#endif

        yield return req.SendWebRequest();
        HandleResponse(req, onSuccess, onError);
    }

    // Unified non-body request: GET/DELETE dùng chung
    private IEnumerator SendRequest(UnityWebRequest req, string url, string verb,
        Action<string> onSuccess, Action<string> onError, bool allowEmptyBody = false)
    {
        using (req)
        {
            SetCommonHeaders(req);

#if UNITY_EDITOR
            GameLog.Info($"[APIClient] {verb} {url}");
#endif

            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError, allowEmptyBody);
        }
    }

    private void HandleResponse(UnityWebRequest req, Action<string> onSuccess,
        Action<string> onError, bool allowEmptyBody = false)
    {
        string body = req.downloadHandler?.text ?? string.Empty;

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(allowEmptyBody && string.IsNullOrEmpty(body) ? "true" : body);
        }
        else
        {
            string err = $"[{req.responseCode}] {req.url} {req.error ?? "error"} {body}";
            Debug.LogError($"[APIClient] {err}");
            onError?.Invoke(err);
        }
    }
}