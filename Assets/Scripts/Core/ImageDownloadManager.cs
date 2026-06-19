using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Tải ảnh qua network + tự quản lý texture cache với memory budget.
/// Public API giữ nguyên: DownloadImage(url, onSuccess, onError).
///
/// Tối ưu so với bản cũ:
///  1. Downscale ảnh về maxTextureSize (512px) trước khi cache
///     → 1024x1024 (8MB readable) → 512x512 GPU-only (~1MB) = giảm ~8x.
///  2. Apply(false, makeNoLongerReadable: true) → xoá bản copy CPU-side.
///  3. LRU cache có memoryBudgetBytes; vượt budget → evict entry cũ nhất.
///  4. Application.lowMemory → xả cache ngay, tránh bị OS kill.
///  5. Bỏ SemaphoreSlim + polling WaitForSeconds(0.1f) (alloc mỗi 0.1s)
///     → thay bằng counter int + Queue, zero-alloc, không trễ 100ms.
///
/// Complexity:
///  - DownloadImage cache-hit: O(1) Dictionary lookup + O(1) LRU move.
///  - Eviction: O(k) với k = số entry bị xoá (amortized rất nhỏ).
///  - Không có loop nào chạy mỗi frame; sweep expiry chạy 60s/lần, O(n) trên cache.
/// </summary>
public class ImageDownloadManager : MonoBehaviour
{
    public static ImageDownloadManager Instance { get; private set; }

    [Header("Download Settings")]
    [SerializeField] private int maxConcurrentDownloads = 3;   // 3 đủ cho mobile, giảm áp lực decode
    [SerializeField] private int maxRetryAttempts = 1;
    [SerializeField] private float retryDelaySeconds = 2f;
    [SerializeField] private int timeoutSeconds = 10;
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Memory Settings (chống OOM)")]
    [Tooltip("Ảnh tải về lớn hơn sẽ bị downscale về kích thước này")]
    [SerializeField] private int maxTextureSize = 512;
    [Tooltip("Tổng budget texture cache. 48MB ≈ 45 ảnh 512x512")]
    [SerializeField] private long memoryBudgetBytes = 48L * 1024 * 1024;
    [SerializeField] private float cacheExpiryMinutes = 10f;

    // ─── Download state ───
    private int _activeDownloads;
    private readonly Queue<DownloadRequest> _waitingQueue = new();
    private readonly Dictionary<string, List<DownloadRequest>> _pendingByUrl = new();
    private WaitForSeconds _retryWait; // cache 1 lần, không new mỗi retry

