// ShoppingCart.cs
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Networking;
//using UnityEditor.PackageManager.Requests;
using Newtonsoft.Json;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using Newtonsoft.Json.Linq;
using static PlayerApiService;
using System.Linq;
using static ShoppingCart;

[System.Serializable]
public class CartItem
{
    public string customId;
    public string productId;
    public string productName;
    public string brandName;
    public float price;
    public string selectedSize;
    public string imageUrl;
    public int quantity;
    public bool isPaid = false; // Đã thanh toán hay chưa
    public System.DateTime purchaseDate; // Ngày mua
    public System.DateTime trialExpiryDate; // Ngày hết hạn dùng thử
    public int trialDaysRemaining => isPaid ? -1 : Mathf.Max(0, (trialExpiryDate - System.DateTime.Now).Days);
    public float TotalPrice => price * quantity;
    public bool isSelectedForCheckout = false;

    public string gameItemId; // item_id từ server response
    public string externalId; // external_id = variant.id (dùng để cross-check)

    public CartItem()
    {
        purchaseDate = System.DateTime.Now;
        trialExpiryDate = System.DateTime.Now.AddDays(3);
    }

    // đánh dấu đã thanh toán
    public void MarkAsPaid()
    {
        isPaid = true;
        purchaseDate = System.DateTime.Now;
    }
}

[System.Serializable]
public class RetailOrderRequest
{
    public string orderTypeId = "COD"; // Thêm field này
    public string departmentId; // Thêm field này  
    public string buyerName;
    public string buyerPhone;
    public List<CartOrderItem> items; // Đổi tên từ orderItems sang items
    public string recipientAddress;
    public string recipientCountryId; // Thêm field này
    public string recipientCountryName; // Thêm field này
    public List<string> tenantCustomerCouponIds; // Thêm field này

    public int? orderSource = 0;
}

[System.Serializable]
public class CartOrderItem
{
    public string customId;
    public string tenantProductVariantId; // Thêm field này thay vì productId
    public int amount; // Đổi từ quantity sang amount
    public string newProductSkuTitle; // Thêm field này
}

[System.Serializable]
public class RetailOrderResult
{
    public string retailOrderId;
    public string retailOrderNumber;
    public float orderCharge;
    public float totalOrderAmount;
    public float shippingCost;
    public string buyerName;
    public string buyerPhone;
    public string buyerNote;
    public string recipientAddress;
}

public class ShoppingCart : MonoBehaviour
{
    public static ShoppingCart Instance { get; private set; }

    [SerializeField] private List<CartItem> cartItems = new List<CartItem>();

    public event System.Action<int> OnCartCountChanged;
    public event System.Action<List<CartItem>> OnCartUpdated;
    public CartUI cartUI;
    public int ItemCount => cartItems.Count;
    public float TotalAmount => CalculateTotalAmount();

    public event System.Action<List<CartItem>> OnUnpaidItemsUpdated;
    public event System.Action<List<CartItem>> OnPaidItemsUpdated;
    public int UnpaidItemCount => cartItems.FindAll(item => !item.isPaid).Count;
    public int PaidItemCount => cartItems.FindAll(item => item.isPaid).Count;

    public PlayerApiService playerApi;

    private void Awake()
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

 

    // Get items theo trạng thái
    public List<CartItem> GetUnpaidItems()
    {
        return cartItems.FindAll(item => !item.isPaid);
    }

    public List<CartItem> GetPaidItems()
    {
        return cartItems.FindAll(item => item.isPaid);
    }

    public List<CartItem> GetCartItems()
    {
        return new List<CartItem>(cartItems);
    }

