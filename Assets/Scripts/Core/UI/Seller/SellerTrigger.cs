using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SellerTrigger : MonoBehaviour
{
    [Header("NPC Configuration")]
    [SerializeField] private NPCAPIConfig npcConfig; // THAY ĐỔI: Dùng NPCAPIConfig thay vì string

    private ShopData dynamicShopData;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (npcConfig != null)
            {
                // ✅ HIỂN THỊ TALK BUTTON NGAY LẬP TỨC
                if (MainMenuView.Instance != null)
                {
                    var baseNPC = GetComponent<BaseNPC>();
                    MainMenuView.Instance.SetNPCInteraction(true, npcConfig.npcName, null, baseNPC);
                    Debug.Log($"[SellerTrigger] Showed talk button for {npcConfig.npcName}");
                }

                FetchShopDataFromAPI();
            }
            else
            {
                Debug.LogError("NPCAPIConfig is not assigned!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (MainMenuView.Instance != null)
            {
                MainMenuView.Instance.SetNPCInteraction(false, npcConfig?.npcName ?? "Unknown", null);
            }
        }
    }

    private void FetchShopDataFromAPI()
    {
        if (ShopAPIManager.Instance != null && npcConfig != null)
        {
            ShopAPIManager.Instance.FetchShopItemsForNPC(
                npcConfig.npcId,
                OnAPISuccess,
                OnAPIError
            );
        }
    }

    private void OnAPISuccess(List<ShopItem> shopItems)
    {
        Debug.Log($"API Success: Received {shopItems.Count} items for {npcConfig.npcName}");

        // Tạo ShopData động
        dynamicShopData = ScriptableObject.CreateInstance<ShopData>();
        dynamicShopData.shopName = $"{npcConfig.npcName}'s {npcConfig.shopCategory} Store";
        SetDynamicItems(dynamicShopData, shopItems);

        if (MainMenuView.Instance != null)
        {
            var baseNPC = GetComponent<BaseNPC>();
            MainMenuView.Instance.SetNPCInteraction(true, npcConfig.npcName, dynamicShopData, baseNPC);
            //MainMenuView.Instance.UpdateShopData(npcConfig.npcName, dynamicShopData, baseNPC);
        }
    }

    private void OnAPIError(string error)
    {
        Debug.LogError($"Failed to load shop data for {npcConfig.npcName}: {error}");

        if (MainMenuView.Instance != null)
        {
            MainMenuView.Instance.SetNPCInteraction(true, npcConfig.npcName, null);
        }
    }

    private void SetDynamicItems(ShopData shopData, List<ShopItem> items)
    {
        var itemsListField = typeof(ShopData).GetField("itemsList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (itemsListField != null)
        {
            itemsListField.SetValue(shopData, items);
        }
    }
}