    // ─── LRU texture cache ───
    private class CacheEntry
    {
        public Texture2D Texture;
        public long Size;            // bytes (GPU-only sau khi nonReadable)
        public float ExpireAt;       // Time.realtimeSinceStartup
        public LinkedListNode<string> LruNode;
    }
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly LinkedList<string> _lru = new(); // đầu list = mới dùng nhất
    private long _usedBytes;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _retryWait = new WaitForSeconds(retryDelaySeconds);
        Application.lowMemory += OnLowMemory;
        StartCoroutine(ExpirySweepLoop());
    }

    private void OnDestroy()
    {
        Application.lowMemory -= OnLowMemory;
        if (Instance == this) ClearCache();
    }

    // ═════════════════════════ PUBLIC API ═════════════════════════

    public void DownloadImage(string imageUrl, Action<Texture2D> onSuccess, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            onError?.Invoke("Empty image URL");
            return;
        }

        // 1. Cache hit — O(1)
        if (_cache.TryGetValue(imageUrl, out var entry) && entry.Texture != null)
        {
            TouchEntry(imageUrl, entry); // refresh LRU + expiry
            onSuccess?.Invoke(entry.Texture);
            return;
        }

        var request = new DownloadRequest { url = imageUrl, onSuccess = onSuccess, onError = onError };

        // 2. Đang tải cùng URL — gộp callback, không tải trùng
        if (_pendingByUrl.TryGetValue(imageUrl, out var list))
        {
            list.Add(request);
            return;
        }

        _pendingByUrl[imageUrl] = new List<DownloadRequest> { request };

        // 3. Giới hạn concurrent bằng counter — zero alloc, không polling
        if (_activeDownloads < maxConcurrentDownloads)
            StartCoroutine(DownloadRoutine(request));
        else
            _waitingQueue.Enqueue(request);
    }

    /// <summary>Xả toàn bộ cache. Gọi khi đổi scene shop hoặc khi OS báo low memory.</summary>
    public void ClearCache()
    {
        foreach (var kv in _cache)
            if (kv.Value.Texture != null) Destroy(kv.Value.Texture);

        _cache.Clear();
        _lru.Clear();
        _usedBytes = 0;
    }

    public long UsedCacheBytes => _usedBytes;

    // ═════════════════════════ DOWNLOAD ═════════════════════════

    private IEnumerator DownloadRoutine(DownloadRequest request)
    {
        _activeDownloads++;
        try { } finally { } // giữ cấu trúc rõ ràng; cleanup ở cuối routine

        for (int attempt = 0; attempt <= maxRetryAttempts; attempt++)
        {
            using (var web = UnityWebRequestTexture.GetTexture(request.url))
            {
                web.timeout = timeoutSeconds;
                yield return web.SendWebRequest();

                if (web.result == UnityWebRequest.Result.Success)
                {
                    var raw = DownloadHandlerTexture.GetContent(web);
                    if (raw != null && raw.width > 0)
                    {
                        // Downscale + drop CPU copy → đây là chỗ tiết kiệm memory chính
                        var processed = ProcessTexture(raw);
                        AddToCache(request.url, processed);
                        FinishUrl(request.url, processed, null);
                        EndDownload();
                        yield break;
                    }
                }
                else if (IsRetriable(web) && attempt < maxRetryAttempts)
                {
                    yield return _retryWait;
                    continue;
                }

                FinishUrl(request.url, null, $"Download failed: {web.error}");
                EndDownload();
                yield break;
            }
        }
    }

    private void EndDownload()
    {
        _activeDownloads--;
        // Lấy request kế tiếp từ queue — O(1)
        while (_waitingQueue.Count > 0 && _activeDownloads < maxConcurrentDownloads)
        {
            var next = _waitingQueue.Dequeue();
            // URL có thể đã được resolve trong lúc chờ → check cache trước
            if (_cache.TryGetValue(next.url, out var hit) && hit.Texture != null)
            {
                FinishUrl(next.url, hit.Texture, null);
                continue;
            }
            StartCoroutine(DownloadRoutine(next));
        }
    }

    private void FinishUrl(string url, Texture2D tex, string error)
    {
        if (!_pendingByUrl.TryGetValue(url, out var requests)) return;
        _pendingByUrl.Remove(url);

        foreach (var r in requests)
        {
            try
            {
                if (tex != null) r.onSuccess?.Invoke(tex);
                else r.onError?.Invoke(error ?? "Unknown error");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImageDownload] Callback error: {e.Message}");
            }
        }
    }

    private static bool IsRetriable(UnityWebRequest req) =>
        req.result == UnityWebRequest.Result.ConnectionError ||
        req.result == UnityWebRequest.Result.DataProcessingError ||
        (req.responseCode >= 500 && req.responseCode < 600);

    // ═════════════════════════ TEXTURE PROCESSING ═════════════════════════

    /// <summary>
    /// Downscale về maxTextureSize nếu cần, sau đó xoá bản copy CPU-side.
    /// Kết quả: texture chỉ tồn tại trên GPU → memory giảm ~50% (không resize)
    /// hoặc ~8x (resize 1024→512).
    /// </summary>
    private Texture2D ProcessTexture(Texture2D src)
    {
        int w = src.width, h = src.height;

        if (w <= maxTextureSize && h <= maxTextureSize)
        {
            // Không cần resize — chỉ drop CPU copy
            src.Apply(false, true);
            return src;
        }

        float scale = Mathf.Min((float)maxTextureSize / w, (float)maxTextureSize / h);
        int nw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
        int nh = Mathf.Max(1, Mathf.RoundToInt(h * scale));

        var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt); // GPU-side, không block lâu

        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
        dst.ReadPixels(new Rect(0, 0, nw, nh), 0, 0, false);
        dst.Apply(false, true); // upload GPU + xoá CPU copy

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Destroy(src); // bỏ texture gốc full-res ngay lập tức
        return dst;
    }

    // ═════════════════════════ LRU CACHE ═════════════════════════

    private void AddToCache(string url, Texture2D tex)
    {
        long size = (long)tex.width * tex.height * 4;

        // Evict trước khi thêm nếu vượt budget — O(k) trên số entry bị xoá
        while (_usedBytes + size > memoryBudgetBytes && _lru.Count > 0)
            EvictOldest();

        var entry = new CacheEntry
        {
            Texture = tex,
            Size = size,
            ExpireAt = Time.realtimeSinceStartup + cacheExpiryMinutes * 60f,
            LruNode = _lru.AddFirst(url)
        };
        _cache[url] = entry;
        _usedBytes += size;
    }

    private void TouchEntry(string url, CacheEntry entry)
    {
        // Move-to-front O(1) nhờ LinkedListNode được cache trong entry
        _lru.Remove(entry.LruNode);
        entry.LruNode = _lru.AddFirst(url);
        entry.ExpireAt = Time.realtimeSinceStartup + cacheExpiryMinutes * 60f;
    }

    private void EvictOldest()
    {
        var node = _lru.Last;
        if (node == null) return;
        RemoveEntry(node.Value);
    }

    private void RemoveEntry(string url)
    {
        if (!_cache.TryGetValue(url, out var entry)) return;

        _lru.Remove(entry.LruNode);
        _cache.Remove(url);
        _usedBytes -= entry.Size;

        if (entry.Texture != null) Destroy(entry.Texture);
        // Lưu ý: Sprite đang hiển thị tham chiếu texture này sẽ trắng,
        // nhưng UI slot luôn gọi DownloadImage lại khi Setup → tự hồi phục.
        // Budget 48MB + downscale khiến trường hợp này gần như không xảy ra
        // với item đang hiển thị (chúng luôn nằm đầu LRU).
    }

    /// <summary>Sweep expiry 60s/lần — O(n) trên cache nhưng tần suất rất thấp.</summary>
    private IEnumerator ExpirySweepLoop()
    {
        var wait = new WaitForSeconds(60f);
        var toRemove = new List<string>(8); // reuse buffer, không new mỗi vòng

        while (true)
        {
            yield return wait;
            toRemove.Clear();

            float now = Time.realtimeSinceStartup;
            foreach (var kv in _cache)
                if (now > kv.Value.ExpireAt) toRemove.Add(kv.Key);

            for (int i = 0; i < toRemove.Count; i++)
                RemoveEntry(toRemove[i]);
        }
    }

    private void OnLowMemory()
    {
        GameLog.Warn("[ImageDownload] OS low memory → clearing texture cache");
        ClearCache();
        Resources.UnloadUnusedAssets();
    }

    private class DownloadRequest
    {
        public string url;
        public Action<Texture2D> onSuccess;
        public Action<string> onError;
    }
}