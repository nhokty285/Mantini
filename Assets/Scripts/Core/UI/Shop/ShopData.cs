using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MainMenuViewModel;

[CreateAssetMenu(fileName = "ShopData", menuName = "Shop/ShopData")]
public class ShopData : ScriptableObject
{
    [Header("Shop Configuration")]
    public string shopName = "General Store";
    public Sprite shopIcon;

    [Header("API Items - Sẽ được load từ API")]
    [SerializeField] private List<ShopItem> itemsList = new List<ShopItem>();

    // Dictionary sẽ được tạo runtime từ itemsList
    private Dictionary<string, ShopItem> itemsDict;

    public Dictionary<string, ShopItem> ItemsDictionary
    {
        get
        {
            if (itemsDict == null)
                BuildDictionary();
            return itemsDict;
        }
    }

    // THÊM method để set dynamic items
    public void SetDynamicItems(List<ShopItem> dynamicItems)
    {
        itemsList = dynamicItems;
        itemsDict = null; // Force rebuild dictionary
    }

    private void BuildDictionary()
    {
        itemsDict = new Dictionary<string, ShopItem>();
        foreach (var item in itemsList)
        {
            if (item != null && !string.IsNullOrEmpty(item.itemID))
            {
                itemsDict[item.itemID] = item;
            }
        }
    }

    public void ClearCache()
    {
        itemsDict = null; // Reset dictionary cache
        Debug.Log("Shop data cache cleared");
    }

    // Optional: Method để clear specific item references
    public void ClearItemReferences()
    {
        if (itemsDict != null)
        {
            itemsDict.Clear();
            itemsDict = null;
        }
    }

    // PUBLIC: Method để thêm API items với Unicode support
 

    public ShopItem GetItem(string itemID) 
        => ItemsDictionary.ContainsKey(itemID) ? ItemsDictionary[itemID] : null;

    public List<ShopItem> GetItemsByType(ItemType type) 
        => itemsList.FindAll(item => item.type == type);

    public bool HasItem(string itemID) 
        => ItemsDictionary.ContainsKey(itemID);
}
