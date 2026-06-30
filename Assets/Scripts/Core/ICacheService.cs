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
        /*if (value is Texture2D texture)
            return texture.width * texture.height * 4; // RGBA*/
        if (value is Texture2D texture)
        {
            int width = texture.width;
            int height = texture.height;
            float bpp = 32f; // Mặc định: 32 bits per pixel (RGBA 32bit)

            // Phân loại định dạng nén thực tế dựa trên định dạng của Texture
            switch (texture.format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                    bpp = 32f; // 4 Bytes / pixel
                    break;
                case TextureFormat.RGB24:
                    bpp = 24f; // 3 Bytes / pixel
                    break;
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ASTC_4x4:
                    bpp = 8f;  // 1 Byte / pixel
                    break;
                case TextureFormat.ASTC_6x6:
                    bpp = 3.56f; // ~0.44 Byte / pixel
                    break;
                case TextureFormat.ASTC_8x8:
                    bpp = 2f;  // 0.25 Byte / pixel
                    break;
                    // Bạn có thể thêm các định dạng khác đang dùng trong project vào đây
            }

            // Tính dung lượng base (đơn vị: Byte)
            long baseSize = Mathf.CeilToInt((width * height * bpp) / 8f);

            // 💡 Mẹo Mobile: Nếu ảnh có bật Mipmaps, Unity sẽ tạo thêm các bản thu nhỏ ngầm, tốn thêm ~33% bộ nhớ
            if (texture.mipmapCount > 1)
            {
                baseSize = Mathf.CeilToInt(baseSize * 1.333f);
            }

            return baseSize;
        }
        if (value is string str)
            return str.Length * 2; // Unicode
        if (value is List<ShopItem> list)
            return list.Count * 1024; // Estimate 1KB per item
        return 256; // Default estimate
    }
}
