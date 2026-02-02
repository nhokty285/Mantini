using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading;

public class ImageDownloadManager : MonoBehaviour
{
    public static ImageDownloadManager Instance { get; private set; }

    [Header("Download Settings")]
    [SerializeField] private int maxConcurrentDownloads = 3;
    [SerializeField] private int maxRetryAttempts = 1;
    [SerializeField] private float retryDelaySeconds = 2f;
    [SerializeField] private int timeoutSeconds = 10;
    [SerializeField] private bool enableDebugLogs = true;

    // Semaphore để giới hạn concurrent downloads
    private SemaphoreSlim downloadSemaphore;
    private readonly Queue<DownloadRequest> downloadQueue = new();
    private readonly Dictionary<string, List<DownloadRequest>> pendingRequests = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            downloadSemaphore = new SemaphoreSlim(maxConcurrentDownloads, maxConcurrentDownloads);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        downloadSemaphore?.Dispose();
    }

    // Public API để tải ảnh
    public void DownloadImage(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            onError?.Invoke("Empty image URL");
            return;
        }

        // Check cache first
        if (CacheService.Instance != null)
        {
            var cachedTexture = CacheService.Instance.GetTexture(imageUrl);
            if (cachedTexture != null)
            {
                onSuccess?.Invoke(cachedTexture);
                return;
            }
        }

        var request = new DownloadRequest
        {
            url = imageUrl,
            onSuccess = onSuccess,
            onError = onError,
            attemptCount = 0
        };

        // Check if already downloading this URL
        if (pendingRequests.ContainsKey(imageUrl))
        {
            pendingRequests[imageUrl].Add(request);
            if (enableDebugLogs)
                Debug.Log($"ImageDownload: Added to pending queue for {imageUrl}");
        }
        else
        {
            pendingRequests[imageUrl] = new List<DownloadRequest> { request };
            StartCoroutine(ProcessDownloadRequest(request));
        }
    }

    private IEnumerator ProcessDownloadRequest(DownloadRequest request)
    {
        // Wait for semaphore (limits concurrent downloads)
        yield return StartCoroutine(WaitForSemaphore());

        try
        {
            yield return StartCoroutine(DownloadWithRetry(request));
        }
        finally
        {
            // Release semaphore
            downloadSemaphore.Release();

            // Remove from pending
            if (pendingRequests.ContainsKey(request.url))
            {
                pendingRequests.Remove(request.url);
            }
        }
    }

    private IEnumerator WaitForSemaphore()
    {
        while (!downloadSemaphore.Wait(0)) // Non-blocking check
        {
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator DownloadWithRetry(DownloadRequest request)
    {
        for (int attempt = 0; attempt <= maxRetryAttempts; attempt++)
        {
            request.attemptCount = attempt + 1;

            if (enableDebugLogs && attempt > 0)
                Debug.Log($"ImageDownload: Retry attempt {attempt} for {request.url}");

            using (var webRequest = UnityWebRequestTexture.GetTexture(request.url))
            {
                webRequest.timeout = timeoutSeconds;
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(webRequest);
                    if (texture != null && texture.width > 0 && texture.height > 0)
                    {
                        // Cache the successful result
                        if (CacheService.Instance != null)
                        {
                            var cacheExpiry = TimeSpan.FromMinutes(30);
                            CacheService.Instance.SetTexture(request.url, texture, cacheExpiry);
                        }

                        // Notify all pending requests for this URL
                        NotifyPendingRequests(request.url, texture, null);
                        yield break; // Success!
                    }
                    else
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning($"ImageDownload: Invalid texture for {request.url}");
                    }
                }
                else if (IsRetriableError(webRequest))
                {
                    if (attempt < maxRetryAttempts)
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning($"ImageDownload: Retriable error for {request.url}: {webRequest.error}. Retrying in {retryDelaySeconds}s...");

                        yield return new WaitForSeconds(retryDelaySeconds);
                        continue; // Retry
                    }
                }

                // If we get here, it's either non-retriable or max attempts exceeded
                string errorMsg = $"Download failed after {attempt + 1} attempts: {webRequest.error}";
                if (enableDebugLogs)
                    Debug.LogError($"ImageDownload: {errorMsg} for {request.url}");

                NotifyPendingRequests(request.url, null, errorMsg);
                yield break;
            }
        }
    }

    private bool IsRetriableError(UnityWebRequest request)
    {
        // Retry on timeout, network issues, but not on 404, 403, etc.
        return request.result == UnityWebRequest.Result.ConnectionError ||
               request.result == UnityWebRequest.Result.DataProcessingError ||
               (request.responseCode >= 500 && request.responseCode < 600); // Server errors
    }

    private void NotifyPendingRequests(string url, Texture2D texture, string error)
    {
        if (pendingRequests.TryGetValue(url, out var requests))
        {
            foreach (var request in requests)
            {
                try
                {
                    if (texture != null)
                    {
                        request.onSuccess?.Invoke(texture);
                    }
                    else
                    {
                        request.onError?.Invoke(error ?? "Unknown error");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in download callback: {e.Message}");
                }
            }
        }
    }

    // Debug method
    public void LogDownloadStats()
    {
        Debug.Log($"=== ImageDownload Statistics ===");
        Debug.Log($"Available slots: {downloadSemaphore.CurrentCount}/{maxConcurrentDownloads}");
        Debug.Log($"Pending URLs: {pendingRequests.Count}");
    }

    private class DownloadRequest
    {
        public string url;
        public Action<Texture2D> onSuccess;
        public Action<string> onError;
        public int attemptCount;
    }
}
