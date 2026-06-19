// ShoppingCart.cs
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using static PlayerApiService;

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
    public DateTime purchaseDate; // Ngày mua
    public DateTime trialExpiryDate; // Ngày hết hạn dùng thử
    public int trialDaysRemaining => isPaid ? -1 : Mathf.Max(0, (trialExpiryDate - DateTime.Now).Days);
    public float TotalPrice => price * quantity;
    public bool isSelectedForCheckout = false;

    public string gameItemId; // item_id từ server response
    public string externalId; // external_id = variant.id (dùng để cross-check)

    public CartItem()
    {
        purchaseDate = DateTime.Now;
        trialExpiryDate = DateTime.Now.AddDays(3);
    }

    // đánh dấu đã thanh toán
    public void MarkAsPaid()
    {
        isPaid = true;
        purchaseDate = DateTime.Now;
    }
}

[System.Serializable]
public class RetailOrderRequest
{
    public string orderTypeId = "COD";
    public string departmentId;
    public string buyerName;
    public string buyerPhone;
    public List<CartOrderItem> items;
    public string recipientAddress;
    public string recipientCountryId;
    public string recipientCountryName;
    public List<string> tenantCustomerCouponIds;
    public int? orderSource = 0;
}

[System.Serializable]
public class CartOrderItem
{
    public string customId;
    public string tenantProductVariantId;
    public int amount;
    public string newProductSkuTitle;
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
    public CartUI cartUI;
    public PlayerApiService playerApi;

    // Map (customId, size) → unpaid item — O(1) lookup khi select checkout.
    // Dùng customId (không phải productId) vì nghiệp vụ phân biệt mặt hàng theo customId:
    // cùng productId+size nhưng khác customId = 2 mặt hàng riêng (server trả variant khác nhau).
    private readonly Dictionary<(string customId, string size), CartItem> _unpaidItemMap
        = new Dictionary<(string customId, string size), CartItem>();

    // Cached split lists — rebuild trong NotifyInventoryUpdated, tránh FindAll mỗi call
    private readonly List<CartItem> _cachedUnpaid = new();
    private readonly List<CartItem> _cachedPaid = new();

    public event Action<int> OnCartCountChanged;
    public event Action<List<CartItem>> OnCartUpdated;
    public event Action<List<CartItem>> OnUnpaidItemsUpdated;
    public event Action<List<CartItem>> OnPaidItemsUpdated;

    public int ItemCount => cartItems.Count;

    private int _unpaidCount;
    private int _paidCount;
    public int UnpaidItemCount => _unpaidCount;
    public int PaidItemCount => _paidCount;

    private float _currentSelectedTotal = 0f;
    private int _selectedItemCount = 0;
    public float TotalAmount => _currentSelectedTotal;

    public int GetSelectedCheckoutCount() => _selectedItemCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        playerApi = FindFirstObjectByType<PlayerApiService>();
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        // 1) Đợi APIClient sẵn sàng
        yield return new WaitUntil(() => APIClient.Instance != null);

        // 2) Gọi GET inventory từ server
        var api = FindFirstObjectByType<PlayerApiService>();
        if (api == null)
        {
            Debug.LogError("[ShoppingCart] PlayerApiService not found in scene");
            yield break;
        }

