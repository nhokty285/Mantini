using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.Concurrent;
using System.Linq;

public class CacheService : MonoBehaviour, ICacheService
{
    public static CacheService Instance { get; private set; }

    [Header("Cache Configuration")]
    [SerializeField] private int maxCacheSize = 100;
    [SerializeField] private long maxMemoryUsage = 50 * 1024 * 1024; // 50MB
    [SerializeField] private float cleanupIntervalSeconds = 300f; // 5 minutes
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool destroyTextureOnEvict = false;
    // FIXED: Proper generic typing
    private readonly ConcurrentDictionary<string, ICacheEntry> cache = new();
    private readonly Dictionary<string, DateTime> expiryTimes = new();
    private readonly Dictionary<string, long> memorySizes = new();

    // Cache statistics
    private long totalMemoryUsage = 0;
    private int hitCount = 0;
    private int missCount = 0;

    // Cleanup coroutine
    private Coroutine cleanupCoroutine;

    public int Count => cache.Count;
    public long TotalMemoryUsage => totalMemoryUsage;
    public float HitRatio => hitCount + missCount > 0 ? (float)hitCount / (hitCount + missCount) : 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartCleanupCoroutine();

            if (enableDebugLogs)
                Debug.Log("CacheService initialized successfully");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        StopCleanupCoroutine();
        Clear();
    }

    #region Generic Cache Methods

    public T Get<T>(string key)
    {
        if (TryGet<T>(key, out T value))
        {
            hitCount++;
            return value;
        }

        missCount++;
        return default(T);
    }

    public bool TryGet<T>(string key, out T value)
    {   
        if (string.IsNullOrEmpty(key))
        {
            value = default(T);
            return false;
        }

        if (cache.TryGetValue(key, out ICacheEntry cachedEntry))
        {
            // Check if expired
            if (cachedEntry.IsExpired)
            {
                Remove(key);
                value = default(T);
                return false;
            }

            // FIXED: Safe casting with proper type checking
            if (cachedEntry is CacheEntry<T> typedEntry)
            {
                typedEntry.IncrementAccess();
                value = typedEntry.Value;
                hitCount++;
                return true;
            }
            else
            {
                // Type mismatch - log warning and remove invalid entry
                if (enableDebugLogs)
                    Debug.LogWarning($"CacheService: Type mismatch for key '{key}'. Expected {typeof(T).Name}, got {cachedEntry.GetType().Name}");
                Remove(key);
            }
        }

        value = default(T);
        missCount++;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan expiry)
    {
        if (string.IsNullOrEmpty(key) || value == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"CacheService: Invalid key or value for caching");
            return;
        }

        // Create cache entry
        var entry = new CacheEntry<T>(value, expiry);

        // Check memory limits
        if (totalMemoryUsage + entry.MemorySize > maxMemoryUsage)
        {
            EvictLeastRecentlyUsed();
        }

        // Remove existing entry if present
        Remove(key);
            
        // Add new entry
        cache[key] = entry;
        expiryTimes[key] = entry.ExpiryTime;
        memorySizes[key] = entry.MemorySize;
        totalMemoryUsage += entry.MemorySize;

        // Check count limits
        if (cache.Count > maxCacheSize)
        {
            EvictLeastRecentlyUsed();
        }

        if (enableDebugLogs)
            Debug.Log($"CacheService: Cached item '{key}' (expires: {entry.ExpiryTime})");
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (cache.TryRemove(key, out ICacheEntry entry))
        {
            if (memorySizes.TryGetValue(key, out long memorySize))
            {
                totalMemoryUsage -= memorySize;
                memorySizes.Remove(key);
            }

            expiryTimes.Remove(key);

            // Cleanup texture memory safely
            if (destroyTextureOnEvict && entry is CacheEntry<Texture2D> textureEntry && textureEntry.Value != null)
            {
                Destroy(textureEntry.Value);
            }

            if (enableDebugLogs)
                Debug.Log($"CacheService: Removed item '{key}'");
        }
    }

    public bool Contains(string key)
    {
        return !string.IsNullOrEmpty(key) && cache.ContainsKey(key);
    }

    public void Clear()
    {
        // Cleanup textures before clearing
        foreach (var kvp in cache.ToList()) // ToList() to avoid modification during iteration
        {
            if (kvp.Value is CacheEntry<Texture2D> textureEntry && textureEntry.Value != null)
            {
                Destroy(textureEntry.Value);
            }
        }

        cache.Clear();
        expiryTimes.Clear();
        memorySizes.Clear();
        totalMemoryUsage = 0;

        if (enableDebugLogs)
            Debug.Log("CacheService: Cache cleared");
    }

    #endregion

    #region Specialized Cache Methods

    public void SetTexture(string key, Texture2D texture, TimeSpan expiry)
    {
        Set(key, texture, expiry);
    }

    public Texture2D GetTexture(string key)
    {
        return Get<Texture2D>(key);
    }

    public void SetShopItems(string key, List<ShopItem> items, TimeSpan expiry)
    {

        Set(key, items, expiry);

    }

    public List<ShopItem> GetShopItems(string key)
    {
        return Get<List<ShopItem>>(key);
    }

    #endregion

    #region Cache Management

    private void StartCleanupCoroutine()
    {
        StopCleanupCoroutine();
        cleanupCoroutine = StartCoroutine(CleanupExpiredItems());
    }

    private void StopCleanupCoroutine()
    {
        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
        }
    }

    private System.Collections.IEnumerator CleanupExpiredItems()
    {
        while (true)
        {
            yield return new WaitForSeconds(cleanupIntervalSeconds);

            var expiredKeys = new List<string>();
            var now = DateTime.Now;

            foreach (var kvp in expiryTimes.ToList()) // Safe iteration
            {
                if (now > kvp.Value)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                Remove(key);
            }

            if (enableDebugLogs && expiredKeys.Count > 0)
                Debug.Log($"CacheService: Cleaned up {expiredKeys.Count} expired items");

        }
    }

    // FIXED: Type-safe LRU eviction using ICacheEntry
    private void EvictLeastRecentlyUsed()
    {
        string lruKey = null;
        int minAccessCount = int.MaxValue;
        DateTime oldestTime = DateTime.MaxValue;

        foreach (var kvp in cache.ToList())
        {
            var entry = kvp.Value;
            if (entry.AccessCount < minAccessCount ||
                (entry.AccessCount == minAccessCount && entry.CreatedTime < oldestTime))
            {
                minAccessCount = entry.AccessCount;
                oldestTime = entry.CreatedTime;
                lruKey = kvp.Key;
            }
        }

        if (lruKey != null)
        {
            Remove(lruKey);

            if (enableDebugLogs)
                Debug.Log($"CacheService: Evicted LRU item '{lruKey}'");
        }
    }


    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            ForceCacheCleanup();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ForceCacheCleanup();
    }

    private void ForceCacheCleanup()
    {
        // Xóa 30% cache khi app pause/unfocus
        int itemsToRemove = Mathf.FloorToInt(cache.Count * 0.3f);
        var oldestItems = GetOldestItems(itemsToRemove);

        foreach (var key in oldestItems)
            Remove(key);

        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }

    private List<string> GetOldestItems(int count)
    {
        var sortedItems = cache.ToList()
            .OrderBy(kvp => kvp.Value.CreatedTime)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        return sortedItems;
    }

    public void LogCacheStats()
    {
        Debug.Log($"=== CacheService Statistics ===");
        Debug.Log($"Items: {Count}/{maxCacheSize}");
        Debug.Log($"Memory: {totalMemoryUsage / 1024f / 1024f:F2}MB / {maxMemoryUsage / 1024f / 1024f:F2}MB");
        Debug.Log($"Hit Ratio: {HitRatio:P}");
        Debug.Log($"Hits: {hitCount}, Misses: {missCount}");
    }
    #endregion
}
