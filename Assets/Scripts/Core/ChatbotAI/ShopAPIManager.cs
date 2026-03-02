/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using static MainMenuViewModel;

public class ShopAPIManager : MonoBehaviour
{
    private const string API_URL = "https://api.staging.storims.com/api/v1/TenantProduct/45a26bfc-f2b2-4ca2-ab49-9ee8e9adcfec";
    [Header("API Configurations")]
    [SerializeField] private List<NPCAPIConfig> npcApiConfigs = new List<NPCAPIConfig>();

    public static ShopAPIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public void FetchShopItemsForNPC(string npcId, System.Action<List<ShopItem>> onSuccess, System.Action<string> onError)
    {
        // Tìm API config cho NPC này
        NPCAPIConfig config = GetConfigForNPC(npcId);
        if (config == null)
        {
            Debug.LogError($"No API configuration found for NPC: {npcId}");
            onError?.Invoke($"No configuration for NPC: {npcId}");
            return;
        }

        StartCoroutine(FetchShopItemsCoroutine(config, onSuccess, onError));
    }

    private NPCAPIConfig GetConfigForNPC(string npcId)
    {
        return npcApiConfigs.Find(config => config.npcId == npcId);
    }

    private IEnumerator FetchShopItemsCoroutine(NPCAPIConfig config, System.Action<List<ShopItem>> onSuccess, System.Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(config.apiUrl))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"API Response for {config.npcName}: {jsonResponse}");

                    APIProductResponse apiResponse = JsonConvert.DeserializeObject<APIProductResponse>(jsonResponse);
                    List<ShopItem> shopItems = ConvertAPIItemsToShopItems(apiResponse.items, config);

                    onSuccess?.Invoke(shopItems);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"JSON Parse Error for {config.npcName}: {e.Message}");

                    // Thử backup API nếu có
                    if (config.useBackupAPI && !string.IsNullOrEmpty(config.backupApiUrl))
                    {
                        StartCoroutine(TryBackupAPI(config, onSuccess, onError));
                    }
                    else
                    {
                        onError?.Invoke($"Failed to parse data: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.LogError($"API Request Error for {config.npcName}: {request.error}");

                // Thử backup API nếu có
                if (config.useBackupAPI && !string.IsNullOrEmpty(config.backupApiUrl))
                {
                    StartCoroutine(TryBackupAPI(config, onSuccess, onError));
                }
                else
                {
                    onError?.Invoke($"Network error: {request.error}");
                }
            }
        }
    }

    private IEnumerator TryBackupAPI(NPCAPIConfig config, System.Action<List<ShopItem>> onSuccess, System.Action<string> onError)
    {
        Debug.Log($"Trying backup API for {config.npcName}: {config.backupApiUrl}");

        using (UnityWebRequest request = UnityWebRequest.Get(config.backupApiUrl))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    APIProductResponse apiResponse = JsonConvert.DeserializeObject<APIProductResponse>(request.downloadHandler.text);
                    List<ShopItem> shopItems = ConvertAPIItemsToShopItems(apiResponse.items, config);
                    onSuccess?.Invoke(shopItems);
                }
                catch (System.Exception e)
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

    private List<ShopItem> ConvertAPIItemsToShopItems(List<APIProductItem   > apiItems, NPCAPIConfig config)
    {
        List<ShopItem> shopItems = new List<ShopItem>();
        List<APIProductItem> filteredItems = FilterItemsForNPC(apiItems, config);

        foreach (var apiItem in filteredItems)
        {
            ShopItem shopItem = ScriptableObject.CreateInstance<ShopItem>();
            shopItem.itemName = apiItem.title;
            shopItem.itemID = apiItem.id;
            shopItem.price = Mathf.RoundToInt(apiItem.price);
            shopItem.description = $"Brand: {apiItem.brandName}\nCategory: {config.shopCategory}\nReviews: {apiItem.totalReviews} ({apiItem.reviewStatFiveScale}★)";

            // Set image URL cho việc load sau
            if (apiItem.images != null && apiItem.images.Count > 0)
            {
                shopItem.imageUrl = apiItem.images[0].small;
            }

            shopItem.icon = null; // Để ShopItemUI tự load
            shopItem.type = config.primaryItemType; // Dùng type từ config

            // THÊM: Lưu reference tới API data gốc
            shopItem.SetAPIData(apiItem);

            shopItems.Add(shopItem);
        }

        return shopItems;
    }

    private List<APIProductItem> FilterItemsForNPC(List<APIProductItem> allItems, NPCAPIConfig config)
    {
        List<APIProductItem> filteredItems = new List<APIProductItem>();

        foreach (var item in allItems)
        {
            bool matchesBrand = config.requiredBrands.Count == 0; // Nếu không có filter brand thì pass
            bool matchesKeyword = config.requiredKeywords.Count == 0; // Nếu không có filter keyword thì pass

            // Check brand filter
            if (config.requiredBrands.Count > 0 && !string.IsNullOrEmpty(item.brandName))
            {
                foreach (string brand in config.requiredBrands)
                {
                    if (item.brandName.ToLower().Contains(brand.ToLower()))
                    {
                        matchesBrand = true;
                        break;
                    }
                }
            }

            // Check keyword filter
            if (config.requiredKeywords.Count > 0 && !string.IsNullOrEmpty(item.title))
            {
                foreach (string keyword in config.requiredKeywords)
                {
                    if (item.title.ToLower().Contains(keyword.ToLower()))
                    {
                        matchesKeyword = true;
                        break;
                    }
                }
            }

            if (matchesBrand && matchesKeyword)
            {
                filteredItems.Add(item);
            }
        }

        // Giới hạn số lượng và random nếu quá nhiều
        if (filteredItems.Count > config.maxItems)
        {
            filteredItems = GetRandomItems(filteredItems, config.maxItems);
        }

        Debug.Log($"Filtered {filteredItems.Count} items for {config.npcName} (Category: {config.shopCategory})");
        return filteredItems;
    }

    private List<APIProductItem> GetRandomItems(List<APIProductItem> items, int count)
    {
        List<APIProductItem> randomItems = new List<APIProductItem>();
        List<APIProductItem> tempList    = new List<APIProductItem>(items);

        for (int i = 0; i < count && tempList.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, tempList.Count);
            randomItems.Add(tempList[randomIndex]);
            tempList.RemoveAt(randomIndex);
        }

        return randomItems;
    }
}*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using static MainMenuViewModel;
using System;
using System.Threading.Tasks;

