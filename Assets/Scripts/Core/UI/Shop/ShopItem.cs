using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static MainMenuViewModel;

public enum ItemType
{
    Shoes,
    Cloth,
    Pants,
    Hat,
    Gloves
}

[CreateAssetMenu(fileName = "ShopItem", menuName = "Shop/Item")]
public class ShopItem : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    public string itemID; // Key cho Dictionary
    public int price;
    public int regularPrice; // Giá gốc (nếu có khuyến mãi)
    public Sprite icon;
    [TextArea(3, 5)]
    public string description;

    [Header("API Integration")]
    public string imageUrl; // THÊM: URL của ảnh từ API
    public string apiTitle;      // THÊM: Title gốc từ API (có Unicode)
    public string apiBrandName;  // THÊM: Brand name gốc từ API

    [Header("Item Properties")]
    public ItemType type; // Weapon, Armor, Consumable, etc.
    public int maxStack = 1;
    public bool isUnlocked = true;

    [Header("API Data Reference")]
    [System.NonSerialized]
    private APIProductItem apiData; // Lưu data gốc từ API

    public void SetAPIData(APIProductItem data)
    {
        apiData = data;
    }

    public APIProductItem GetAPIData()
    {
        return apiData;
    }

    // THÊM: Method để update từ API data
    public void UpdateFromAPI(APIProductItem apiItem)
    {
        itemID = apiItem.id;
        apiTitle = apiItem.title;           // Giữ nguyên Unicode
        apiBrandName = apiItem.brandName;   // Giữ nguyên Unicode
        price = Mathf.RoundToInt(apiItem.price);
        imageUrl = apiItem.imageUrl;
        regularPrice = Mathf.RoundToInt(apiItem.regularPrice);
        // Tạo display name an toàn
        itemName = !string.IsNullOrEmpty(apiItem.title) ?
            apiItem.title : "Unknown Item";

        // Description có thể chứa Unicode
        description = $"Brand: {apiBrandName}\nPrice: {price} Gold";
    }
}
