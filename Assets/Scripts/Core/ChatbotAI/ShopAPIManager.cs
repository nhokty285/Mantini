using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using static MainMenuViewModel;

/// <summary>
/// Fetch shop items per-NPC từ external API (storims). Có cache (CacheService),
/// retry-with-backup-URL, và per-NPC coroutine tracking để cancel khi NPC re-trigger.
///
/// ⚠️ NOTE: File này tự dùng UnityWebRequest thay vì APIClient.Instance vì:
///  - Endpoint là external (storims), không cần Bearer token Mantini
///  - Có timeout + retry logic riêng (backup URL)
/// Đây là exception chấp nhận được trong Mantini coding standards.
/// </summary>
public class ShopAPIManager : MonoBehaviour
{
    [Header("API Configurations")]
    [SerializeField] private List<NPCAPIConfig> npcApiConfigs = new List<NPCAPIConfig>();

    [Header("Cache Settings")]
    [SerializeField] private float cacheExpiryMinutes = 10f;
    [SerializeField] private bool enableCaching = true;

    public static ShopAPIManager Instance { get; private set; }

    // Cache key prefix — extract const tránh string concat scattered
    private const string CACHE_KEY_PREFIX = "shop_items_";

    // Per-NPC coroutine tracking — cho phép cancel khi cùng NPC được trigger lại
    private readonly Dictionary<string, Coroutine> _activeCoroutines = new Dictionary<string, Coroutine>();

