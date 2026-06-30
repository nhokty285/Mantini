using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cache service tối ưu cho Mantini — giữ nguyên ICacheService nên các caller
/// (ShopAPIManager, ShopItemUI legacy...) KHÔNG cần sửa.
///
/// Thay đổi so với bản cũ:
///  1. LRU thật sự bằng LinkedList + node map → Touch/Evict O(1)
///     (bản cũ là LFU sai logic, O(n) + ToList() alloc mỗi lần evict).
///  2. Eviction lặp WHILE cho đến khi đủ chỗ (bản cũ chỉ evict 1 item).
///  3. Texture2D LUÔN được Destroy khi remove/evict/clear → hết leak native memory.
///  4. Bỏ GC.Collect() thủ công (gây spike frame — vi phạm skill performance).
///  5. Gộp 3 dictionary (cache/expiryTimes/memorySizes) về 1 — entry là
///     single source of truth, không còn sync thủ công.
///  6. Dictionary thường thay ConcurrentDictionary (main-thread only).
///  7. Sweep expiry dùng buffer tái sử dụng + WaitForSeconds cache sẵn — zero alloc.
///
/// Complexity tổng kết:
///  - Get/Set/Remove: O(1) average (Dictionary + LinkedList node).
///  - Evict 1 entry: O(1). Evict k entries khi over budget: O(k).
///  - Sweep expiry: O(n) nhưng chỉ chạy mỗi cleanupIntervalSeconds.
/// </summary>
public class CacheService : MonoBehaviour, ICacheService
{
    public static CacheService Instance { get; private set; }

    [Header("Cache Configuration")]
    [SerializeField] private int maxCacheSize = 100;
    [SerializeField] private long maxMemoryUsage = 32L * 1024 * 1024; // 32MB cho JSON/ShopItems là quá đủ
    [SerializeField] private float cleanupIntervalSeconds = 120f;
    [SerializeField] private bool enableDebugLogs = false;

    // ─── Single source of truth ───
    private readonly Dictionary<string, ICacheEntry> _cache = new();
    // LRU: đầu list = mới dùng nhất. Node được map riêng để Touch O(1).
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new();

    private long _totalMemoryUsage;
    private int _hitCount;
    private int _missCount;

    // Reusable buffers — không new mỗi sweep
    private readonly List<string> _expiredBuffer = new(16);
    private WaitForSeconds _cleanupWait;