    public void AddToInventory(CartItem newItem)
    {
        newItem.isPaid = false; // Mặc định chưa thanh toán

        var existingItem = cartItems.Find(item =>
            item.productId == newItem.productId &&
            item.customId == newItem.customId &&
            item.selectedSize == newItem.selectedSize &&
            item.isPaid == newItem.isPaid);

        if (existingItem != null)
        {
            existingItem.quantity += newItem.quantity;
            if (string.IsNullOrEmpty(existingItem.customId) && !string.IsNullOrEmpty(newItem.customId))
                existingItem.customId = newItem.customId;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newItem.customId))
                Debug.LogError($"[Cart] Missing customId for {newItem.productName}");
            cartItems.Add(newItem);
        }

        NotifyInventoryUpdated();
    }

    // ✅ THÊM: Mark items as paid after checkout
    public void MarkUnpaidItemsAsPaid()
    {
        var unpaidItems = cartItems.FindAll(item => !item.isPaid);
        foreach (var item in unpaidItems)
        {
            item.MarkAsPaid();
        }

        NotifyInventoryUpdated();
        Debug.Log($"Marked {unpaidItems.Count} items as paid");
    }

    // ✅ Giữ nguyên methods cũ cho compatibility
    public void AddItem(CartItem newItem)
    {
        AddToInventory(newItem);
    }

    public void RemoveItem(string productId, string size)
    {
        cartItems.RemoveAll(item => item.productId == productId && item.selectedSize == size);
        NotifyInventoryUpdated();
    }

    public void UpdateQuantity(string productId, string size, int newQuantity)
    {
        var item = cartItems.Find(i => i.productId == productId && i.selectedSize == size);
        if (item != null)
        {
            if (newQuantity <= 0)
            {
                RemoveItem(productId, size);
            }
            else
            {
                item.quantity = newQuantity;
                NotifyInventoryUpdated();
            }
        }
    }

    public void ClearCart()
    {
        PopupManager.Instance.ShowPopup(
            "Xác nhận",
            "Bạn có chắc muốn loại bỏ vật phẩm này?",
            () =>
            {
                cartItems.Clear();
                NotifyInventoryUpdated();
            },
            "Đồng ý"
        );
    }

    // ✅ THÊM: Clear only 1 selected unpaid item
    public void ClearUnpaidItems(CartItem targetItem)
    {
        if (targetItem == null || targetItem.isPaid) return;

        PopupManager.Instance.ShowPopup(
            "Xác nhận",
            "Bạn có chắc muốn loại bỏ vật phẩm này?",
            () =>
            {
                cartItems.Remove(targetItem);
                NotifyInventoryUpdated();
            },
            "Đồng ý"
        );
    }

    private float CalculateTotalAmount()
    {
        float total = 0f;
        foreach (var item in GetUnpaidItems())
            if (item.isSelectedForCheckout) // chỉ cộng item đã “bỏ vào giỏ”
                total += item.TotalPrice;
        return total;
    }

    // ✅ THÊM: Notify all tabs
    private void NotifyInventoryUpdated()
    {
        OnCartCountChanged?.Invoke(ItemCount);
        OnCartUpdated?.Invoke(new List<CartItem>(cartItems));
        OnUnpaidItemsUpdated?.Invoke(GetUnpaidItems());
        OnPaidItemsUpdated?.Invoke(GetPaidItems());
    }

    ///
    public void ProcessCheckout(string oderTypeID ="COD")
    {
        var selected = GetUnpaidItems().FindAll(i => i.isSelectedForCheckout);
        if (selected.Count == 0)
        {
            PopupManager.Instance.ShowPopup("Thông báo", "Giỏ hàng của bạn đang trống!", null, "Đóng");
            return;
        }

        Debug.Log("=== CHECKOUT (SELECTED) ===");
        Debug.Log($"Selected count: {selected.Count}");
        Debug.Log($"Total Amount: {TotalAmount:N0} VND");
        foreach (var it in selected)
            Debug.Log($"- {it.productName} x{it.quantity} = {it.TotalPrice:N0} VND");

        var request = new RetailOrderRequest
        {
            orderTypeId = oderTypeID,
            departmentId = "62bc4cb7-51c9-4e03-662b-09a9e145dda7",
            buyerName = cartUI.customerNameInput.text.Trim(),
            buyerPhone = cartUI.customerPhoneInput.text.Trim(),
            items = BuildOrderItems(),
            recipientAddress = cartUI.customerAddressInput.text.Trim(),
            recipientCountryId = "E2C96513-1D11-4531-8E62-31CE91946556",
            recipientCountryName = "Vietnam",
            tenantCustomerCouponIds = new List<string>()
        };

        StartCoroutine(SendOrderToBackend(request));

    }

    

    /* private List<CartOrderItem> BuildOrderItems()
     {
         var selected = GetUnpaidItems().FindAll(i => i.isSelectedForCheckout);
         var items = new List<CartOrderItem>();
         foreach (var c in selected)
         {
             *//*  items.Add(new CartOrderItem
               {
                   tenantProductVariantId = c.productId,
                   customId = c.customId,
                   amount = c.quantity,
                   newProductSkuTitle = c.selectedSize
               });*//*

             items.Add(new CartOrderItem
             {
                 tenantProductVariantId = c.productId,
                 customId = c.customId,
                 amount = c.quantity,
                 newProductSkuTitle = c.selectedSize
             });
         }
         return items;
     }*/

    private List<CartOrderItem> BuildOrderItems()
    {
        var selected = GetUnpaidItems().FindAll(i => i.isSelectedForCheckout);
        var items = new List<CartOrderItem>();

        foreach (var c in selected)
        {
            items.Add(new CartOrderItem
            {
                tenantProductVariantId = c.productId, // ✅ Đây là variant.id
                customId = c.customId,
                amount = c.quantity,
                newProductSkuTitle = c.selectedSize
            });
        }

        return items;
    }



    // Existing coroutine methods remain the same...
    private IEnumerator SendOrderToBackend(RetailOrderRequest orderRequest)
    {
        string url = "https://api.staging.storims.com/api/v1/RetailOrder/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/AnonymousOrder";
        string jsonBody = JsonConvert.SerializeObject(orderRequest, Formatting.Indented);

        Debug.Log("=== SENDING ORDER REQUEST ===");
        Debug.Log($"URL: {url}");
        Debug.Log($"JSON Body: {jsonBody}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            Debug.Log("=== RESPONSE RECEIVED ===");
            Debug.Log($"Response Code: {request.responseCode}");
            Debug.Log($"Response Body: {request.downloadHandler.text}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Gửi đơn hàng thành công!");
                var result = JsonConvert.DeserializeObject<RetailOrderResult>(request.downloadHandler.text);
                OnOrderSuccess(result);

            }
            else
            {
           
                OnOrderFailed(request.downloadHandler.text);
                
            }
        }
    }

    /*  private void OnOrderSuccess(RetailOrderResult order)
      {
          Debug.Log($"Đặt hàng thành công! Mã đơn: {order.retailOrderNumber}");

          // ✅ THAY ĐỔI: Mark as paid instead of clear
          MarkUnpaidItemsAsPaid();
      }*/

    private void OnOrderSuccess(RetailOrderResult order)
    {
        Debug.Log($"Đặt hàng thành công! Mã đơn: {order.retailOrderNumber}");
        PopupManager.Instance.ShowPopup("Thông báo", "Thanh toán thành công", null, "Đóng");

        var selected = GetUnpaidItems().FindAll(i => i.isSelectedForCheckout);
        foreach (var it in selected)
            it.MarkAsPaid();
        NotifyInventoryUpdated();
        SaveOwnedItemsToBackend(selected);
    }

    private void OnOrderFailed(string message)
    {
        Debug.LogError("Đặt hàng thất bại: " + message);
        PopupManager.Instance.ShowPopup("Lỗi", message, null, "Đóng");
    }

    public void SelectItemForCheckout(string productId, string size, bool selected)
    {
        var it = GetUnpaidItems().Find(i => i.productId == productId && i.selectedSize == size);
        if (it != null)
        {
            it.isSelectedForCheckout = selected;
            NotifyInventoryUpdated(); // để UI cập nhật tổng tiền tức thì
        }
    }
    public void SelectAllUnpaidItems(bool selected)
    {
        foreach (var it in GetUnpaidItems())
            it.isSelectedForCheckout = selected;
        NotifyInventoryUpdated();
    }

    // ShoppingCart.cs - nếu backend không hỗ trợ size
    private string BuildOwnedItemsString(IEnumerable<CartItem> items)
    {
        var list = new List<string>();
        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.productId)) continue;
            list.Add(it.productId); // CHỈ customId, không ghép @size
        }
        return string.Join(",", list);
    }



    /* private void SaveOwnedItemsToBackend(List<CartItem> paidJustNow)
     {
         var ownedString = BuildOwnedItemsString(paidJustNow); // "customId@size,..."

         var api = PlayerSelectionSync.FindFirstObjectByType<PlayerSelectionSync>();
         if (api == null)
         {
             Debug.LogError("[ShoppingCart] PlayerApiService not found in scene");
             return;
         }

         api.SaveInventoryString(
             ownedString,
             onSuccess: () => Debug.Log("[ShoppingCart] Saved inventory string"),
             onError: e => Debug.LogError("[ShoppingCart] Save inventory failed: " + e)
         );
     }*/

    private void SaveOwnedItemsToBackend(List<CartItem> paidJustNow)
    {
        var api = PlayerApiService.FindFirstObjectByType<PlayerApiService>();
        if (api == null)
        {
            Debug.LogError("ShoppingCart: PlayerApiService not found in scene");
            return;
        }

        // Gọi method mới thay vì SaveInventoryString
        api.SaveInventoryItems(
            paidJustNow,
            onSuccess: () => Debug.Log("ShoppingCart: Saved inventory successfully"),
            onError: e => Debug.LogError($"ShoppingCart: Save inventory failed {e}")
        );
    }

    // ShoppingCart.cs
    public void AddFromProductApi(string productDetailFullUrl, string selectedSize, int quantity = 1)
    {
        StartCoroutine(AddFromProductApiRoutine(productDetailFullUrl, selectedSize, quantity));
    }

    private static string ResolveVariantIdFromJson(string productJson, string selectedSize)
    {
        var root = JObject.Parse(productJson);
        var variants = root["variants"] as JArray;
        if (variants != null)
        {
            foreach (var v in variants)
            {
                var attrs = v["attributes"] as JArray;
                if (attrs != null)
                {
                    foreach (var a in attrs)
                    {
#nullable enable annotations
                        var name = (string?)a["name"] ?? (string?)a["value"];
                        if (!string.IsNullOrWhiteSpace(name) &&
                            string.Equals(name.Trim(), selectedSize.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return (string)v["id"];
                        }
                    }
                }
            }
        }
        // Fallback nếu không tìm được variant (không khóa luồng checkout)
        return (string?)root["id"] ?? string.Empty;
    }

    private IEnumerator AddFromProductApiRoutine(string url, string selectedSize, int qty)
    {
        string json = null; string err = null;
        APIClient.Instance.GetFull(url, s => json = s, e => err = e); // gọi API chi tiết
        while (json == null && err == null) yield return null;
        if (err != null) { Debug.LogError(err); yield break; }

        var root = JObject.Parse(json);
        /*var variantId = ResolveVariantIdFromJson(json, selectedSize);*/
        var item = new CartItem
        {
            productId = (string)root["productId"],                                      // dùng làm tenantProductVariantId
            customId = (string)root["customId"],                       // lưu lại mã sản phẩm
            productName = (string)root["title"],
            brandName = (string)root["brandName"],
            imageUrl = (string)root["imageUrl"],
            price = (float)((double?)root["price"] ?? 0),
            selectedSize = selectedSize,
            quantity = qty
        };
        AddToInventory(item); // tái sử dụng flow giỏ hàng sẵn có
    }

    // GameBoot.cs (hoặc MainMenu)
    private void Start()
    {
        playerApi = FindFirstObjectByType<PlayerApiService>();
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        // 1) Đợi token sẵn sàng nếu token load async
        yield return new WaitUntil(() => APIClient.Instance != null /*&& HasToken()*/);

        // 2) Gọi GET inventory
        var cart = ShoppingCart.Instance;
        var api = FindFirstObjectByType<PlayerApiService>();

        api.LoadInventoryFromServer(
            items =>
            {
                // items: List<PlayerSelectionSync.InventoryItem>
                StartCoroutine(EnrichAndAddRange(items)); // phương án đã có trong ShoppingCart
            },
            err => Debug.LogError("Load inventory failed: " + err)
        );
    }

    // ShoppingCart.cs
    public void RefreshOwnedFromServer()
    {
        playerApi.LoadInventoryFromServer(items =>
        {
            StartCoroutine(EnrichAndAddRange(items));
        }, err => Debug.LogError($"Load inventory failed: {err}"));
    }

    private IEnumerator EnrichAndAddRange(List<InventoryItem> items)
    {
        foreach (var it in items)
        {
            var gi = it.game_item; // có external_id, name, image_url...
                                   // Tách customId/size nếu cần: external_id có thể là "customId@Size 42"
            var ext = gi.external_id ?? "";
            var parts = ext.Split('@');
            var customId = parts[0];
            var sizeName = parts.Length > 1 ? parts[1] : "";

            // Gọi chi tiết sản phẩm theo customId để lấy title/brand/price/image (nếu muốn override)
            string detailUrl = $"https://data.storims.c1.hubcom.tech/api/v1/TenantProduct/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/{customId}";
            string productJson = null; string error = null;
            APIClient.Instance.GetFull(detailUrl, j => productJson = j, e => error = e);
            while (productJson == null && error == null) yield return null;

            // Map sang CartItem hiển thị trong túi
            var cartItem = new CartItem
            {
                gameItemId = gi.item_id,
                customId = customId,
                productId = ext,                // lưu external_id để đối soát
                productName = gi.name,
                brandName = "",                 // có thể điền từ productJson
                price = 0,                      // có thể điền từ productJson/gi.price
                selectedSize = sizeName,
                imageUrl = gi.image_url,
                quantity = it.quantity,
                isPaid = true,
                purchaseDate = DateTime.Now
            };
            // --- LOGIC TÁCH BRAND & SIZE TỪ DESCRIPTION CỦA BACKEND ---
            // Backend trả về: "Jeep - Size 36"
            string desc = gi.description ?? "";
            if (desc.Contains("-"))
            {
                var part = desc.Split(new[] { '-' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (part.Length >= 2)
                {
                    cartItem.brandName = part[0].Trim();      // "Jeep"
                    cartItem.selectedSize = part[1].Trim();   // "Size 36"
                }
                else
                {
                    cartItem.brandName = desc; // Fallback
                    cartItem.selectedSize = "";
                }
            }
            else
            {
                // Trường hợp không có dấu gạch ngang
                cartItem.brandName = "";
                cartItem.selectedSize = desc;
            }
            // ----------------------------------------------------------
            cartItems.Add(cartItem);
        }
        NotifyInventoryUpdated();
    }

    // ShoppingCart.cs
    /*  public void DeleteOwnedItemById(string itemId)
      {
          string url = $"https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me/inventory/{itemId}";
          APIClient.Instance.DeleteFull(url,
              json => {
                  // server trả true
                  // Xóa local
                  var idx = cartItems.FindIndex(c => c.gameItemId == itemId && c.isPaid);
                  if (idx >= 0) cartItems.RemoveAt(idx);
                  NotifyInventoryUpdated();
                  Debug.Log($"Deleted inventory item: {itemId}");
              },
              err => Debug.LogError($"Delete inventory failed: {err}")
          );
      }*/
    // ShoppingCart.cs
    public void DeleteOwnedItemById(string itemId)
    {
        var url = $"https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me/inventory/{itemId}";
        APIClient.Instance.DeleteFull(url,
            _ => {
                cartItems.RemoveAll(c => c.gameItemId == itemId && c.isPaid);
                NotifyInventoryUpdated();
            },
            err => Debug.LogError($"Delete inventory failed: {err}")
        );
    }


}