        api.LoadInventoryFromServer(
            items => StartCoroutine(EnrichAndAddRange(items)),
            err => Debug.LogError($"[ShoppingCart] Load inventory failed: {err}")
        );
    }

    // Khoá định danh unpaid item = (customId, size). Cùng productId nhưng khác customId
    // được coi là 2 mặt hàng riêng nên KHÔNG được trùng khoá.
    private static (string customId, string size) MakeKey(string customId, string size)
        => (customId ?? string.Empty, size ?? string.Empty);

    private void RecalculateSelectionAggregates()
    {
        _currentSelectedTotal = 0f;
        _selectedItemCount = 0;

        foreach (var item in cartItems)
        {
            if (!item.isPaid && item.isSelectedForCheckout)
            {
                _selectedItemCount += 1;
                _currentSelectedTotal += item.TotalPrice;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GET ITEMS — dùng cached lists (O(n) copy thay vì O(n) FindAll + check)
    // ═══════════════════════════════════════════════════════════════════════

    public List<CartItem> GetUnpaidItems()
        => new List<CartItem>(_cachedUnpaid);

    public List<CartItem> GetPaidItems()
        => new List<CartItem>(_cachedPaid);

    public List<CartItem> GetCartItems()
        => new List<CartItem>(cartItems);

    // ═══════════════════════════════════════════════════════════════════════
    // ADD / REMOVE / UPDATE
    // ═══════════════════════════════════════════════════════════════════════

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

            // Đảm bảo map trỏ về đúng instance unpaid hiện tại
            var key = MakeKey(existingItem.customId, existingItem.selectedSize);
            _unpaidItemMap[key] = existingItem;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newItem.customId))
                Debug.LogError($"[ShoppingCart] Missing customId for {newItem.productName}");
            cartItems.Add(newItem);

            // Thêm item unpaid mới vào map
            var key = MakeKey(newItem.customId, newItem.selectedSize);
            _unpaidItemMap[key] = newItem;
        }

        NotifyInventoryUpdated();
    }

    public void MarkUnpaidItemsAsPaid()
    {
        // Iterate trực tiếp cartItems để tránh alloc List<CartItem> từ FindAll
        int marked = 0;
        foreach (var item in cartItems)
        {
            if (item.isPaid) continue;
            item.MarkAsPaid();

            // Bỏ khỏi map unpaid khi chuyển sang paid
            var key = MakeKey(item.customId, item.selectedSize);
            _unpaidItemMap.Remove(key);
            marked++;
        }

        NotifyInventoryUpdated();
#if UNITY_EDITOR
        GameLog.Info($"[ShoppingCart] Marked {marked} items as paid");
#endif
    }

    // Giữ cho compatibility
    public void AddItem(CartItem newItem) => AddToInventory(newItem);

    public void RemoveItem(string customId, string size)
    {
        // Xoá khỏi map unpaid trước
        var key = MakeKey(customId, size);
        _unpaidItemMap.Remove(key);

        cartItems.RemoveAll(item => item.customId == customId && item.selectedSize == size && !item.isPaid);
        NotifyInventoryUpdated();
    }

    public void UpdateQuantity(string customId, string size, int newQuantity)
    {
        var item = cartItems.Find(i => i.customId == customId && i.selectedSize == size && !i.isPaid);
        if (item == null) return;

        if (newQuantity <= 0)
        {
            RemoveItem(customId, size);
        }
        else
        {
            item.quantity = newQuantity;
            NotifyInventoryUpdated();
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
                _unpaidItemMap.Clear();
                NotifyInventoryUpdated();
            },
            "Đồng ý"
        );
    }

    public void ClearUnpaidItems(CartItem targetItem)
    {
        if (targetItem == null || targetItem.isPaid) return;

        PopupManager.Instance.ShowPopup(
            "Xác nhận",
            "Bạn có chắc muốn loại bỏ vật phẩm này?",
            () =>
            {
                var key = MakeKey(targetItem.customId, targetItem.selectedSize);
                _unpaidItemMap.Remove(key);
                cartItems.Remove(targetItem);
                NotifyInventoryUpdated();
            },
            "Đồng ý"
        );
    }

    public void ClearCheckoutSelection()
    {
        foreach (var it in cartItems)
            if (!it.isPaid) it.isSelectedForCheckout = false;

        _selectedItemCount = 0;
        _currentSelectedTotal = 0f;

        NotifyInventoryUpdated();
    }

    public void SetCheckoutSelection(HashSet<CartItem> selectedSet)
    {
        foreach (var it in cartItems)
        {
            if (it.isPaid) continue;
            it.isSelectedForCheckout = selectedSet.Contains(it);
        }

        // Batch update -> tính lại từ đầu
        RecalculateSelectionAggregates();
        NotifyInventoryUpdated();
    }

    // ✅ Notify all tabs với cached lists
    private void NotifyInventoryUpdated()
    {
        _cachedUnpaid.Clear();
        _cachedPaid.Clear();
        foreach (var it in cartItems)
        {
            if (it.isPaid) _cachedPaid.Add(it);
            else _cachedUnpaid.Add(it);
        }

        _unpaidCount = _cachedUnpaid.Count;
        _paidCount = _cachedPaid.Count;

        OnCartCountChanged?.Invoke(cartItems.Count);
        OnUnpaidItemsUpdated?.Invoke(_cachedUnpaid);
        OnPaidItemsUpdated?.Invoke(_cachedPaid);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHECKOUT
    // ═══════════════════════════════════════════════════════════════════════

    public void ProcessCheckout(string oderTypeID = "COD")
    {
        // Iterate cached unpaid trực tiếp (chứ không alloc thêm via FindAll)
        int selectedCount = 0;
        foreach (var it in _cachedUnpaid)
            if (it.isSelectedForCheckout) selectedCount++;

        if (selectedCount == 0)
        {
            PopupManager.Instance.ShowPopup("Thông báo", "Giỏ hàng của bạn đang trống!", null, "Đóng");
            return;
        }

#if UNITY_EDITOR
        GameLog.Info("[ShoppingCart] === CHECKOUT (SELECTED) ===");
        GameLog.Info($"[ShoppingCart] Selected count: {selectedCount}");
        GameLog.Info($"[ShoppingCart] Total Amount: {TotalAmount:N0} VND");
        foreach (var it in _cachedUnpaid)
            if (it.isSelectedForCheckout)
                GameLog.Info($"[ShoppingCart]  - {it.productName} x{it.quantity} = {it.TotalPrice:N0} VND");
#endif

        var request = new RetailOrderRequest
        {
            orderTypeId = oderTypeID,
            departmentId = "62bc4cb7-51c9-4e03-662b-09a9e145dda7",
            buyerName = cartUI.customerNameInput.text.Trim(),
            buyerPhone = cartUI.customerPhoneInput.text.Trim(),
            items = BuildOrderItems(selectedCount),
            recipientAddress = cartUI.customerAddressInput.text.Trim(),
            recipientCountryId = "E2C96513-1D11-4531-8E62-31CE91946556",
            recipientCountryName = "Vietnam",
            tenantCustomerCouponIds = new List<string>()
        };

        StartCoroutine(SendOrderToBackend(request));
    }

    // Pre-allocate capacity = selectedCount, tránh resize List
    private List<CartOrderItem> BuildOrderItems(int selectedCount)
    {
        var items = new List<CartOrderItem>(selectedCount);
        foreach (var c in _cachedUnpaid)
        {
            if (!c.isSelectedForCheckout) continue;
            items.Add(new CartOrderItem
            {
                tenantProductVariantId = c.productId, // variant.id
                customId = c.customId,
                amount = c.quantity,
                newProductSkuTitle = c.selectedSize
            });
        }
        return items;
    }

    private IEnumerator SendOrderToBackend(RetailOrderRequest orderRequest, Action<RetailOrderResult> onSuccess = null)
    {
        // Endpoint external storims — không cần Mantini Bearer token, dùng UnityWebRequest trực tiếp (AnonymousOrder)
        const string url = "https://api.staging.storims.com/api/v1/RetailOrder/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/AnonymousOrder";
        string jsonBody = JsonConvert.SerializeObject(orderRequest, Formatting.Indented);

#if UNITY_EDITOR
        // Body chứa PII (buyerName, buyerPhone, recipientAddress) — chỉ log editor
        GameLog.Info($"[ShoppingCart] === SENDING ORDER === URL: {url}");
        GameLog.Info($"[ShoppingCart] JSON Body: {jsonBody}");
#endif

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

#if UNITY_EDITOR
            GameLog.Info($"[ShoppingCart] === RESPONSE === code={request.responseCode}");
            GameLog.Info($"[ShoppingCart] Body: {request.downloadHandler.text}");
#endif

            if (request.result == UnityWebRequest.Result.Success)
            {
                var result = JsonConvert.DeserializeObject<RetailOrderResult>(request.downloadHandler.text);
                if (onSuccess != null) onSuccess(result);
                else OnOrderSuccess(result);
            }
            else
            {
                OnOrderFailed(request.downloadHandler.text);
            }
        }
    }

    private void OnOrderSuccess(RetailOrderResult order)
    {
        GameLog.Info($"[ShoppingCart] Đặt hàng thành công! Mã đơn: {order.retailOrderNumber}");
        PopupManager.Instance.ShowPopup("Thông báo", "Thanh toán thành công", null, "Đóng");
        TutorialGamePlay.Instance?.OnCheckoutCompleted();

        // Iterate cached unpaid + lưu paid items vào temp list để gửi backend
        var paidNow = new List<CartItem>();
        foreach (var it in _cachedUnpaid)
        {
            if (!it.isSelectedForCheckout) continue;
            it.MarkAsPaid();
            var key = MakeKey(it.customId, it.selectedSize);
            _unpaidItemMap.Remove(key);
            paidNow.Add(it);
        }

        NotifyInventoryUpdated();
        SaveOwnedItemsToBackend(paidNow);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUY NOW — mua trực tiếp 1 vật phẩm đang xem, KHÔNG đụng giỏ / cờ chọn
    // ═══════════════════════════════════════════════════════════════════════

    // item: bản sao vật phẩm đang xem ở ProductDetailUI (không cần nằm trong cartItems).
    // Gửi đơn riêng cho đúng item này; các món đang chọn trong giỏ KHÔNG bị ảnh hưởng.
    public void ProcessBuyNow(CartItem item, string oderTypeID = "COD")
    {
        if (item == null)
        {
            PopupManager.Instance.ShowPopup("Thông báo", "Không có vật phẩm để mua!", null, "Đóng");
            return;
        }

#if UNITY_EDITOR
        GameLog.Info("[ShoppingCart] === BUY NOW (SINGLE) ===");
        GameLog.Info($"[ShoppingCart]  - {item.productName} x{item.quantity} = {item.TotalPrice:N0} VND");
#endif

        var request = new RetailOrderRequest
        {
            orderTypeId = oderTypeID,
            departmentId = "62bc4cb7-51c9-4e03-662b-09a9e145dda7",
            buyerName = cartUI.customerNameInput.text.Trim(),
            buyerPhone = cartUI.customerPhoneInput.text.Trim(),
            items = new List<CartOrderItem>(1)
            {
                new CartOrderItem
                {
                    tenantProductVariantId = item.productId, // variant.id
                    customId = item.customId,
                    amount = item.quantity,
                    newProductSkuTitle = item.selectedSize
                }
            },
            recipientAddress = cartUI.customerAddressInput.text.Trim(),
            recipientCountryId = "E2C96513-1D11-4531-8E62-31CE91946556",
            recipientCountryName = "Vietnam",
            tenantCustomerCouponIds = new List<string>()
        };

        StartCoroutine(SendOrderToBackend(request, _ => OnBuyNowSuccess(item)));
    }

    // Mua ngay thành công: chỉ đánh dấu + lưu đúng item đó vào inventory đã mua.
    // Giữ nguyên giỏ & cờ chọn của các món khác.
    private void OnBuyNowSuccess(CartItem item)
    {
        GameLog.Info("[ShoppingCart] Mua ngay thành công!");
        PopupManager.Instance.ShowPopup("Thông báo", "Thanh toán thành công", null, "Đóng");
        TutorialGamePlay.Instance?.OnCheckoutCompleted();

        // Nếu item này vốn nằm trong giỏ unpaid (cùng customId+size) thì đánh dấu paid + gỡ khỏi map.
        var key = MakeKey(item.customId, item.selectedSize);
        if (_unpaidItemMap.TryGetValue(key, out var inCart) && !inCart.isPaid)
        {
            if (inCart.isSelectedForCheckout)
            {
                inCart.isSelectedForCheckout = false;
                _selectedItemCount = Mathf.Max(0, _selectedItemCount - 1);
                _currentSelectedTotal = Mathf.Max(0f, _currentSelectedTotal - inCart.TotalPrice);
            }
            inCart.MarkAsPaid();
            _unpaidItemMap.Remove(key);
            SaveOwnedItemsToBackend(new List<CartItem> { inCart });
        }
        else
        {
            // Item mua ngay chưa có trong giỏ: thêm thẳng vào inventory như hàng đã mua.
            item.MarkAsPaid();
            cartItems.Add(item);
            SaveOwnedItemsToBackend(new List<CartItem> { item });
        }

        NotifyInventoryUpdated();
    }

    private void OnOrderFailed(string message)
    {
        Debug.LogError("[ShoppingCart] Đặt hàng thất bại: " + message);
        PopupManager.Instance.ShowPopup("Lỗi", message, null, "Đóng");
    }

    public void SelectItemForCheckout(string customId, string size, bool selected)
    {
        var key = MakeKey(customId, size);

        if (!_unpaidItemMap.TryGetValue(key, out var it)) return;
        if (it.isPaid) return;

        // Nếu state không đổi thì không làm gì
        if (it.isSelectedForCheckout == selected) return;

        // Cập nhật aggregate incrementally TRƯỚC khi notify (O(1) thay vì O(n) recalc)
        if (selected)
        {
            _selectedItemCount += 1;
            _currentSelectedTotal += it.TotalPrice;
        }
        else
        {
            _selectedItemCount -= 1;
            _currentSelectedTotal -= it.TotalPrice;
        }

        it.isSelectedForCheckout = selected;

        // Clamp an toàn nếu có case lệch state
        if (_selectedItemCount < 0) _selectedItemCount = 0;
        if (_currentSelectedTotal < 0f) _currentSelectedTotal = 0f;

        NotifyInventoryUpdated();
        TutorialGamePlay.Instance?.OnAddSelectedToCartSuccess();
    }

    public void SelectAllUnpaidItems(bool selected)
    {
        foreach (var it in _cachedUnpaid)
            it.isSelectedForCheckout = selected;

        RecalculateSelectionAggregates();
        NotifyInventoryUpdated();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BACKEND SYNC — owned items
    // ═══════════════════════════════════════════════════════════════════════

    private void SaveOwnedItemsToBackend(List<CartItem> paidJustNow)
    {
        var api = FindFirstObjectByType<PlayerApiService>();
        if (api == null)
        {
            Debug.LogError("[ShoppingCart] PlayerApiService not found in scene");
            return;
        }

        api.SaveInventoryItems(
            paidJustNow,
            onSuccess: () => GameLog.Info("[ShoppingCart] Saved inventory successfully"),
            onError: e => Debug.LogError($"[ShoppingCart] Save inventory failed: {e}")
        );
    }

    public void AddFromProductApi(string productDetailFullUrl, string selectedSize, int quantity = 1)
        => StartCoroutine(AddFromProductApiRoutine(productDetailFullUrl, selectedSize, quantity));

    private IEnumerator AddFromProductApiRoutine(string url, string selectedSize, int qty)
    {
        string json = null;
        string err = null;
        APIClient.Instance.GetFull(url, s => json = s, e => err = e);
        while (json == null && err == null) yield return null;
        if (err != null)
        {
            Debug.LogError($"[ShoppingCart] AddFromProductApi failed: {err}");
            yield break;
        }

        var root = JObject.Parse(json);
        var item = new CartItem
        {
            productId = (string)root["productId"],
            customId = (string)root["customId"],
            productName = (string)root["title"],
            brandName = (string)root["brandName"],
            imageUrl = (string)root["imageUrl"],
            price = (float)((double?)root["price"] ?? 0),
            selectedSize = selectedSize,
            quantity = qty
        };
        AddToInventory(item);
    }

    public void RefreshOwnedFromServer()
    {
        if (playerApi == null) playerApi = FindFirstObjectByType<PlayerApiService>();
        if (playerApi == null)
        {
            Debug.LogError("[ShoppingCart] PlayerApiService not found");
            return;
        }

        playerApi.LoadInventoryFromServer(
            items => StartCoroutine(EnrichAndAddRange(items)),
            err => Debug.LogError($"[ShoppingCart] Load inventory failed: {err}")
        );
    }

    // Compile regex 1 lần — tránh re-compile mỗi item
    private static readonly System.Text.RegularExpressions.Regex SizeRegex =
        new System.Text.RegularExpressions.Regex(
            @"Size\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private IEnumerator EnrichAndAddRange(List<InventoryItem> items)
    {
        if (items == null || items.Count == 0)
        {
            NotifyInventoryUpdated();
            yield break;
        }

        foreach (var it in items)
        {
            var gi = it.game_item;

            // external_id = variant UUID — dùng làm tenantProductVariantId
            var variantId = gi.external_id ?? "";

            // Parse "Size XX" từ tên sản phẩm
            string sizeFromName = "";
            string productName = gi.name ?? "";
            var sizeMatch = SizeRegex.Match(productName);
            if (sizeMatch.Success)
                sizeFromName = "Size " + sizeMatch.Groups[1].Value;

            var cartItem = new CartItem
            {
                gameItemId = gi.item_id,
                customId = "",
                productId = variantId,
                productName = productName,
                brandName = "",
                price = 0,
                selectedSize = sizeFromName,
                imageUrl = gi.image_url,
                quantity = it.quantity,
                isPaid = true,
                purchaseDate = DateTime.Now
            };

            // Tách brand & size từ description — backend trả "Jeep - Size 36"
            string desc = gi.description ?? "";
            if (desc.Contains("-"))
            {
                var part = desc.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (part.Length >= 2)
                {
                    cartItem.brandName = part[0].Trim();
                    cartItem.selectedSize = part[1].Trim();
                }
                else
                {
                    cartItem.brandName = desc;
                    cartItem.selectedSize = "";
                }
            }
            else if (!string.IsNullOrEmpty(desc))
            {
                cartItem.brandName = "";
                cartItem.selectedSize = desc;
            }
            // Nếu description rỗng → dùng sizeFromName đã parse từ name

            cartItems.Add(cartItem);
        }

        NotifyInventoryUpdated();
        // Không có yield return thực sự — giữ IEnumerator để callers dùng StartCoroutine
    }

    public void DeleteOwnedItemById(string itemId)
    {
        var url = $"https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me/inventory/{itemId}";
        APIClient.Instance.DeleteFull(url,
            _ =>
            {
                cartItems.RemoveAll(c => c.gameItemId == itemId && c.isPaid);
                NotifyInventoryUpdated();
            },
            err => Debug.LogError($"[ShoppingCart] Delete inventory failed: {err}")
        );
    }
}