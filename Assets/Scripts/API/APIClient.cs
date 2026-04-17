using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;


public class APIClient : MonoBehaviour
{
    public static APIClient Instance { get; private set; }

    [Header("Auth")]
    [SerializeField] private string token = "PASTE_YOUR_BEARER_TOKEN";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // GET với full URL
    public void GetFull(string fullUrl, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(GetRequest(fullUrl, onSuccess, onError));
    }

    private IEnumerator GetRequest(string url, Action<string> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Accept", "application/json");

            Debug.Log("[API] GET " + url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onError?.Invoke($"[{req.responseCode}] {req.url}\n{req.error}\n{req.downloadHandler.text}");
        }
    }

    // PUT JSON với full URL
    public void PutJsonFull(string fullUrl, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(PutJsonRequest(fullUrl, jsonBody, onSuccess, onError));
    }

    private IEnumerator PutJsonRequest(string url, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        byte[] data = Encoding.UTF8.GetBytes(jsonBody);
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");

            Debug.Log($"[API] PUT {url}\nBody: {jsonBody}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onError?.Invoke($"[{req.responseCode}] {req.url}\n{req.error}\n{req.downloadHandler.text}");
        }
    }

    // APIClient.cs  (thêm mới)
    public void PostJsonFull(string fullUrl, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(PostJsonRequest(fullUrl, jsonBody, onSuccess, onError));
    }

    private IEnumerator PostJsonRequest(string url, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            Debug.Log($"[API] POST {url}\nBody: {jsonBody}");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onSuccess?.Invoke(req.downloadHandler.text);
            else onError?.Invoke($"[{req.responseCode}] {req.url}\n{req.error}\n{req.downloadHandler.text}");
        }
    }
    public void DeleteFull(string fullUrl, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(DeleteRequest(fullUrl, onSuccess, onError));
    }

    private IEnumerator DeleteRequest(string url, Action<string> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Delete(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();              // ✅ tránh NRE
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Accept", "application/json");
            Debug.Log("[API] DELETE " + url);

            yield return req.SendWebRequest();

            string body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Backend thường trả "true", nhưng nếu rỗng thì coi là true luôn
                onSuccess?.Invoke(string.IsNullOrEmpty(body) ? "true" : body);
            }
            else
            {
                string err = $"[{req.responseCode}] {req.url} {(req.error ?? "error")} {body}";
                Debug.LogError(err);
                onError?.Invoke(err);
            }
        }
    }
    /*
        private static readonly Encoding _utf8 = new UTF8Encoding(false);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ✅ Helper: set headers chung, tránh lặp code O(n) lines → O(1) call
        private void SetCommonHeaders(UnityWebRequest req, bool hasBody = false)
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Accept", "application/json");
            if (hasBody) req.SetRequestHeader("Content-Type", "application/json");
        }

        public void GetFull(string fullUrl, Action<string> onSuccess, Action<string> onError)
            => StartCoroutine(SendRequest(UnityWebRequest.Get(fullUrl), fullUrl, "GET", onSuccess, onError));

        public void PutJsonFull(string fullUrl, string jsonBody, Action<string> onSuccess, Action<string> onError)
            => StartCoroutine(SendBodyRequest(fullUrl, UnityWebRequest.kHttpVerbPUT, jsonBody, onSuccess, onError));

        public void PostJsonFull(string fullUrl, string jsonBody, Action<string> onSuccess, Action<string> onError)
            => StartCoroutine(SendBodyRequest(fullUrl, UnityWebRequest.kHttpVerbPOST, jsonBody, onSuccess, onError));

        public void DeleteFull(string fullUrl, Action<string> onSuccess, Action<string> onError)
        {
            var req = UnityWebRequest.Delete(fullUrl);
            req.downloadHandler = new DownloadHandlerBuffer();
            StartCoroutine(SendRequest(req, fullUrl, "DELETE", onSuccess, onError, allowEmptyBody: true));
        }

        // ✅ Unified body request: PUT/POST dùng chung — Time: O(1), Space: O(n) với n = json size
        private IEnumerator SendBodyRequest(string url, string verb, string jsonBody,
            Action<string> onSuccess, Action<string> onError)
        {
            byte[] data = _utf8.GetBytes(jsonBody); // dùng cached encoding
            using var req = new UnityWebRequest(url, verb)
            {
                uploadHandler = new UploadHandlerRaw(data),
                downloadHandler = new DownloadHandlerBuffer()
            };
            SetCommonHeaders(req, hasBody: true);
            Debug.Log($"[API] {verb} {url}\nBody: {jsonBody}");
            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError);
        }

        // ✅ Unified response handler — loại bỏ copy-paste logic ở mọi coroutine
        private IEnumerator SendRequest(UnityWebRequest req, string url, string verb,
            Action<string> onSuccess, Action<string> onError, bool allowEmptyBody = false)
        {
            using (req)
            {
                SetCommonHeaders(req);
                Debug.Log($"[API] {verb} {url}");
                yield return req.SendWebRequest();
                HandleResponse(req, onSuccess, onError, allowEmptyBody);
            }
        }

        private void HandleResponse(UnityWebRequest req, Action<string> onSuccess,
            Action<string> onError, bool allowEmptyBody = false)
        {
            string body = req.downloadHandler?.text ?? string.Empty;
            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(allowEmptyBody && string.IsNullOrEmpty(body) ? "true" : body);
            else
            {
                string err = $"[{req.responseCode}] {req.url} {req.error ?? "error"} {body}";
                Debug.LogError(err);
                onError?.Invoke(err);
            }
        }*/
}