public class ShopAPIManager : MonoBehaviour
{

    [Header("API Configurations")]
    [SerializeField] private List<NPCAPIConfig> npcApiConfigs = new List<NPCAPIConfig>();

    [Header("Cache Settings")]
    [SerializeField] private float cacheExpiryMinutes = 10f;
    [SerializeField] private bool enableCaching = true;

    public static ShopAPIManager Instance { get; private set; }

    // Thread-safe singleton with lock
    private static readonly object lockObject = new object();

    // Coroutine tracking for cleanup
    private Dictionary<string, Coroutine> activeCoroutines = new Dictionary<string, Coroutine>();

    private void Awake()
    {
        lock (lockObject)
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        // Cleanup all active coroutines
        foreach (var coroutine in activeCoroutines.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }
        activeCoroutines.Clear();
    }

    public void FetchShopItemsForNPC(string npcId, System.Action<List<ShopItem>> onSuccess, System.Action<string> onError)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            onError?.Invoke("Invalid NPC ID");
            return;
        }

        // Check cache first
        if (enableCaching && CacheService.Instance != null)
        {
            string cacheKey = $"shop_items_{npcId}";
            if (CacheService.Instance.TryGet(cacheKey, out List<ShopItem> cachedItems))
            {
                Debug.Log($"Using cached shop items for NPC: {npcId}");
                onSuccess?.Invoke(cachedItems);
                return;
            }
        }

        // Find API config for this NPC
        NPCAPIConfig config = GetConfigForNPC(npcId);
        if (config == null)
        {
            Debug.LogError($"No API configuration found for NPC: {npcId}");
            onError?.Invoke($"No configuration for NPC: {npcId}");
            return;
        }

        // Cancel existing request for same NPC
        if (activeCoroutines.ContainsKey(npcId))
        {
            StopCoroutine(activeCoroutines[npcId]);
            activeCoroutines.Remove(npcId);
        }

        // Start new request
        var coroutine = StartCoroutine(FetchShopItemsCoroutine(config, onSuccess, onError));
        activeCoroutines[npcId] = coroutine;
    }

    private NPCAPIConfig GetConfigForNPC(string npcId)
    {
        return npcApiConfigs.Find(config => config.npcId == npcId);
    }

    private IEnumerator FetchShopItemsCoroutine(NPCAPIConfig config, System.Action<List<ShopItem>> onSuccess, System.Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(config.apiUrl))
        {
           request.SetRequestHeader("Content-Type", "application/json");
           
            request.timeout = 8; // 30 second timeout
            yield return request.SendWebRequest();

            // Remove from active coroutines
            activeCoroutines.Remove(config.npcId);

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"API Response for {config.npcName}: Data received successfully");

                    APIProductResponse apiResponse = JsonConvert.DeserializeObject<APIProductResponse>(jsonResponse);
                    List<ShopItem> shopItems = ConvertAPIItemsToShopItems(apiResponse.items, config);

                    // Cache the results
                    if (enableCaching && CacheService.Instance != null)
                    {
                        string cacheKey = $"shop_items_{config.npcId}";
                        TimeSpan expiry = TimeSpan.FromMinutes(cacheExpiryMinutes);
                        CacheService.Instance.SetShopItems(cacheKey, shopItems, expiry);
                        Debug.Log($"Cached shop items for {config.npcName} (expires in {cacheExpiryMinutes} minutes)");
                    }

                    onSuccess?.Invoke(shopItems);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"JSON Parse Error for {config.npcName}: {e.Message}");

                    // Try backup API if available
                    if (config.useBackupAPI && !string.IsNullOrEmpty(config.backupApiUrl))
                    {
                        var backupCoroutine = StartCoroutine(TryBackupAPI(config, onSuccess, onError));
                        activeCoroutines[config.npcId] = backupCoroutine;
                    }
                    else
                    {
                        onError?.Invoke($"Failed to parse data: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.LogError($"API Request Error for {config.npcName}: {request.error}");

                // Try backup API if available
                if (config.useBackupAPI && !string.IsNullOrEmpty(config.backupApiUrl))
                {
                    var backupCoroutine = StartCoroutine(TryBackupAPI(config, onSuccess, onError));
                    activeCoroutines[config.npcId] = backupCoroutine;
                }
                else
                {
                    onError?.Invoke($"Network error: {request.error}");
                }
            }
        }
    }
   


    private IEnumerator TryBackupAPI(NPCAPIConfig config, System.Action<List<ShopItem>> onSuccess, System.Action<string> onError)
    {
        Debug.Log($"Trying backup API for {config.npcName}: {config.backupApiUrl}");

        using (UnityWebRequest request = UnityWebRequest.Get(config.backupApiUrl))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            // Remove from active coroutines
            activeCoroutines.Remove(config.npcId);

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    APIProductResponse apiResponse = JsonConvert.DeserializeObject<APIProductResponse>(request.downloadHandler.text);
                    List<ShopItem> shopItems = ConvertAPIItemsToShopItems(apiResponse.items, config);

                    // Cache backup results
                    if (enableCaching && CacheService.Instance != null)
                    {
                        string cacheKey = $"shop_items_{config.npcId}";
                        TimeSpan expiry = TimeSpan.FromMinutes(cacheExpiryMinutes / 2); // Shorter cache for backup
                        CacheService.Instance.SetShopItems(cacheKey, shopItems, expiry);
                    }

                    onSuccess?.Invoke(shopItems);
                }
                catch (System.Exception e)
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

    private List<ShopItem> ConvertAPIItemsToShopItems(List<APIProductItem> apiItems, NPCAPIConfig config)
    {
        List<ShopItem> shopItems = new List<ShopItem>();
        List<APIProductItem> filteredItems = FilterItemsForNPC(apiItems, config);

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
            {
                shopItem.imageUrl = apiItem.images[0].small;
            }

            shopItem.icon = null; // Let ShopItemUI handle loading
            shopItem.type = config.primaryItemType;
            shopItem.SetAPIData(apiItem);
            shopItems.Add(shopItem);
        }

        return shopItems;
    }

    private string SanitizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "Unknown";

        // Basic sanitization for display
        return input.Trim().Replace("\n", " ").Replace("\r", "");
    }

    private List<APIProductItem> FilterItemsForNPC(List<APIProductItem> allItems, NPCAPIConfig config)
    {
        List<APIProductItem> filteredItems = new List<APIProductItem>();

        foreach (var item in allItems)
        {
            bool matchesBrand = config.requiredBrands.Count == 0;
            bool matchesKeyword = config.requiredKeywords.Count == 0;

            // Check brand filter
            if (config.requiredBrands.Count > 0 && !string.IsNullOrEmpty(item.brandName))
            {
                foreach (string brand in config.requiredBrands)
                {
                    if (item.brandName.ToLower().Contains(brand.ToLower()))
                    {
                        matchesBrand = true;
                        break;
                    }
                }
            }

            // Check keyword filter
            if (config.requiredKeywords.Count > 0 && !string.IsNullOrEmpty(item.title))
            {
                foreach (string keyword in config.requiredKeywords)
                {
                    if (item.title.ToLower().Contains(keyword.ToLower()))
                    {
                        matchesKeyword = true;
                        break;
                    }
                }
            }

            if (matchesBrand && matchesKeyword)
            {
                filteredItems.Add(item);
            }
        }

        // Limit quantity and randomize if too many
        if (filteredItems.Count > config.maxItems)
        {
            filteredItems = GetRandomItems(filteredItems, config.maxItems);
        }

        Debug.Log($"Filtered {filteredItems.Count} items for {config.npcName} (Category: {config.shopCategory})");
        return filteredItems;
    }

    private List<APIProductItem> GetRandomItems(List<APIProductItem> items, int count)
    {
        List<APIProductItem> randomItems = new List<APIProductItem>();
        List<APIProductItem> tempList = new List<APIProductItem>(items);

        for (int i = 0; i < count && tempList.Count > 0; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, tempList.Count);
            randomItems.Add(tempList[randomIndex]);
            tempList.RemoveAt(randomIndex);
        }

        return randomItems;
    }

    // Public method to clear cache manually
    public void ClearAPICache()
    {
        if (CacheService.Instance != null)
        {
            CacheService.Instance.Clear();
            Debug.Log("API Cache cleared manually");
        }
    }

    // Get cache statistics
    public void LogCacheStatistics()
    {
        if (CacheService.Instance != null)
        {
            CacheService.Instance.LogCacheStats();
        }
    }

   /* public async Task<bool> SyncUserData(UserProfile user)
    {
        var requestData = new
        {
            playerId = user.playerId,
            email = user.email,
            displayName = user.displayName,
            authProvider = "unity_player_accounts"
        };

        string jsonData = JsonUtility.ToJson(requestData);

        try
        {
            var response = await SendPostRequest("/auth/unity-sync", jsonData);
            return response.success;
        }
        catch (Exception e)
        {
            Debug.LogError($"Backend sync failed: {e.Message}");
            return false;
        }
    }

    public async Task<APIResponse> SendPostRequest(string endpoint, string jsonData)
    {
        string fullUrl = "https://accounts.google.com/o/oauth2/v2/auth" + endpoint;

        using (UnityWebRequest request = new UnityWebRequest(fullUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                return new APIResponse { success = true, data = responseText };
            }
            else
            {
                Debug.LogError($"API Request failed: {request.error}");
                return new APIResponse { success = false, error = request.error };
            }
        }
    }

    [System.Serializable]
    public class APIResponse
    {
        public bool success;
        public string data;
        public string error;
    }*/

}
