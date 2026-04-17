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
    [Serializable]
    public class PlayerData
    {
        public string player_id;
        public string name;
        public string username_email;
        public string mail;
        public string phone;
        public string avatar_url;
        public string[] companion_ids; // Mảng chứa cả character + companions đã chọn
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
        public string[] companion_ids; // Chỉ gửi companion_ids, các field khác giữ nguyên
        public string avatar_id;
    }


    /*    public void SyncSelectionToServer()
        {
            // Lấy index character + companion từ PlayerDataManager
            var characterData = PlayerDataManager.Instance.GetSelectedCharacterData();
            var companionData = PlayerDataManager.Instance.GetSelectedCompanionData();

            if (characterData == null || companionData == null)
            {
                Debug.LogError("[PlayerSelectionSync] Character or companion data not found!");
                return;
            }

            // Build mảng companion_ids: character là phần tử đầu, sau đó là companion
            // (Giả sử bạn có ID duy nhất cho từng CharacterData; nếu không, dùng characterName)
            List<string> companionIds = new List<string>
            {
                characterData.characterName,  // ID character (hoặc dùng characterData.id nếu có)
                companionData.characterName   // ID companion
            };

            // Build payload chỉ gồm companion_ids để PUT
            PlayerUpdatePayload payload = new PlayerUpdatePayload
            {
                companion_ids = companionIds.ToArray(),
            };

            string json = JsonUtility.ToJson(payload);

            // PUT lên server
            APIClient.Instance.PostJsonFull(playerMeUrl, json,
                onSuccess: (response) =>
                {
                    Debug.Log($"[PlayerSelectionSync] Successfully synced selection to server:\n{json}");
                },
                onError: (error) =>
                {
                    Debug.LogError($"[PlayerSelectionSync] Failed to sync: {error}");
                }
            );
        }*/

    // Pseudo: lấy current từ server trước rồi PUT lại
    public void SyncSelectionToServer()
    {
        APIClient.Instance.GetFull(playerMeUrl, json =>
        {
            var current = JsonUtility.FromJson<PlayerData>(json);

            var characterData = PlayerDataManager.Instance.GetSelectedCharacterData();
            var companionData = PlayerDataManager.Instance.GetSelectedCompanionData();
            if (characterData == null || companionData == null)
            {
                Debug.LogError("[PlayerSelectionSync] Character or companion data not found!");
                return;
            }

         /*   var companionIds = new List<string>
        {          
            companionData.characterName
        };
            string characterId = characterData.characterName;*/

            var payload = new PlayerUpdatePayload
            {
                name = current.name,
                username_email = current.username_email,
                mail = current.mail,
                phone = current.phone,
                avatar_url = current.avatar_url,
                companion_ids = new[] { companionData.characterName },
                avatar_id = current.avatar_id
            };

            APIClient.Instance.PutJsonFull(playerMeUrl, JsonUtility.ToJson(payload),
                _ => Debug.Log("[PlayerSelectionSync] Successfully synced selection"),
                err => Debug.LogError("[PlayerSelectionSync] Failed to sync: " + err)
            );
        },
        error => Debug.LogError("[PlayerSelectionSync] Failed to get player before sync: " + error));
    }

    public void GetPlayerDataFromServer()
    {
        APIClient.Instance.GetFull(playerMeUrl,
            onSuccess: (json) =>
            {
                var data = JsonUtility.FromJson<PlayerData>(json);
                Debug.Log($"[PlayerSelectionSync] Current server data: name={data.name}, companion_ids=[{string.Join(", ", data.companion_ids)}]");
            },
            onError: (error) =>
            {
                Debug.LogError($"[PlayerSelectionSync] Failed to get player data: {error}");
            });
    }
    /* public void UpdatePlayerInfo(
         string newName,
         string newMail,
         string newUserName,
         string newPhone,
         //string newAvatarUrl,
         Action onSuccess,
         Action<string> onError)
     {
         // Chỉ gửi những field có giá trị
         string json = BuildPartialProfileJson(newName, newMail, newPhone, newUserName*//*, newAvatarUrl*//*);

         if (json == "{}")
         {
             Debug.Log("[PlayerSelectionSync] No profile fields provided, skip PUT");
             onSuccess?.Invoke();
             return;
         }

         APIClient.Instance.PutJsonFull(playerMeUrl, json,
             onSuccess: (res) =>
             {
                 Debug.Log("[PlayerSelectionSync] Profile updated");
                 onSuccess?.Invoke();
             },
             onError: (err) =>
             {
                 Debug.LogError("[PlayerSelectionSync] Update profile failed: " + err);
                 onError?.Invoke(err);
             });
     }*/

    public void UpdatePlayerInfo(string newName, string newMail, string newUserName, string newPhone, string newAvatarUrl,
    Action onSuccess, Action<string> onError)
    {
        // Tạo partial JSON chỉ field non-empty
        string partialJson = BuildPartialProfileJson(newName, newMail, newPhone, newUserName, newAvatarUrl);
        if (partialJson == "{}")
        {
            Debug.Log("[PlayerSelectionSync] No profile fields, skip");
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
                name = !string.IsNullOrEmpty(newName) ? newName : current.name,
                username_email = !string.IsNullOrEmpty(newUserName) ? newUserName : current.username_email,
                mail = !string.IsNullOrEmpty(newMail) ? newMail : current.mail,
                phone = !string.IsNullOrEmpty(newPhone) ? newPhone : current.phone,
                avatar_url = !string.IsNullOrEmpty(newAvatarUrl) ? newAvatarUrl : current.avatar_url,
                // companion_ids giữ nguyên nếu cần

               
            };
            string fullJson = JsonUtility.ToJson(payload);
            APIClient.Instance.PutJsonFull(playerMeUrl, fullJson,
                _ => {
                    Debug.Log("[PlayerSelectionSync] Profile updated");
                    onSuccess?.Invoke();
                },
                err => {
                    Debug.LogError("[PlayerSelectionSync] Update failed: " + err);
                    onError?.Invoke(err);
                });
        },
        err => {
            // Nếu chưa tồn tại (404?), dùng POST create
            if (err.Contains("404") || err.Contains("Not Found"))
            {
                APIClient.Instance.PutJsonFull(playerMeUrl, partialJson,
                    _ => {
                        Debug.Log("[PlayerSelectionSync] Profile created");
                        onSuccess?.Invoke();
                    },
                    onError);
            }
            else
            {
                Debug.LogError("[PlayerSelectionSync] GET failed: " + err);
                onError?.Invoke(err);
            }
        });
    }


    // Build JSON chỉ chứa các field non-empty
    private string BuildPartialProfileJson(string name, string email, string phone, string userName, string avatarUrl)//, string avatarUrl)
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

    // Thêm fields
    private const string KEY_NAME = "Profile_Name";
    private const string KEY_USERNAME = "Profile_Username";
    private const string KEY_MAIL = "Profile_Mail";
    private const string KEY_PHONE = "Profile_Phone";

    // Get/Set
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


    private string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public void LoadProfileFromServer(Action<PlayerData> onSuccess, Action<string> onError)
    {
        APIClient.Instance.GetFull(playerMeUrl, json =>
        {
            var data = JsonUtility.FromJson<PlayerData>(json);
            Debug.Log($"[LoadProfile] name={data.name}, mail={data.mail}, avatar_url={data.avatar_url}");
            onSuccess?.Invoke(data);
        },
        error =>
        {
            Debug.LogError($"[LoadProfile] Failed: {error}");
            onError?.Invoke(error);
        });
    }

    [System.Serializable]
    public class GameItemData
    {
       public string item_id;
        public string name;
        public string description;
        public string image_url;
        public string type;
        public string external_id;
    }

    [System.Serializable]
    public class InventoryItem
    {
        public GameItemData game_item;
        public int quantity;
    }

    [System.Serializable]
    public class InventoryPayload
    {
        public InventoryItem[] inventory; // Array of objects thay vì array of strings
    }

    public void SaveInventoryItems(List<CartItem> items, Action onSuccess, Action<string> onError)
    {
        if (items == null || items.Count == 0)
        {
            onError?.Invoke("No items to save");
            return;
        }

        // Convert CartItem sang InventoryItem format mới
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
        // string json = JsonUtility.ToJson(payload);
        // ✅ Dùng Newtonsoft.Json
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        /* string json = JsonConvert.SerializeObject(payload, settings);
         Debug.Log($"[Inventory] POST body: {json}");

         APIClient.Instance.PostJsonFull(
             "https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me/inventory",
             json,
             _ =>
             onSuccess?.Invoke(),
             onError
         );*/


        string json = JsonConvert.SerializeObject(payload, settings);
        Debug.Log($"[Inventory] POST body: {json}");

        APIClient.Instance.PostJsonFull(
            playerInventoryUrl,
            json,
            responseJson =>
            {
                // ✅ THÊM: Log response để xem item_id server tạo
                Debug.Log($"[Inventory] POST response: {responseJson}");

                // ✅ THÊM: Parse response để lấy item_id
                try
                {
                    var response = JsonConvert.DeserializeObject<InventoryPayload>(responseJson);
                    if (response?.inventory != null)
                    {
                        foreach (var item in response.inventory)
                        {
                            Debug.Log($"[Inventory] Created item_id: {item.game_item?.item_id}, " +
                                      $"external_id: {item.game_item?.external_id}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Inventory] Could not parse response: {ex.Message}");
                }

                onSuccess?.Invoke();
            },
            onError
        );

    }

    // PlayerApiService.cs
    public void LoadInventoryFromServer(Action<List<InventoryItem>> onSuccess, Action<string> onError)
    {
        var url = playerInventoryUrl;
        APIClient.Instance.GetFull(url,
            json => {
                // Ví dụ server trả List<InventoryItem>
                var items = JsonConvert.DeserializeObject<List<InventoryItem>>(json);
                onSuccess?.Invoke(items ?? new List<InventoryItem>());
            },
            onError);
    }

}