    public int Count => _cache.Count;
    public long TotalMemoryUsage => _totalMemoryUsage;
    public float HitRatio => (_hitCount + _missCount) > 0
        ? (float)_hitCount / (_hitCount + _missCount) : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);   // DontDestroyOnLoad chỉ hoạt động cho ROOT GameObject — tách khỏi parent trước
        DontDestroyOnLoad(gameObject);

        _cleanupWait = new WaitForSeconds(cleanupIntervalSeconds);
        StartCoroutine(CleanupExpiredLoop());
        Application.lowMemory += OnLowMemory;
    }

    private void OnDestroy()
    {
        Application.lowMemory -= OnLowMemory;
        if (Instance == this) Clear();
    }

    // ═════════════════════ Generic Cache Methods ═════════════════════

    public T Get<T>(string key)
    {
        return TryGet<T>(key, out T value) ? value : default;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default;
        if (string.IsNullOrEmpty(key)) return false;

        if (_cache.TryGetValue(key, out ICacheEntry cached))
        {
            if (cached.IsExpired)
            {
                Remove(key);
                _missCount++;
                return false;
            }

            if (cached is CacheEntry<T> typed)
            {
                typed.IncrementAccess();
                Touch(key);            // move-to-front O(1)
                value = typed.Value;
                _hitCount++;           // đếm 1 nơi duy nhất — hết double-count
                return true;
            }

            // Type mismatch → entry hỏng, dọn luôn
            if (enableDebugLogs)
                GameLog.Warn($"[CacheService] Type mismatch for '{key}'");
            Remove(key);
        }

        _missCount++;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan expiry)
    {
        if (string.IsNullOrEmpty(key) || value == null) return;

        // Xoá entry cũ trước (trả memory về pool đếm) rồi mới tính budget
        Remove(key);

        var entry = new CacheEntry<T>(value, expiry);

        // ✅ FIX: lặp evict cho đến khi ĐỦ CHỖ — bản cũ chỉ evict 1 lần
        while ((_totalMemoryUsage + entry.MemorySize > maxMemoryUsage
                || _cache.Count >= maxCacheSize)
               && _lru.Count > 0)
        {
            EvictLeastRecentlyUsed();
        }

        _cache[key] = entry;
        _lruNodes[key] = _lru.AddFirst(key);
        _totalMemoryUsage += entry.MemorySize;
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!_cache.TryGetValue(key, out ICacheEntry entry)) return;

        _cache.Remove(key);
        _totalMemoryUsage -= entry.MemorySize;

        if (_lruNodes.TryGetValue(key, out var node))
        {
            _lru.Remove(node);          // O(1) nhờ giữ node, không phải scan list
            _lruNodes.Remove(key);
        }

        DestroyIfTexture(entry);        // ✅ FIX: luôn giải phóng native memory
    }

    public bool Contains(string key)
        => !string.IsNullOrEmpty(key) && _cache.ContainsKey(key);

    public void Clear()
    {
        foreach (var kv in _cache)
            DestroyIfTexture(kv.Value);

        _cache.Clear();
        _lru.Clear();
        _lruNodes.Clear();
        _totalMemoryUsage = 0;
    }

    // ═════════════════════ Specialized Methods ═════════════════════

    public void SetTexture(string key, Texture2D texture, TimeSpan expiry)
        => Set(key, texture, expiry);

    public Texture2D GetTexture(string key)
        => Get<Texture2D>(key);

    public void SetShopItems(string key, List<ShopItem> items, TimeSpan expiry)
        => Set(key, items, expiry);

    public List<ShopItem> GetShopItems(string key)
        => Get<List<ShopItem>>(key);

    // ═════════════════════ Internal ═════════════════════

    private void Touch(string key)
    {
        if (_lruNodes.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lruNodes[key] = _lru.AddFirst(key);
        }
    }

    /// <summary>Evict đúng nghĩa LRU: phần tử CUỐI list = lâu chưa dùng nhất. O(1).</summary>
    private void EvictLeastRecentlyUsed()
    {
        var last = _lru.Last;
        if (last == null) return;

        if (enableDebugLogs)
            GameLog.Info($"[CacheService] Evict LRU '{last.Value}'");

        Remove(last.Value);
    }

    private void DestroyIfTexture(ICacheEntry entry)
    {
        // Texture2D là NATIVE memory — C# GC không thu hồi được, phải Destroy.
        // Lưu ý: nếu module nào tự quản lý vòng đời texture (như
        // ImageDownloadManager bản mới có cache nội bộ riêng) thì KHÔNG đưa
        // texture đó vào CacheService — tránh double-ownership.
        if (entry is CacheEntry<Texture2D> texEntry && texEntry.Value != null)
            Destroy(texEntry.Value);
    }

    private IEnumerator CleanupExpiredLoop()
    {
        while (true)
        {
            yield return _cleanupWait;          // reuse, không new mỗi vòng

            _expiredBuffer.Clear();
            var now = DateTime.Now;             // 1 lần / sweep, không per-entry

            foreach (var kv in _cache)
                if (now > kv.Value.ExpiryTime)
                    _expiredBuffer.Add(kv.Key);

            for (int i = 0; i < _expiredBuffer.Count; i++)
                Remove(_expiredBuffer[i]);

            if (enableDebugLogs && _expiredBuffer.Count > 0)
                GameLog.Info($"[CacheService] Cleaned {_expiredBuffer.Count} expired items");
        }
    }

    // ═════════════════════ Lifecycle hooks ═════════════════════

    private void OnApplicationPause(bool pauseStatus)
    {
        // Khi app vào background: OS Android có thể kill app chiếm nhiều RAM.
        // Xả 50% cache (đuôi LRU) + unload asset. KHÔNG GC.Collect() —
        // GC spike khi resume tệ hơn nhiều so với để GC tự chạy.
        if (!pauseStatus) return;

        int toEvict = _lru.Count / 2;
        for (int i = 0; i < toEvict; i++)
            EvictLeastRecentlyUsed();

        Resources.UnloadUnusedAssets();
    }
    // ❌ Bỏ OnApplicationFocus — kéo notification shade cũng trigger,
    //    dọn cache lúc đó vừa thừa vừa gây hitch khi quay lại.

    private void OnLowMemory()
    {
        GameLog.Warn("[CacheService] OS low memory → clear all");
        Clear();
        Resources.UnloadUnusedAssets();
    }

    public void LogCacheStats()
    {
        GameLog.Info($"=== CacheService Statistics ===\n" +
                  $"Items: {Count}/{maxCacheSize}\n" +
                  $"Memory: {_totalMemoryUsage / 1024f / 1024f:F2}MB / {maxMemoryUsage / 1024f / 1024f:F2}MB\n" +
                  $"Hit Ratio: {HitRatio:P} (hits {_hitCount}, misses {_missCount})");
    }
}
