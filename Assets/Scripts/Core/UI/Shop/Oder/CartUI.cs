using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CartUI : MonoBehaviour
{
    [SerializeField] private GameObject moreObject;
    [SerializeField] private Button moreButton;
    [SerializeField] private Button selectAllToCartButton;
    [SerializeField] private Button closeMoreButton;

    [Header("Button State Icons")]
    [SerializeField] private GameObject selectAllIcon;

    [Header("Cart Button")]
    [SerializeField] private Button cartButton;
    [SerializeField] private TextMeshProUGUI cartCountText;
    [SerializeField] private GameObject cartCountBadge;

    [Header("Cart Panel")]
    [SerializeField] private GameObject cartPanel;
    [SerializeField] private Transform cartItemsContainer;
    [SerializeField] private TextMeshProUGUI totalAmountText;
    [SerializeField] private Button checkoutButton;
    [SerializeField] private Button continueShopButton;

    [Header("Customer Info")]
    // ⚠️ Các field dưới giữ public vì code khác/scene wiring có thể dùng trực tiếp
    public TMP_InputField customerNameInput;
    public TMP_InputField customerPhoneInput;
    public TMP_InputField customerAddressInput;
    public TMP_InputField customerNoteInput;
    public GameObject customerInfoPanel;
    public Button buyButton;
    public Button backButton;

    [Header("Checkout Localization (VI)")]
    [SerializeField] private string placeholderName = "Họ và tên";
    [SerializeField] private string placeholderPhone = "Số điện thoại";
    [SerializeField] private string placeholderAddress = "Địa chỉ giao hàng";
    [SerializeField] private string placeholderNote = "Ghi chú (tuỳ chọn)";
    [SerializeField] private string buyButtonLabel = "Đặt hàng";

    [Header("Validation (Inline Errors)")]
    // Optional: nếu để trống, label lỗi sẽ được tự tạo dưới mỗi ô lúc runtime.
    [SerializeField] private TextMeshProUGUI nameErrorText;
    [SerializeField] private TextMeshProUGUI phoneErrorText;
    [SerializeField] private TextMeshProUGUI addressErrorText;
    [SerializeField] private Color errorColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    [Header("Payment Method")]
    public GameObject paymentMethodPanel;
    public Button codButton;
    public Button bankButton;
    public Button changePaymentButton;
    public TextMeshProUGUI selectedPaymentText;

    [Header("Inventory Tabs")]
    [SerializeField] private Button unpaidTab;
    [SerializeField] private Button paidTab;
    [SerializeField] private Button futureTab3;
    [SerializeField] private Button futureTab4;

    [Header("Image Grid System")]
    [SerializeField] private GameObject cartImageItemPrefab;

    [Header("Auto Refresh")]
    public bool autoUpdateTotalAmount = true;
    public GameObject shopController;

    [Header("Select Mode")]
    [SerializeField] private List<RectTransform> selectZones = new List<RectTransform>();
    [SerializeField] public bool isSelectMode = false;
    [SerializeField] private int number;

    [Header("Long Press")]
    [SerializeField] private float longPressThreshold = 0.5f;

    // ── State ────────────────────────────────────────────────────────────────
    private enum InventoryTab { Unpaid, Paid, Future3, Future4 }

    private InventoryTab _currentTab = InventoryTab.Unpaid;
    private string _selectedPaymentMethod = "COD";

    // Buy Now (mua ngay) — tái dùng panel nhập liệu nhưng checkout riêng 1 item.
    private bool _buyNowMode = false;
    private CartItem _buyNowItem;
    private int _ignoreOutsideClickUntilFrame = -1;

    // Inline error labels (resolved 1 lần — serialized ref hoặc auto-create)
    private TextMeshProUGUI _nameError;
    private TextMeshProUGUI _phoneError;
    private TextMeshProUGUI _addressError;
    private bool _errorLabelsReady;

    // Long press
    private CartItem _longPressCandidate;
    private float _longPressTimer;
    private bool _longPressConsumed;

    // Khoá định danh cell = (customId, size) — đồng bộ với ShoppingCart._unpaidItemMap.
    // Cùng productId nhưng khác customId là 2 mặt hàng riêng nên KHÔNG trùng khoá.
    private readonly Dictionary<(string customId, string size), CartImageItem> _cellLookup
        = new Dictionary<(string customId, string size), CartImageItem>();

    private readonly List<CartImageItem> _spawnedCells = new();

    // ═══════════════════════════════════════════════════════════════════════
    // LONG-PRESS PUBLIC API (CartImageItem gọi)
    // ═══════════════════════════════════════════════════════════════════════

    public void BeginLongPress(CartItem item)
    {
#if UNITY_EDITOR
        GameLog.Info($"[CartUI][LongPress] Begin: {item.productName}");
#endif
        _longPressCandidate = item;
        _longPressTimer = 0f;
        _longPressConsumed = false;
    }

    public void CancelLongPress()
    {
        _longPressCandidate = null;
        _longPressTimer = 0f;
    }

    public bool IsLongPressConsumed() => _longPressConsumed;

    private void HandleLongPress()
    {
        if (_longPressCandidate == null || _longPressConsumed) return;

        _longPressTimer += Time.deltaTime;
        if (_longPressTimer < longPressThreshold) return;

        var item = _longPressCandidate;
        _longPressConsumed = true;
        _longPressCandidate = null;
        _longPressTimer = 0f;

        ShowProductDetailInMainUI(item);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    private void Start()
    {
        SetupEventListeners();
        SetupTabSystem();
        InitializeUI();

        // Chuẩn hoá ngôn ngữ (VI) + gỡ '\n' thừa ở placeholder + chuẩn bị label lỗi inline
        LocalizeCheckoutUI();
        EnsureErrorLabels();
        SetupInlineErrorClearing();

        BindIconToButtonState(selectAllToCartButton, selectAllIcon);
        if (moreObject != null) moreObject.SetActive(false);
    }

    private void Update()
    {
        HandleLongPress();
    }

    private void LateUpdate()
    {
        // Click ra ngoài select zones → đóng select mode
        if (cartPanel == null || !cartPanel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.frameCount <= _ignoreOutsideClickUntilFrame) return;

        bool insideZone = false;
        if (selectZones != null)
        {
            foreach (var zone in selectZones)
            {
                if (zone == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(zone, Input.mousePosition, null))
                {
                    insideZone = true;
                    break;
                }
            }
        }

        if (!insideZone)
        {
            // Reuse nút đóng để đảm bảo đồng bộ logic tắt mode
            closeMoreButton?.onClick.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SETUP
    // ═══════════════════════════════════════════════════════════════════════

    private void SetupEventListeners()
    {
        // Refactor: RemoveAllListeners() trước AddListener — tránh duplicate handlers
        if (selectAllToCartButton != null)
        {
            selectAllToCartButton.onClick.RemoveAllListeners();
            selectAllToCartButton.onClick.AddListener(OnSelectAllToCartClicked);
        }

        if (cartButton != null)
        {
            cartButton.onClick.RemoveAllListeners();
            cartButton.onClick.AddListener(() =>
            {
                ToggleCartPanel();
                SwitchTab(InventoryTab.Unpaid);
            });
        }

        if (moreButton != null)
        {
            moreButton.onClick.RemoveAllListeners();
            moreButton.onClick.AddListener(() =>
            {
                if (moreObject != null) moreObject.SetActive(true);
                SetSelectAllButtonActive(true);
                OnSelectAllToCartClicked();
            });
        }

        if (closeMoreButton != null)
        {
            closeMoreButton.onClick.RemoveAllListeners();
            closeMoreButton.onClick.AddListener(() =>
            {
                if (moreObject != null) moreObject.SetActive(false);
                ExitSelectModeVisualOnly();
                ResetSelectAllButtonVisual();
            });
        }

        if (checkoutButton != null)
        {
            checkoutButton.onClick.RemoveAllListeners();
            checkoutButton.onClick.AddListener(InputInfomation);
        }

        if (continueShopButton != null)
        {
            continueShopButton.onClick.RemoveAllListeners();
            continueShopButton.onClick.AddListener(CloseCartPanel);
        }

        if (ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.OnCartCountChanged += UpdateCartCount;
            ShoppingCart.Instance.OnUnpaidItemsUpdated += OnUnpaidItemsUpdated;
            ShoppingCart.Instance.OnPaidItemsUpdated += OnPaidItemsUpdated;
        }

        if (codButton != null)
        {
            codButton.onClick.RemoveAllListeners();
            codButton.onClick.AddListener(() => SetPaymentMethod("COD"));
        }
        if (bankButton != null)
        {
            bankButton.onClick.RemoveAllListeners();
            bankButton.onClick.AddListener(() => SetPaymentMethod("BANK_TRANSFER"));
        }
        if (changePaymentButton != null)
        {
            changePaymentButton.onClick.RemoveAllListeners();
            changePaymentButton.onClick.AddListener(OpenPaymentMethodPanel);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyButtonClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseCheckOut);
        }
    }

    // Refactor: tách lambda buyButton thành method có tên — dễ debug stack trace
    private void OnBuyButtonClicked()
    {
        // Hiển thị lỗi ngay dưới từng ô; chỉ tiếp tục khi mọi field hợp lệ
        if (!ValidateCheckout()) return;

        PopupManager.Instance.ShowPopup(
            "Xác nhận mua",
            "Bạn có chắc chắn muốn mua vật phẩm này?",
            OnCheckoutClicked
        );
    }

    private void SetupTabSystem()
    {
        unpaidTab?.onClick.RemoveAllListeners();
        unpaidTab?.onClick.AddListener(() => SwitchTab(InventoryTab.Unpaid));
        paidTab?.onClick.RemoveAllListeners();
        paidTab?.onClick.AddListener(() => SwitchTab(InventoryTab.Paid));
        futureTab3?.onClick.RemoveAllListeners();
        futureTab3?.onClick.AddListener(() => SwitchTab(InventoryTab.Future3));
        futureTab4?.onClick.RemoveAllListeners();
        futureTab4?.onClick.AddListener(() => SwitchTab(InventoryTab.Future4));

        if (futureTab3 != null) futureTab3.interactable = false;
        if (futureTab4 != null) futureTab4.interactable = false;
    }

    private void InitializeUI()
    {
        UpdateCartCount(0);
        if (cartPanel != null) cartPanel.SetActive(false);
        SwitchTab(InventoryTab.Unpaid);
        if (moreObject != null) moreObject.SetActive(false);
        if (moreButton != null) moreButton.interactable = false;
        if (customerInfoPanel != null) customerInfoPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHECKOUT LOCALIZATION (VI) + INLINE VALIDATION
    // ═══════════════════════════════════════════════════════════════════════

    // Set placeholder tiếng Việt (đã loại bỏ ký tự '\n' thừa) + nhãn nút mua.
    private void LocalizeCheckoutUI()
    {
        SetPlaceholder(customerNameInput, placeholderName);
        SetPlaceholder(customerPhoneInput, placeholderPhone);
        SetPlaceholder(customerAddressInput, placeholderAddress);
        SetPlaceholder(customerNoteInput, placeholderNote);

        if (buyButton != null)
        {
            var label = buyButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = buyButtonLabel;
        }
    }

    private static void SetPlaceholder(TMP_InputField field, string text)
    {
        if (field == null) return;
        if (field.placeholder is TextMeshProUGUI ph)
            ph.text = text; // gán chuỗi sạch -> tự gỡ '\n' thừa ở giá trị cũ
    }

    // Clear lỗi của từng ô ngay khi người dùng gõ lại.
    private void SetupInlineErrorClearing()
    {
        // Start() chạy 1 lần/đời object nên AddListener không bị nhân đôi.
        customerNameInput?.onValueChanged.AddListener(_ => HideError(_nameError));
        customerPhoneInput?.onValueChanged.AddListener(_ => HideError(_phoneError));
        customerAddressInput?.onValueChanged.AddListener(_ => HideError(_addressError));
    }

    private void EnsureErrorLabels()
    {
        if (_errorLabelsReady) return;
        _nameError    = nameErrorText    != null ? nameErrorText    : CreateErrorLabel(customerNameInput);
        _phoneError   = phoneErrorText   != null ? phoneErrorText   : CreateErrorLabel(customerPhoneInput);
        _addressError = addressErrorText != null ? addressErrorText : CreateErrorLabel(customerAddressInput);
        _errorLabelsReady = true;
    }

    // Fallback: tạo 1 TMP label đỏ ngay dưới ô input (clone placeholder để giữ font/style).
    private TextMeshProUGUI CreateErrorLabel(TMP_InputField field)
    {
        if (field == null) return null;

        GameObject go;
        var placeholder = field.placeholder as TextMeshProUGUI;
        if (placeholder != null)
        {
            go = Instantiate(placeholder.gameObject, field.transform);
        }
        else
        {
            go = new GameObject("ErrorLabel", typeof(RectTransform));
            go.transform.SetParent(field.transform, false);
        }
        go.name = "ErrorLabel";

        var label = go.GetComponent<TextMeshProUGUI>();
        if (label == null) label = go.AddComponent<TextMeshProUGUI>();

        // Neo thành dải ngang ngay dưới đáy ô input
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-16f, 32f);
        rt.anchoredPosition = new Vector2(0f, -2f);

        label.color = errorColor;
        label.fontSize = 22f;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.richText = false;
        label.text = string.Empty;
        label.gameObject.SetActive(false);

        // Đừng để label ảnh hưởng layout của ô input
        var le = go.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

#if UNITY_EDITOR
        GameLog.Info($"[CartUI] Auto-created inline error label under '{field.name}'. Gán field trong Inspector để kiểm soát vị trí chính xác hơn.");
#endif
        return label;
    }

    // Trả về true nếu hợp lệ; ngược lại hiển thị lỗi inline. O(1) theo số field (4), không alloc đáng kể.
    private bool ValidateCheckout()
    {
        EnsureErrorLabels();
        ClearAllErrors();
        bool ok = true;

        string name = customerNameInput != null ? customerNameInput.text?.Trim() : null;
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
        {
            SetError(_nameError, "Vui lòng nhập họ tên hợp lệ.");
            ok = false;
        }

        string phone = customerPhoneInput != null ? customerPhoneInput.text : null;
        if (!IsValidVietnamPhone(phone))
        {
            SetError(_phoneError, "Số điện thoại không hợp lệ (10 số, bắt đầu bằng 0).");
            ok = false;
        }

        string address = customerAddressInput != null ? customerAddressInput.text?.Trim() : null;
        if (string.IsNullOrWhiteSpace(address) || address.Length < 5)
        {
            SetError(_addressError, "Vui lòng nhập địa chỉ giao hàng.");
            ok = false;
        }

        // Phương thức thanh toán không có ô riêng → giữ popup nhắc.
        if (string.IsNullOrWhiteSpace(_selectedPaymentMethod))
        {
            PopupManager.Instance?.ShowPopup("Thiếu thông tin", "Vui lòng chọn hình thức thanh toán.", null);
            ok = false;
        }

        return ok;
    }

    // Định dạng SĐT VN: 10 số, bắt đầu '0', số thứ 2 ∈ {3,5,7,8,9}. Chấp nhận tiền tố +84/84.
    private static bool IsValidVietnamPhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string s = raw.Trim();
        if (s.StartsWith("+")) s = s.Substring(1);

        for (int i = 0; i < s.Length; i++)
            if (!char.IsDigit(s[i])) return false; // phải toàn số

        if (s.StartsWith("84") && s.Length == 11)
            s = "0" + s.Substring(2);

        if (s.Length != 10 || s[0] != '0') return false;
        return "1235789".IndexOf(s[1]) >= 0;
    }

    private void SetError(TextMeshProUGUI label, string message)
    {
        if (label == null) return;
        label.text = message;
        label.color = errorColor;
        label.gameObject.SetActive(true);
    }

    private void ClearAllErrors()
    {
        HideError(_nameError);
        HideError(_phoneError);
        HideError(_addressError);
    }

    private static void HideError(TextMeshProUGUI label)
    {
        if (label == null) return;
        label.text = string.Empty;
        label.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SELECT MODE
    // ═══════════════════════════════════════════════════════════════════════

    private void ExitSelectModeVisualOnly()
    {
        isSelectMode = false;
        CartImageItem.ClearAllHighlights();
    }

    private void SetSelectAllButtonActive(bool active)
    {
        if (selectAllToCartButton == null) return;
        var colors = selectAllToCartButton.colors;
        selectAllToCartButton.targetGraphic.color = active ? colors.selectedColor : colors.normalColor;
    }

    private void ResetSelectAllButtonVisual()
    {
        if (selectAllToCartButton == null) return;
        selectAllToCartButton.targetGraphic.color = selectAllToCartButton.colors.normalColor;
    }

    private void OnSelectAllToCartClicked()
    {
        // Chỉ bật select mode, không toggle OFF
        isSelectMode = true;

        // Khi vào (hoặc vào lại) select mode:
        // - Đọc state isSelectedForCheckout từ ShoppingCart
        // - Highlight lại các item đã được chọn
        RefreshHighlightsFromSelection();
        UpdateTotalAmount();
    }

    private void RefreshHighlightsFromSelection()
    {
        if (_cellLookup.Count == 0) return;

        foreach (var cell in _cellLookup.Values)
        {
            if (cell == null) continue;

            var item = cell.GetCurrentItem();
            if (item == null) continue;

            bool selected = item.isSelectedForCheckout;
            cell.SetHighlightVisual(selected);
            cell.RefreshCartIndicator();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB SYSTEM
    // ═══════════════════════════════════════════════════════════════════════

    private void SwitchTab(InventoryTab tab)
    {
        _currentTab = tab;
        UpdateTabVisuals();
        RefreshCurrentTabContent();
    }

    private void UpdateTabVisuals()
    {
        SetTabColor(unpaidTab, _currentTab == InventoryTab.Unpaid);
        SetTabColor(paidTab, _currentTab == InventoryTab.Paid);
        SetTabColor(futureTab3, _currentTab == InventoryTab.Future3);
        SetTabColor(futureTab4, _currentTab == InventoryTab.Future4);

        bool showCheckout = _currentTab == InventoryTab.Unpaid;
        if (checkoutButton != null) checkoutButton.gameObject.SetActive(showCheckout);
        if (totalAmountText != null) totalAmountText.gameObject.SetActive(showCheckout);
    }

    private static void SetTabColor(Button tab, bool isActive)
    {
        if (tab == null) return;
        var colors = tab.colors;
        colors.normalColor = isActive ? Color.white : Color.gray;
        tab.colors = colors;
    }

    private void RefreshCurrentTabContent()
    {
        if (ShoppingCart.Instance == null) return;

        switch (_currentTab)
        {
            case InventoryTab.Unpaid:
                UpdateCartDisplay(ShoppingCart.Instance.GetUnpaidItems());
                break;
            case InventoryTab.Paid:
                UpdateCartDisplay(ShoppingCart.Instance.GetPaidItems());
                break;
            case InventoryTab.Future3:
                break;
            case InventoryTab.Future4:
                UpdateCartDisplay(new List<CartItem>());
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CART DISPLAY (pooling)
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateCartCount(int count)
    {
        if (cartCountText != null) cartCountText.text = count.ToString();
        if (cartCountBadge != null) cartCountBadge.SetActive(count > 0);
    }

    private void OnUnpaidItemsUpdated(List<CartItem> items)
    {
        if (_currentTab == InventoryTab.Unpaid)
        {
            RefreshCurrentTabContent();
            RefreshAllCartIndicators();
        }
    }

    private void OnPaidItemsUpdated(List<CartItem> items)
    {
        if (_currentTab == InventoryTab.Paid)
            UpdateCartDisplay(items);
    }

    private void UpdateCartDisplay(List<CartItem> items)
    {
        if (cartItemsContainer == null || cartImageItemPrefab == null) return;

        // Clear previous lookup → rebuild mapping cho current items
        _cellLookup.Clear();

        // Instantiate chỉ các cells còn thiếu (pooling)
        while (_spawnedCells.Count < items.Count)
        {
            var go = Instantiate(cartImageItemPrefab, cartItemsContainer);
            var cell = go.GetComponent<CartImageItem>();
            _spawnedCells.Add(cell);
        }

        // Configure mỗi spawned cell: activate trong items.Count, deactivate phần dư
        for (int i = 0; i < _spawnedCells.Count; i++)
        {
            var cell = _spawnedCells[i];
            if (cell == null) continue;

            bool active = i < items.Count;
            if (cell.gameObject.activeSelf != active)
                cell.gameObject.SetActive(active);

            if (!active) continue;

            var item = items[i];
            cell.Setup(item, OnItemClicked, this);

            // Tuple key (customId, selectedSize) — khớp kiểu dictionary mới
            var key = (customId: item.customId, size: item.selectedSize);
            _cellLookup[key] = cell;
        }
        UpdateTotalAmount();
    }

    // Refactor: dùng _spawnedCells thay vì foreach Transform + GetComponent<CartImageItem>()
    private void RefreshAllCartIndicators()
    {
        foreach (var cell in _spawnedCells)
        {
            if (cell != null && cell.gameObject.activeSelf)
                cell.RefreshCartIndicator();
        }
    }

    private void OnItemClicked(CartItem item)
    {
        // Guard: nếu long press vừa fire → bỏ qua click này
        if (_longPressConsumed)
        {
            _longPressConsumed = false;
            return;
        }

        _ignoreOutsideClickUntilFrame = Time.frameCount + 1;

        // isSelectMode luôn = true → chỉ còn nhánh multi-select
        if (!_cellLookup.TryGetValue((item.customId, item.selectedSize), out var clickedCell)
            || clickedCell == null) return;

        clickedCell.ToggleHighlightMultiSelect();
        bool nowHighlighted = clickedCell.IsHighlighted();

        ShoppingCart.Instance?.SelectItemForCheckout(item.customId, item.selectedSize, nowHighlighted);
        clickedCell.RefreshCartIndicator();
        UpdateTotalAmount();
    }

    private void ShowProductDetailInMainUI(CartItem item)
    {
        if (ProductDetailUI.Instance == null)
        {
            Debug.LogError("[CartUI] ProductDetailUI Instance not found!");
            return;
        }

#if UNITY_EDITOR
        GameLog.Info($"[CartUI] Opening Detail for: {item.productName} (Paid: {item.isPaid})");
#endif

        if (item.isPaid)
        {
            // CASE 1: Hàng ĐÃ MUA -> Gọi hàm hiển thị trực tiếp từ data Inventory
            ProductDetailUI.Instance.ShowPaidProductDetail(item);
        }
        else
        {
            // CASE 2: Hàng CHƯA MUA -> Gọi logic cũ (API Shop) dùng customId
            if (!string.IsNullOrEmpty(item.customId))
                ProductDetailUI.Instance.ShowUnpaidProductDetail(item.customId, item.selectedSize);
            else
                Debug.LogError("[CartUI] Unpaid item missing CustomID, cannot load shop detail.");
        }
    }

    private void UpdateTotalAmount()
    {
        if (totalAmountText == null || ShoppingCart.Instance == null) return;

        float total = ShoppingCart.Instance.TotalAmount;                 // đọc aggregate O(1)
        int count = ShoppingCart.Instance.GetSelectedCheckoutCount();    // đọc aggregate O(1)

        totalAmountText.text = $"Tổng: {count} món\n{total:N0} VND";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CART PANEL OPEN/CLOSE
    // ═══════════════════════════════════════════════════════════════════════

    private void ToggleCartPanel()
    {
        if (cartPanel == null) return;

        bool isActive = !cartPanel.activeSelf;
        cartPanel.SetActive(isActive);
        this.enabled = isActive;

        if (isActive)
        {
            isSelectMode = true; // luôn vào select mode khi mở
            RefreshCurrentTabContent();
        }

        PlayerController.Instance?.SetCanMove(!isActive);
    }

    private void CloseCartPanel()
    {
        ExitSelectModeVisualOnly();
        if (cartPanel == null) return;

        // Refactor: null check shopController trước khi access
        bool shopHidden = shopController != null && !shopController.activeInHierarchy;
        if (shopHidden && cartPanel.activeInHierarchy)
        {
            cartPanel.SetActive(false);
            PlayerController.Instance?.SetCanMove(true);
        }
        else
        {
            cartPanel.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHECKOUT FLOW
    // ═══════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════
    // BUY NOW — mở panel nhập liệu dùng chung cho "Mua ngay" (ProductDetailUI gọi)
    // ═══════════════════════════════════════════════════════════════════════
    public void OpenCheckoutForBuyNow(CartItem item)
    {
        if (item == null)
        {
            PopupManager.Instance.ShowPopup("Thông báo", "Không có vật phẩm để mua!", null, "Đóng");
            return;
        }

        _buyNowMode = true;
        _buyNowItem = item;

        // Mở panel nhập thông tin + chọn thanh toán (KHÔNG kiểm tra giỏ trống vì mua trực tiếp)
        if (customerInfoPanel != null) customerInfoPanel.SetActive(true);
        if (paymentMethodPanel != null) paymentMethodPanel.SetActive(true);
        if (changePaymentButton != null) changePaymentButton.gameObject.SetActive(false);
        _selectedPaymentMethod = "";
        if (selectedPaymentText != null) selectedPaymentText.text = "";
        ClearAllErrors();
    }

    private void InputInfomation()
    {
        _buyNowMode = false;
        _buyNowItem = null;

        if (ShoppingCart.Instance != null)
        {
            float total = ShoppingCart.Instance.TotalAmount;
            int count = ShoppingCart.Instance.GetSelectedCheckoutCount();

            if (total == 0 && count == 0)
            {
                PopupManager.Instance.ShowPopup(
                    "Thông báo",
                    "Bạn cần nhấn vào lưu vật phẩm để xác nhận trước",
                    null
                );
                return;
            }
        }

        if (customerInfoPanel != null) customerInfoPanel.SetActive(true);

        // Hiện panel chọn phương thức, ẩn nút đổi
        if (paymentMethodPanel != null) paymentMethodPanel.SetActive(true);
        if (changePaymentButton != null) changePaymentButton.gameObject.SetActive(false);
        _selectedPaymentMethod = "";
        if (selectedPaymentText != null) selectedPaymentText.text = "";

        // Mở form mới → xoá mọi lỗi inline cũ
        ClearAllErrors();
    }

    private void CloseCheckOut()
    {
        if (customerInfoPanel != null) customerInfoPanel.SetActive(false);
        _buyNowMode = false;
        _buyNowItem = null;
    }

    private void SetPaymentMethod(string method)
    {
        _selectedPaymentMethod = method;
        if (selectedPaymentText != null)
            selectedPaymentText.text = method == "COD" ? "COD" : "Bank";

        // Ẩn panel chọn, hiện nút đổi
        if (paymentMethodPanel != null) paymentMethodPanel.SetActive(false);
        if (changePaymentButton != null) changePaymentButton.gameObject.SetActive(true);
    }

    private void OpenPaymentMethodPanel()
    {
        if (paymentMethodPanel != null) paymentMethodPanel.SetActive(true);
        if (changePaymentButton != null) changePaymentButton.gameObject.SetActive(false);
        UpdatePaymentButtonVisuals();
    }

    private void UpdatePaymentButtonVisuals()
    {
        SetPaymentButtonColor(codButton, _selectedPaymentMethod == "COD");
        SetPaymentButtonColor(bankButton, _selectedPaymentMethod == "BANK_TRANSFER");
    }

    private static void SetPaymentButtonColor(Button btn, bool isSelected)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = isSelected ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
    }

    private void OnCheckoutClicked()
    {
        if (ShoppingCart.Instance != null)
        {
            if (_buyNowMode)
            {
                // Mua ngay: chỉ thanh toán đúng item đang xem, không đụng giỏ
                ShoppingCart.Instance.ProcessBuyNow(_buyNowItem, _selectedPaymentMethod);
            }
            else if (_currentTab == InventoryTab.Unpaid)
            {
                ShoppingCart.Instance.ProcessCheckout(_selectedPaymentMethod);
            }
        }

        // Reset state sau khi đặt đơn
        _buyNowMode = false;
        _buyNowItem = null;

        if (customerInfoPanel != null) customerInfoPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ICON BINDING (EventTrigger)
    // ═══════════════════════════════════════════════════════════════════════

    private void BindIconToButtonState(Button button, GameObject icon)
    {
        if (button == null || icon == null) return;

        icon.SetActive(false);

        var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        System.Func<bool> isSelected = () =>
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;

        AddEvent(trigger, EventTriggerType.PointerDown, _ => icon.SetActive(true));
        AddEvent(trigger, EventTriggerType.PointerUp,   _ => icon.SetActive(isSelected()));
        AddEvent(trigger, EventTriggerType.PointerExit, _ => icon.SetActive(isSelected()));
        AddEvent(trigger, EventTriggerType.Select,      _ => icon.SetActive(true));
        AddEvent(trigger, EventTriggerType.Deselect,    _ => icon.SetActive(false));
    }

    private static void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