    private void Awake()
    {
        // Refactor: bỏ `lock` thừa — Unity lifecycle chạy single-thread, lock không cần.
        // Standardize singleton theo Mantini convention.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        // Cleanup all active coroutines khi destroy
        foreach (var coroutine in _activeCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        _activeCoroutines.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════

    public void FetchShopItemsForNPC(string npcId, Action<List<ShopItem>> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            onError?.Invoke("Invalid NPC ID");
            return;
        }

        // Check cache trước — O(1) lookup qua CacheService
        if (enableCaching && CacheService.Instance != null)
        {
            string cacheKey = CACHE_KEY_PREFIX + npcId;
            if (CacheService.Instance.TryGet(cacheKey, out List<ShopItem> cachedItems))
            {
#if UNITY_EDITOR
                GameLog.Info($"[ShopAPIManager] Cache hit for NPC: {npcId}");
#endif
                onSuccess?.Invoke(cachedItems);
                return;
            }
        }

        // Tìm API config cho NPC này
        NPCAPIConfig config = GetConfigForNPC(npcId);
        if (config == null)
        {
            Debug.LogError($"[ShopAPIManager] No API configuration found for NPC: {npcId}");
            onError?.Invoke($"No configuration for NPC: {npcId}");
            return;
        }

        // Cancel existing request cho cùng NPC (TryGetValue = 1 lookup thay vì ContainsKey + indexer)
        if (_activeCoroutines.TryGetValue(npcId, out var oldCoroutine))
        {
            if (oldCoroutine != null) StopCoroutine(oldCoroutine);
            _activeCoroutines.Remove(npcId);
        }

        // Start new request
        _activeCoroutines[npcId] = StartCoroutine(FetchShopItemsCoroutine(config, onSuccess, onError));
    }

    public void ClearAPICache()
    {
        if (CacheService.Instance != null)
        {
            CacheService.Instance.Clear();
            GameLog.Info("[ShopAPIManager] API Cache cleared manually");
        }
    }

    public void LogCacheStatistics()
        => CacheService.Instance?.LogCacheStats();

    // ═══════════════════════════════════════════════════════════════════════
    // INTERNAL — Fetch / Backup coroutines
    // ═══════════════════════════════════════════════════════════════════════

    private NPCAPIConfig GetConfigForNPC(string npcId)
        => npcApiConfigs.Find(config => config.npcId == npcId);

    private IEnumerator FetchShopItemsCoroutine(NPCAPIConfig config, Action<List<ShopItem>> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(config.apiUrl))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 8; // 8 second timeout (sửa comment cũ sai)

            yield return request.SendWebRequest();

            // Remove from active coroutines (request đã xong)
            _activeCoroutines.Remove(config.npcId);

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
#if UNITY_EDITOR
                    GameLog.Info($"[ShopAPIManager] API Response for {config.npcName}: Data received successfully");
#endif

                    APIProductResponse apiResponse = JsonConvert.DeserializeObject<APIProductResponse>(jsonResponse);
                    List<ShopItem> shopItems = ConvertAPIItemsToShopItems(apiResponse.items, config);

                    // Cache results
                    if (enableCaching && CacheService.Instance != null)
                    {
                        string cacheKey = CACHE_KEY_PREFIX + config.npcId;
                        TimeSpan expiry = TimeSpan.FromMinutes(cacheExpiryMinutes);
                        CacheService.Instance.SetShopItems(cacheKey, shopItems, expiry);
#if UNITY_EDITOR
                        GameLog.Info($"[ShopAPIManager] Cached shop items for {config.npcName} (expires in {cacheExpiryMinutes}m)");
#endif
                    }

                    onSuccess?.Invoke(shopItems);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ShopAPIManager] JSON Parse Error for {config.npcName}: {e.Message}");
                    TryBackupOrFail(config, onSuccess, onError, $"Failed to parse data: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[ShopAPIManager] API Request Error for {config.npcName}: {request.error}");
                TryBackupOrFail(config, onSuccess, onError, $"Network error: {request.error}");
            }
        }
    }

    // Refactor: gộp 2 nhánh backup/error duplicate (parse-error + network-error đều có cùng logic)
    private void TryBackupOrFail(NPCAPIConfig config, Action<List<ShopItem>> onSuccess, Action<string> onError, string fallbackErrorMsg)
    {
        if (config.useBackupAPI && !string.IsNullOrEmpty(config.backupApiUrl))
        {
            _activeCoroutines[config.npcId] = StartCoroutine(TryBackupAPI(config, onSuccess, onError));
        }
        else
        {
            onError?.Invoke(fallbackErrorMsg);
        }
    }

    private IEnumerator TryBackupAPI(NPCAPIConfig config, Action<List<ShopItem>> onSuccess, Action<string> onError)
    {
#if UNITY_EDITOR
        GameLog.Info($"[ShopAPIManager] Trying backup API for {config.npcName}: {config.backupApiUrl}");
#endif

        using (UnityWebRequest request = UnityWebRequest.Get(config.backupApiUrl))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();
            _activeCoroutines.Remove(config.npcId);

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    APIProductResponse apiResponse = JsonConvert.DeserializeObject<APIProductResponse>(request.downloadHandler.text);
                    List<ShopItem> shopItems = ConvertAPIItemsToShopItems(apiResponse.items, config);

                    // Cache với thời gian ngắn hơn cho backup
                    if (enableCaching && CacheService.Instance != null)
                    {
                        string cacheKey = CACHE_KEY_PREFIX + config.npcId;
                        TimeSpan expiry = TimeSpan.FromMinutes(cacheExpiryMinutes / 2);
                        CacheService.Instance.SetShopItems(cacheKey, shopItems, expiry);
                    }

                    onSuccess?.Invoke(shopItems);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Backup API also failed: {e.Message}");
                }
            }
            else
            {
                onError?.Invoke($"Backup API request failed: {request.error}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONVERSION & FILTERING
    // ═══════════════════════════════════════════════════════════════════════

    private List<ShopItem> ConvertAPIItemsToShopItems(List<APIProductItem> apiItems, NPCAPIConfig config)
    {
        List<APIProductItem> filteredItems = FilterItemsForNPC(apiItems, config);
        var shopItems = new List<ShopItem>(filteredItems.Count); // pre-allocate

        foreach (var apiItem in filteredItems)
        {
            ShopItem shopItem = ScriptableObject.CreateInstance<ShopItem>();
            shopItem.itemName = SanitizeString(apiItem.title);
            shopItem.itemID = apiItem.id;
            shopItem.price = Mathf.RoundToInt(apiItem.price);
            shopItem.regularPrice = Mathf.RoundToInt(apiItem.regularPrice);
            shopItem.description = $"Brand: {SanitizeString(apiItem.brandName)}\nCategory: {config.shopCategory}\nReviews: {apiItem.totalReviews} ({apiItem.reviewStatFiveScale}★)";

            // Set image URL for later loading
            if (apiItem.images != null && apiItem.images.Count > 0)
                shopItem.imageUrl = apiItem.images[0].small;

            shopItem.icon = null; // Let ShopItemUI handle loading
            shopItem.type = config.primaryItemType;
            shopItem.SetAPIData(apiItem);
            shopItems.Add(shopItem);
        }

        return shopItems;
    }

    private static string SanitizeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "Unknown";
        return input.Trim().Replace("\n", " ").Replace("\r", "");
    }

    private List<APIProductItem> FilterItemsForNPC(List<APIProductItem> allItems, NPCAPIConfig config)
    {
        // Pre-allocate với capacity ước lượng
        var filteredItems = new List<APIProductItem>(Mathf.Min(allItems.Count, config.maxItems));

        // Cache count để tránh re-evaluation
        int requiredBrandCount = config.requiredBrands.Count;
        int requiredKeywordCount = config.requiredKeywords.Count;

        foreach (var item in allItems)
        {
            // Nếu không có filter brand/keyword → pass mặc định
            bool matchesBrand = requiredBrandCount == 0;
            bool matchesKeyword = requiredKeywordCount == 0;

            // Refactor: dùng OrdinalIgnoreCase IndexOf thay vì ToLower().Contains()
            // Tiết kiệm 2 string alloc mỗi item (1 cho ToLower brandName, 1 cho ToLower brand)
            if (requiredBrandCount > 0 && !string.IsNullOrEmpty(item.brandName))
            {
                for (int i = 0; i < requiredBrandCount; i++)
                {
                    if (item.brandName.IndexOf(config.requiredBrands[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchesBrand = true;
                        break;
                    }
                }
            }

            if (requiredKeywordCount > 0 && !string.IsNullOrEmpty(item.title))
            {
                for (int i = 0; i < requiredKeywordCount; i++)
                {
                    if (item.title.IndexOf(config.requiredKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchesKeyword = true;
                        break;
                    }
                }
            }

            if (matchesBrand && matchesKeyword)
                filteredItems.Add(item);
        }

        // Giới hạn số lượng và random nếu quá nhiều
        if (filteredItems.Count > config.maxItems)
            filteredItems = GetRandomItems(filteredItems, config.maxItems);

#if UNITY_EDITOR
        GameLog.Info($"[ShopAPIManager] Filtered {filteredItems.Count} items for {config.npcName} (Category: {config.shopCategory})");
#endif
        return filteredItems;
    }

    /// <summary>
    /// Refactor: Fisher-Yates partial shuffle, O(n) thay vì O(n²) của bản cũ
    /// (cũ dùng RemoveAt mỗi vòng = O(n) shift). Cũng pre-allocate result list.
    /// </summary>
    private static List<APIProductItem> GetRandomItems(List<APIProductItem> items, int count)
    {
        int total = items.Count;
        int take = Mathf.Min(count, total);

        // Shallow copy để không mutate input
        var temp = new List<APIProductItem>(items);
        var result = new List<APIProductItem>(take);

        // Partial Fisher-Yates: swap temp[i] với temp[random in [i, total)] rồi take temp[i]
        for (int i = 0; i < take; i++)
        {
            int j = UnityEngine.Random.Range(i, total);
            (temp[i], temp[j]) = (temp[j], temp[i]);
            result.Add(temp[i]);
        }

        return result;
    }
}