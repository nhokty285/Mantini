/*using System;
using System.Collections.Generic;
using UnityEngine;

public interface ICacheService
{
    // Generic caching methods
    T Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan expiry);
    bool TryGet<T>(string key, out T value);
    void Remove(string key);
    void Clear();
    bool Contains(string key);

    // Specialized methods for common types
    void SetTexture(string key, Texture2D texture, TimeSpan expiry);
    Texture2D GetTexture(string key);
    void SetShopItems(string key, List<ShopItem> items, TimeSpan expiry);
    List<ShopItem> GetShopItems(string key);

    // Cache statistics
    int Count { get; }
    long TotalMemoryUsage { get; }
    void LogCacheStats();
}

[Serializable]
public class CacheEntry<T>
{
    public T Value;
    public DateTime ExpiryTime;
    public DateTime CreatedTime;
    public int AccessCount;
    public long MemorySize;

    public bool IsExpired => DateTime.Now > ExpiryTime;

    public CacheEntry(T value, TimeSpan expiry)
    {
        Value = value;
        ExpiryTime = DateTime.Now.Add(expiry);
        CreatedTime = DateTime.Now;
        AccessCount = 0;
        MemorySize = EstimateMemorySize(value);
    }

    private long EstimateMemorySize(T value)
    {
        if (value is Texture2D texture)
            return texture.width * texture.height * 4; // RGBA
        if (value is string str)
            return str.Length * 2; // Unicode
        if (value is List<ShopItem> list)
            return list.Count * 1024; // Estimate 1KB per item
        return 256; // Default estimate
    }
}
*/

using System;
using System.Collections.Generic;
using UnityEngine;

// Interface chung cho tất cả cache entries
public interface ICacheEntry
{
    DateTime ExpiryTime { get; }
    DateTime CreatedTime { get; }
    int AccessCount { get; set; }
    long MemorySize { get; }
    bool IsExpired { get; }
    void IncrementAccess();
}

public interface ICacheService
{
    // Generic caching methods
    T Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan expiry);
    bool TryGet<T>(string key, out T value);
    void Remove(string key);
    void Clear();
    bool Contains(string key);

    // Specialized methods for common types
    void SetTexture(string key, Texture2D texture, TimeSpan expiry);
    Texture2D GetTexture(string key);
    void SetShopItems(string key, List<ShopItem> items, TimeSpan expiry);
    List<ShopItem> GetShopItems(string key);

    // Cache statistics
    int Count { get; }
    long TotalMemoryUsage { get; }
    void LogCacheStats();
}

[Serializable]
public class CacheEntry<T> : ICacheEntry
{
    public T Value;
    public DateTime ExpiryTime { get; private set; }
    public DateTime CreatedTime { get; private set; }
    public int AccessCount { get; set; }
    public long MemorySize { get; private set; }

    public bool IsExpired => DateTime.Now > ExpiryTime;

    public CacheEntry(T value, TimeSpan expiry)
    {
        Value = value;
        ExpiryTime = DateTime.Now.Add(expiry);
        CreatedTime = DateTime.Now;
        AccessCount = 0;
        MemorySize = EstimateMemorySize(value);
    }

    public void IncrementAccess()
    {
        AccessCount++;
    }

    private long EstimateMemorySize(T value)
    {
        if (value is Texture2D texture)
            return texture.width * texture.height * 4; // RGBA
        if (value is string str)
            return str.Length * 2; // Unicode
        if (value is List<ShopItem> list)
            return list.Count * 1024; // Estimate 1KB per item
        return 256; // Default estimate
    }
}
