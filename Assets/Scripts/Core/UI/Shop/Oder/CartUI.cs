using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CartUI : MonoBehaviour 
{
    [SerializeField] private GameObject moreObject;
    [SerializeField] private Button moreButton;
    [SerializeField] private Button selectAllToCartButton;
    //[SerializeField] private Button addSelectedToCartButton;
    [SerializeField] private Button closeMoreButton;

    [Header("Button State Icons")]
    [SerializeField] private GameObject selectAllIcon;
    //[SerializeField] private GameObject addSelectedIcon;

    [Header("Cart Button")]
    [SerializeField] private Button cartButton;
    [SerializeField] private TextMeshProUGUI cartCountText;
    [SerializeField] private GameObject cartCountBadge;

    [Header("Cart Panel")]
    [SerializeField] private GameObject cartPanel;
    [SerializeField] private Transform cartItemsContainer;
    // ĐÃ XÓA: cartItemPrefab (theo yêu cầu)
    [SerializeField] private TextMeshProUGUI totalAmountText;
    [SerializeField] private Button checkoutButton;
    [SerializeField] private Button continueShopButton;

    [Header("Customer Info")]
    public TMP_InputField customerNameInput;
    public TMP_InputField customerPhoneInput;
    public TMP_InputField customerAddressInput;
    public TMP_InputField customerNoteInput;
    public GameObject customerInfoPanel;
    public Button buyButton;
    public Button backButton;

    [Header("Payment Method")]
    public GameObject paymentMethodPanel;   // Object chứa codButton + bankButton
    public Button codButton;                // Thanh toán khi nhận hàng
    public Button bankButton;               // Chuyển khoản ngân hàng
    public Button changePaymentButton;      // Button nằm trong selectedPaymentText để mở lại panel
    public TextMeshProUGUI selectedPaymentText; // Hiển thị phương thức đang chọn
    private string selectedPaymentMethod = "COD";

    [Header("Inventory Tabs")]
    [SerializeField] private Button unpaidTab; // Tab 1: Chưa thanh toán
    [SerializeField] private Button paidTab; // Tab 2: Đã thanh toán
    [SerializeField] private Button futureTab3; // Tab 3: Tương lai
    [SerializeField] private Button futureTab4; // Tab 4: Tương lai

    private enum InventoryTab { Unpaid, Paid, Future3, Future4 }
    private InventoryTab currentTab = InventoryTab.Unpaid;
    private CartItem selectedItem = null;

    private CartItem lastClickedItem = null;
    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 1f;

    [Header("Image Grid System")]
    [SerializeField] private GameObject cartImageItemPrefab; // Prefab cho image items (Grid)
    // ĐÃ XÓA: detailPanel và closeDetailButton vì không còn dùng workflow hiển thị isSelectMode 

    [Header("Auto Refresh")]
    public bool autoUpdateTotalAmount = true;
    public GameObject shopController; // Reference đến ShopController để gọi API khi cần

    [Header("Select Mode")]
    [SerializeField] private List<RectTransform> selectZones = new List<RectTransform>(); 
    [SerializeField] public bool isSelectMode = false;     // 🆕 THÊM: track chế độ chọn thủ công
    [SerializeField] private int number;

    private readonly Dictionary<(string productId, string size), CartImageItem> _cellLookup
    = new Dictionary<(string productId, string size), CartImageItem>();

    private int ignoreOutsideClickUntilFrame = -1;
    private void Start()
    {
        SetupEventListeners();
        SetupTabSystem();
        InitializeUI();

        BindIconToButtonState(selectAllToCartButton, selectAllIcon);
        //BindIconToButtonState(addSelectedToCartButton, addSelectedIcon);
    }

    // Trong CartUI.cs - Thêm vào cuối Update() hoặc LateUpdate()
    private void LateUpdate()
    {
        /*// Chỉ chạy khi panel active
        if (cartPanel != null && cartPanel.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!IsPointerOverCartItem())
            {
                CartImageItem.ClearAllHighlights();

            }
        }*/

        if (!cartPanel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0)) return;

        if (Time.frameCount <= ignoreOutsideClickUntilFrame) return;

        bool insideZone = false;

        if (selectZones != null)
        {
            foreach (var zone in selectZones)
            {
                if (zone == null) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(
                        zone,
                        Input.mousePosition,
                        null)) // Screen Space Overlay => cam = null
                {
                    insideZone = true;
                    break; // chỉ cần trúng 1 vùng là đủ
                }
            }
        }

        if (!insideZone)
        {
            // Click ra ngoài vùng → tắt mode, clear hết
           closeMoreButton.onClick.Invoke(); // Reuse nút đóng để đảm bảo đồng bộ logic tắt mode
        }
    }

    private void ExitSelectModeVisualOnly()
    {
        isSelectMode = false;
        CartImageItem.ClearAllHighlights();
    }

    private bool IsPointerOverCartItem()
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject.GetComponent<CartImageItem>() != null)
                return true;
        }

        return false;


    }


    private void SetupEventListeners()
    {
        selectAllToCartButton?.onClick.AddListener(()=> 
        {
            OnSelectAllToCartClicked();
        });

       /* addSelectedToCartButton?.onClick.AddListener(() =>
        {
            OnAddSelectedToCartClicked();
        });*/

        cartButton?.onClick.AddListener(() =>
        {
            ToggleCartPanel();
            SwitchTab(InventoryTab.Unpaid);
        });
        moreButton?.onClick.AddListener(() =>
        {
            if (moreObject != null)
                moreObject.SetActive(true);
            // Gọi logic trực tiếp thay vì Invoke()
            SetSelectAllButtonActive(true);
            OnSelectAllToCartClicked();
        });
        closeMoreButton?.onClick.AddListener(() =>
        {
            if (moreObject != null)
                moreObject.SetActive(false);
            ExitSelectModeVisualOnly();
            ResetSelectAllButtonVisual();
        });
        checkoutButton?.onClick.AddListener(InputInfomation);
        continueShopButton?.onClick.AddListener(CloseCartPanel);
        if (ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.OnCartCountChanged += UpdateCartCount;
            ShoppingCart.Instance.OnUnpaidItemsUpdated += OnUnpaidItemsUpdated;
            ShoppingCart.Instance.OnPaidItemsUpdated += OnPaidItemsUpdated;
        }
        codButton?.onClick.AddListener(() => SetPaymentMethod("COD"));
        bankButton?.onClick.AddListener(() => SetPaymentMethod("BANK_TRANSFER"));
        changePaymentButton?.onClick.AddListener(OpenPaymentMethodPanel);

        buyButton?.onClick.AddListener(()=> 
        {
            string error = ValidateCheckout();
            if (error != null)
            {
                PopupManager.Instance.ShowPopup("Thông tin chưa hợp lệ", error, null);
                return;
            }

            // Gọi Popup thay vì gọi hàm mua
            PopupManager.Instance.ShowPopup(
                "Xác nhận mua",
                "Bạn có chắc chắn muốn mua vật phẩm này?",
                () => {
                    // Khi bấm "Đồng ý" trên Popup thì mới chạy hàm này
                    OnCheckoutClicked();
                }
            );
            
        });
        backButton?.onClick.AddListener(() =>
        {
            CloseCheckOut();
        });
    }

    private void SetSelectAllButtonActive(bool active)
    {
        var colors = selectAllToCartButton.colors;
        selectAllToCartButton.targetGraphic.color = active
            ? colors.selectedColor  // hoặc colors.pressedColor tùy visual bạn muốn
            : colors.normalColor;
    }

    private void ResetSelectAllButtonVisual()
    {
        if (selectAllToCartButton == null) return;
        selectAllToCartButton.targetGraphic.color = selectAllToCartButton.colors.normalColor;
    }

    private void OnSelectAllToCartClicked()
    {

        /*  isSelectMode = !isSelectMode;
          selectedItem = null;
          CartImageItem.ClearAllHighlights();

          if (isSelectMode)
              ShoppingCart.Instance?.ClearCheckoutSelection(); // reset state cũ khi bắt đầu lượt mới*/

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
        if (_cellLookup == null || _cellLookup.Count == 0) return;

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

    private void SetupTabSystem()
    {
        unpaidTab?.onClick.AddListener(() => SwitchTab(InventoryTab.Unpaid));
        paidTab?.onClick.AddListener(() => SwitchTab(InventoryTab.Paid));
        futureTab3?.onClick.AddListener(() => SwitchTab(InventoryTab.Future3));
        futureTab4?.onClick.AddListener(() => SwitchTab(InventoryTab.Future4));

        if (futureTab3 != null) futureTab3.interactable = false;
        if (futureTab4 != null) futureTab4.interactable = false;
    }

    private void SwitchTab(InventoryTab tab)
    {
        currentTab = tab;
        selectedItem = null;
        UpdateTabVisuals();
        RefreshCurrentTabContent();
    }

    private void UpdateTabVisuals()
    {
        SetTabColor(unpaidTab, currentTab == InventoryTab.Unpaid);
        SetTabColor(paidTab, currentTab == InventoryTab.Paid);
        SetTabColor(futureTab3, currentTab == InventoryTab.Future3);
        SetTabColor(futureTab4, currentTab == InventoryTab.Future4);

        bool showCheckout = currentTab == InventoryTab.Unpaid;
        if (checkoutButton != null) checkoutButton.gameObject.SetActive(showCheckout);
        if (totalAmountText != null) totalAmountText.gameObject.SetActive(showCheckout);
    }

    private void SetTabColor(Button tab, bool isActive)
    {
        if (tab == null) return;
        var colors = tab.colors;
        colors.normalColor = isActive ? Color.white : Color.gray; // Điều chỉnh màu tùy ý
        tab.colors = colors;
    }

    private void RefreshCurrentTabContent()
    {
        switch (currentTab)
        {
            case InventoryTab.Unpaid:
                if (ShoppingCart.Instance != null)
                    UpdateCartDisplay(ShoppingCart.Instance.GetUnpaidItems());
                break;
            case InventoryTab.Paid:
                if (ShoppingCart.Instance != null)
                    UpdateCartDisplay(ShoppingCart.Instance.GetPaidItems());
                break;
            case InventoryTab.Future3:
                break;
            case InventoryTab.Future4:
                UpdateCartDisplay(new List<CartItem>());
                break;
        }
    }

    private void InitializeUI()
    {
        UpdateCartCount(0);
        if (cartPanel != null) cartPanel.SetActive(false);
        SwitchTab(InventoryTab.Unpaid);
        if (moreObject != null) moreObject.SetActive(false);
        if (customerInfoPanel != null) customerInfoPanel.SetActive(false);
    }

    private void UpdateCartCount(int count)
    {
        if (cartCountText != null) cartCountText.text = count.ToString();
        if (cartCountBadge != null) cartCountBadge.SetActive(count > 0);
    }

    private void OnUnpaidItemsUpdated(List<CartItem> items)
    {
        if (currentTab == InventoryTab.Unpaid)
        {
            RefreshCurrentTabContent();
            RefreshAllCartIndicators();
        }
    }

    private void OnPaidItemsUpdated(List<CartItem> items)
    {
        if (currentTab == InventoryTab.Paid)
            UpdateCartDisplay(items);
    }

    private void UpdateCartDisplay(List<CartItem> items)
    {
        if (cartItemsContainer == null || cartImageItemPrefab == null) return;

        foreach (Transform child in cartItemsContainer)
            Destroy(child.gameObject);

        _cellLookup.Clear();

        foreach (var item in items)
        {
            var itemGO = Instantiate(cartImageItemPrefab, cartItemsContainer);
            // Giả định tên class là CartImageItemUI dựa trên ngữ cảnh
            var imageItemUI = itemGO.GetComponent<CartImageItem>();
            if (imageItemUI != null)
            {
                imageItemUI.Setup(item, OnItemClicked);
                imageItemUI.RefreshCartIndicator();

                var key = (item.productId, item.selectedSize);
                _cellLookup[key] = imageItemUI;
            }
        }
        UpdateTotalAmount();
    }

    private void RefreshAllCartIndicators()
    {
        foreach (Transform child in cartItemsContainer)
        {
            var cell = child.GetComponent<CartImageItem>();
            if (cell != null) cell.RefreshCartIndicator();
        }
    }

    private void OnItemClicked(CartItem item)
    {
        ignoreOutsideClickUntilFrame = Time.frameCount + 1;
        float currentTime = Time.time;

        // 🆕 Nhánh multi-select tối ưu
        if (isSelectMode)
        {
            // Lookup cell O(1) bằng (productId, size)
            CartImageItem clickedCell = null;
            if (!_cellLookup.TryGetValue((item.productId, item.selectedSize), out clickedCell) || clickedCell == null)
            {
                // Fallback an toàn: nếu vì lý do gì đó dictionary chưa sync, có thể
                // (optionally) fallback sang loop nếu bạn muốn.
                return;
            }

            // Toggle highlight (multi-select, không ảnh hưởng item khác)
            clickedCell.ToggleHighlightMultiSelect();
            bool nowHighlighted = clickedCell.IsHighlighted();

            // Đồng bộ state xuống ShoppingCart (isSelectedForCheckout)
            if (ShoppingCart.Instance != null)
            {
                ShoppingCart.Instance.SelectItemForCheckout(
                    item.productId,
                    item.selectedSize,
                    nowHighlighted
                );
            }

            // Cập nhật overlay/icon "đã chọn"
            clickedCell.RefreshCartIndicator();

            // Tổng tiền & số món sẽ dùng currentSelectedTotal (ở bước 2)
            UpdateTotalAmount();

            lastClickedItem = null;
            return;
        }

        // ==== Phần dưới: single-select + double-click mở detail giữ nguyên ====

        selectedItem = item;

        foreach (Transform child in cartItemsContainer)
        {
            var cell = child.GetComponent<CartImageItem>();
            if (cell != null && cell.GetCurrentItem() == item)
            {
                cell.SelectThisItem();
                break;
            }
        }

        if (lastClickedItem == item && (currentTime - lastClickTime) < doubleClickThreshold)
        {
            ShowProductDetailInMainUI(item);
            lastClickedItem = null;
        }
        else
        {
            lastClickedItem = item;
            lastClickTime = currentTime;
        }
    }

    // ✅ MỚI: Gọi ProductDetailUI để hiển thị thông tin
    private void ShowProductDetailInMainUI(CartItem item)
    {
        if (ProductDetailUI.Instance == null)
        {
            Debug.LogError("ProductDetailUI Instance not found!");
            return;
        }

        Debug.Log($"Opening Detail for: {item.productName} (Paid: {item.isPaid})");

        if (item.isPaid)
        {
            // CASE 1: Hàng ĐÃ MUA -> Gọi hàm hiển thị trực tiếp từ data Inventory
            ProductDetailUI.Instance.ShowPaidProductDetail(item);
        }
        else
        {
            // CASE 2: Hàng CHƯA MUA -> Gọi logic cũ (API Shop) dùng customId
            if (!string.IsNullOrEmpty(item.customId))
            {
                ProductDetailUI.Instance.ShowUnpaidProductDetail(item.customId, item.selectedSize);
            }
            else
            {
                Debug.LogError("Unpaid item missing CustomID, cannot load shop detail.");
            }
        }
    }

    private void UpdateTotalAmount()
    {
        if (totalAmountText == null || ShoppingCart.Instance == null) return;

        float total = ShoppingCart.Instance.TotalAmount;                 // đọc aggregate O(1)
        int count = ShoppingCart.Instance.GetSelectedCheckoutCount();    // đọc aggregate O(1)

        totalAmountText.text = $"Tổng: {count} món\n{total:N0} VND";
    }

    private void ToggleCartPanel()
    {
        if (cartPanel != null)
        {
            bool isActive = !cartPanel.activeSelf;
            cartPanel.SetActive(isActive);
            this.enabled = isActive;
            if (isActive) RefreshCurrentTabContent();
            PlayerController.Instance?.SetCanMove(!isActive);
        }
    }

    private void CloseCartPanel()
    {
        ExitSelectModeVisualOnly();
        if (!shopController.activeInHierarchy && cartPanel.activeInHierarchy)
        {
            cartPanel.SetActive(false);
            PlayerController.Instance?.SetCanMove(true);
        }
        else
        {
            cartPanel.SetActive(false);
        }
  
    }   

    private void InputInfomation()
    {
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
                return; // ← Dừng lại, không mở panel
            }
        }

        if (customerInfoPanel != null)
            customerInfoPanel.SetActive(true);
        // Hiện panel chọn phương thức, ẩn nút đổi
        if (paymentMethodPanel != null) paymentMethodPanel.SetActive(true);
        if (changePaymentButton != null) changePaymentButton.gameObject.SetActive(false);
        selectedPaymentMethod = "";
        if (selectedPaymentText != null) selectedPaymentText.text = "";
    }

    private string ValidateCheckout()
    {
        if (string.IsNullOrWhiteSpace(customerNameInput?.text)) return "Vui lòng nhập họ tên.";
        if (string.IsNullOrWhiteSpace(customerPhoneInput?.text)) return "Vui lòng nhập số điện thoại.";
        if (customerPhoneInput.text.Trim().Length < 10) return "Số điện thoại phải có ít nhất 10 số.";
        if (string.IsNullOrWhiteSpace(customerAddressInput?.text)) return "Vui lòng nhập địa chỉ giao hàng.";
        if (string.IsNullOrWhiteSpace(selectedPaymentMethod)) return "Vui lòng chọn hình thức thanh toán.";
        return null;
    }

    private void CloseCheckOut()
    {
        if (customerInfoPanel != null)
            customerInfoPanel.SetActive(false);
    }


    private void SetPaymentMethod(string method)
    {
        selectedPaymentMethod = method;
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
        SetPaymentButtonColor(codButton, selectedPaymentMethod == "COD");
        SetPaymentButtonColor(bankButton, selectedPaymentMethod == "BANK_TRANSFER");
    }

    private void SetPaymentButtonColor(Button btn, bool isSelected)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = isSelected ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
    }

    private void OnCheckoutClicked()
    {
        if (currentTab == InventoryTab.Unpaid && ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.ProcessCheckout(selectedPaymentMethod);
        }
        customerInfoPanel.SetActive(false);
    }

    private void BindIconToButtonState(Button button, GameObject icon)
    {
        if (button == null || icon == null) return;

        icon.SetActive(false);

        var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        System.Func<bool> isSelected = () => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;

        AddEvent(trigger, EventTriggerType.PointerDown, _ => icon.SetActive(true));
        AddEvent(trigger, EventTriggerType.PointerUp, _ => icon.SetActive(isSelected()));
        AddEvent(trigger, EventTriggerType.PointerExit, _ => icon.SetActive(isSelected()));
        AddEvent(trigger, EventTriggerType.Select, _ => icon.SetActive(true));
        AddEvent(trigger, EventTriggerType.Deselect, _ => icon.SetActive(false));
    }

    private void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

}


