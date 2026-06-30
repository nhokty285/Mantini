using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PlayerApiService : MonoBehaviour
{
    [Header("API Configuration")]
    [SerializeField] private string playerMeUrl = "https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me";
    [SerializeField] private string playerInventoryUrl = "https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me/inventory";

    // Refactor: cache JsonSerializerSettings static — trước đây alloc mới mỗi lần SaveInventoryItems
    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    // PlayerPrefs keys
    private const string KEY_NAME     = "Profile_Name";
    private const string KEY_USERNAME = "Profile_Username";
    private const string KEY_MAIL     = "Profile_Mail";
    private const string KEY_PHONE    = "Profile_Phone";

    [Serializable]
    public class PlayerData
    {
        public string player_id;
        public string name;
        public string username_email;
        public string mail;
        public string phone;
        public string avatar_url;
        public string[] companion_ids; // Mảng companions đã chọn
        public string avatar_id;
    }

    [Serializable]
    private class PlayerUpdatePayload
    {
        public string name;
        public string username_email;
        public string mail;
        public string phone;
        public string avatar_url;
        public string[] companion_ids;
        public string avatar_id;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER PROFILE — SYNC / UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    // Lấy current từ server trước rồi PUT lại — đảm bảo merge đúng
    public void SyncSelectionToServer()
    {
        APIClient.Instance.GetFull(playerMeUrl, json =>
        {
            var current = JsonUtility.FromJson<PlayerData>(json);

            var characterData = PlayerDataManager.Instance.GetSelectedCharacterData();
            var companionData = PlayerDataManager.Instance.GetSelectedCompanionData();
            if (characterData == null || companionData == null)
            {
                Debug.LogError("[PlayerApiService] Character or companion data not found!");
                return;
            }

            var payload = new PlayerUpdatePayload
            {
                name = current.name,
                username_email = current.username_email,
                mail = current.mail,
                phone = current.phone,
                avatar_url = current.avatar_url,
                companion_ids = new[] { companionData.characterName },
                avatar_id = characterData.characterName // Lưu nhân vật chính để restore khi đăng nhập lại
            };

            APIClient.Instance.PutJsonFull(playerMeUrl, JsonUtility.ToJson(payload),
                _ => GameLog.Info("[PlayerApiService] Successfully synced selection"),
                err => Debug.LogError("[PlayerApiService] Failed to sync: " + err)
            );
        },
        error => Debug.LogError("[PlayerApiService] Failed to get player before sync: " + error));
    }

    public void GetPlayerDataFromServer()
    {
        APIClient.Instance.GetFull(playerMeUrl,
            onSuccess: (json) =>
            {
                var data = JsonUtility.FromJson<PlayerData>(json);
#if UNITY_EDITOR
                // PII (mail, companion list) — chỉ log trong editor
                GameLog.Info($"[PlayerApiService] Current server data: name={data.name}, companion_ids=[{string.Join(", ", data.companion_ids)}]");
#endif
            },
            onError: (error) =>
            {
                Debug.LogError($"[PlayerApiService] Failed to get player data: {error}");
            });
    }

    public void UpdatePlayerInfo(string newName, string newMail, string newUserName, string newPhone, string newAvatarUrl,
        Action onSuccess, Action<string> onError)
    {
        // Tạo partial JSON chỉ field non-empty
        string partialJson = BuildPartialProfileJson(newName, newMail, newPhone, newUserName, newAvatarUrl);
        if (partialJson == "{}")
        {
            GameLog.Info("[PlayerApiService] No profile fields, skip");
            onSuccess?.Invoke();
            return;
        }

        // GET current trước
        APIClient.Instance.GetFull(playerMeUrl, currentJson =>
        {
            var current = JsonUtility.FromJson<PlayerData>(currentJson);
            // Merge: dùng new values nếu non-empty, else giữ current
            var payload = new PlayerUpdatePayload
            {
                name           = !string.IsNullOrEmpty(newName)      ? newName      : current.name,
                username_email = !string.IsNullOrEmpty(newUserName)  ? newUserName  : current.username_email,
                mail           = !string.IsNullOrEmpty(newMail)      ? newMail      : current.mail,
                phone          = !string.IsNullOrEmpty(newPhone)     ? newPhone     : current.phone,
                avatar_url     = !string.IsNullOrEmpty(newAvatarUrl) ? newAvatarUrl : current.avatar_url,
            };
            string fullJson = JsonUtility.ToJson(payload);
            APIClient.Instance.PutJsonFull(playerMeUrl, fullJson,
                _ =>
                {
                    GameLog.Info("[PlayerApiService] Profile updated");
                    onSuccess?.Invoke();
                },
                err =>
                {
                    Debug.LogError("[PlayerApiService] Update failed: " + err);
                    onError?.Invoke(err);
                });
        },
        err =>
        {
            // Nếu chưa tồn tại (404?), dùng PUT create với partial JSON
            if (err.Contains("404") || err.Contains("Not Found"))
            {
                APIClient.Instance.PutJsonFull(playerMeUrl, partialJson,
                    _ =>
                    {
                        GameLog.Info("[PlayerApiService] Profile created");
                        onSuccess?.Invoke();
                    },
                    onError);
            }
            else
            {
                Debug.LogError("[PlayerApiService] GET failed: " + err);
                onError?.Invoke(err);
            }
        });
    }

    public void LoadProfileFromServer(Action<PlayerData> onSuccess, Action<string> onError)
    {
        APIClient.Instance.GetFull(playerMeUrl, json =>
        {
            var data = JsonUtility.FromJson<PlayerData>(json);
#if UNITY_EDITOR
            GameLog.Info($"[PlayerApiService] LoadProfile: name={data.name}, mail={data.mail}, avatar_url={data.avatar_url}");
#endif
            onSuccess?.Invoke(data);
        },
        error =>
        {
            Debug.LogError($"[PlayerApiService] LoadProfile failed: {error}");
            onError?.Invoke(error);
        });
    }

    // Build JSON chỉ chứa các field non-empty.
    // ⚠️ NOTE: Escape() chỉ xử lý \ và " — chưa xử lý \n, \r, \t, \b, \f, \u00xx.
    // Nếu user input có newline, JSON sẽ invalid. Backend hiện chấp nhận → giữ behavior.
    private string BuildPartialProfileJson(string name, string email, string phone, string userName, string avatarUrl)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        bool wrote = false;
        void Add(string key, string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            if (wrote) sb.Append(',');
            sb.Append('\"').Append(key).Append("\":");
            sb.Append('\"').Append(Escape(val.Trim())).Append('\"');
            wrote = true;
        }

        Add("name", name);
        Add("username_email", userName);
        Add("email", email);
        Add("phone", phone);
        Add("avatar_url", avatarUrl);

        sb.Append('}');
        return sb.ToString();
    }

    private string Escape(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYERPREFS PROFILE CACHE (local)
    // ═══════════════════════════════════════════════════════════════════════

    public void SaveProfile(string name, string username, string mail, string phone)
    {
        PlayerPrefs.SetString(KEY_NAME, name);
        PlayerPrefs.SetString(KEY_USERNAME, username);
        PlayerPrefs.SetString(KEY_MAIL, mail);
        PlayerPrefs.SetString(KEY_PHONE, phone);
        PlayerPrefs.Save();
    }

    public (string name, string username, string mail, string phone) GetProfile()
    {
        return (
            PlayerPrefs.GetString(KEY_NAME, ""),
            PlayerPrefs.GetString(KEY_USERNAME, ""),
            PlayerPrefs.GetString(KEY_MAIL, ""),
            PlayerPrefs.GetString(KEY_PHONE, "")
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INVENTORY
    // ═══════════════════════════════════════════════════════════════════════

    [Serializable]
    public class GameItemData
    {
        public string item_id;
        public string name;
        public string description;
        public string image_url;
        public string type;
        public string external_id;
    }

    [Serializable]
    public class InventoryItem
    {
        public GameItemData game_item;
        public int quantity;
    }

    [Serializable]
    public class InventoryPayload
    {
        public InventoryItem[] inventory;
    }

    public void SaveInventoryItems(List<CartItem> items, Action onSuccess, Action<string> onError)
    {
        if (items == null || items.Count == 0)
        {
            onError?.Invoke("No items to save");
            return;
        }

        // Convert CartItem sang InventoryItem (pre-allocate array, không LINQ)
        var inventoryItems = new InventoryItem[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var c = items[i];
            inventoryItems[i] = new InventoryItem
            {
                game_item = new GameItemData
                {
                    name = c.productName,
                    description = $"{c.brandName} - {c.selectedSize}",
                    image_url = c.imageUrl,
                    type = "product",
                    external_id = c.productId
                },
                quantity = c.quantity
            };
        }

        var payload = new InventoryPayload { inventory = inventoryItems };
        string json = JsonConvert.SerializeObject(payload, _jsonSettings); // dùng cached settings

#if UNITY_EDITOR
        GameLog.Info($"[PlayerApiService] Inventory POST body: {json}");
#endif

        APIClient.Instance.PostJsonFull(
            playerInventoryUrl,
            json,
            responseJson =>
            {
#if UNITY_EDITOR
                GameLog.Info($"[PlayerApiService] Inventory POST response: {responseJson}");

                try
                {
                    var response = JsonConvert.DeserializeObject<InventoryPayload>(responseJson);
                    if (response?.inventory != null)
                    {
                        foreach (var item in response.inventory)
                        {
                            GameLog.Info($"[PlayerApiService] Created item_id: {item.game_item?.item_id}, " +
                                      $"external_id: {item.game_item?.external_id}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    GameLog.Warn($"[PlayerApiService] Could not parse inventory response: {ex.Message}");
                }
#endif

                onSuccess?.Invoke();
            },
            onError
        );
    }

    public void LoadInventoryFromServer(Action<List<InventoryItem>> onSuccess, Action<string> onError)
    {
        APIClient.Instance.GetFull(playerInventoryUrl,
            json =>
            {
                var items = JsonConvert.DeserializeObject<List<InventoryItem>>(json);
                onSuccess?.Invoke(items ?? new List<InventoryItem>());
            },
            onError);
    }
}