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
    [SerializeField] private Button addSelectedToCartButton;

    [Header("Button State Icons")]
    [SerializeField] private GameObject selectAllButtonIcon;
    [SerializeField] private GameObject addSelectedButtonIcon;

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
    [SerializeField] private RectTransform selectZone;
    [SerializeField] public bool isSelectMode = false;     // 🆕 THÊM: track chế độ chọn thủ công

    private void Start()
    {
        SetupEventListeners();
        SetupTabSystem();
        InitializeUI();

        BindIconToButtonState(selectAllToCartButton, selectAllButtonIcon);
        BindIconToButtonState(addSelectedToCartButton, addSelectedButtonIcon);
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

        if (!isSelectMode) return; // ← early exit, không làm gì khi tắt mode
        if (!cartPanel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // Kiểm tra click có nằm trong vùng selectZone không
        bool insideZone = selectZone != null &&
                          RectTransformUtility.RectangleContainsScreenPoint(
                              selectZone,
                              Input.mousePosition,
                              null // null = Screen Space Overlay, truyền Camera nếu dùng Screen Space Camera
                          );

        if (!insideZone)
        {
            // Click ra ngoài vùng → tắt mode, clear hết
            isSelectMode = false;
            CartImageItem.ClearAllHighlights();
        }
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

        addSelectedToCartButton?.onClick.AddListener(()=>
        {
            OnAddSelectedToCartClicked();
        });
      
        cartButton?.onClick.AddListener(() =>
        {
            ToggleCartPanel();
            SwitchTab(InventoryTab.Unpaid);
        });
        moreButton?.onClick.AddListener(() =>
        {
            if (moreObject != null)
                moreObject.SetActive(!moreObject.activeSelf);
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

        buyButton?.onClick.AddListener(()=> {

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

    /*  private bool allSelectedCache = false;
      private void OnSelectAllToCartClicked()
      {
          if (ShoppingCart.Instance == null) return;
          allSelectedCache = !allSelectedCache;
          ShoppingCart.Instance.SelectAllUnpaidItems(allSelectedCache);
          UpdateTotalAmount();
      }*/

    // SAU (mới):

    private void OnSelectAllToCartClicked()
    {
        isSelectMode = !isSelectMode;

        if (!isSelectMode)
        {
            CartImageItem.ClearAllHighlights();
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

        foreach (var item in items)
        {
            var itemGO = Instantiate(cartImageItemPrefab, cartItemsContainer);
            // Giả định tên class là CartImageItemUI dựa trên ngữ cảnh
            var imageItemUI = itemGO.GetComponent<CartImageItem>();
            if (imageItemUI != null)
            {
                imageItemUI.Setup(item, OnItemClicked);
                imageItemUI.RefreshCartIndicator();
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
        float currentTime = Time.time;
        selectedItem = item;
        // 🆕 Nếu đang trong chế độ multi-select
        if (isSelectMode)
        {
            // Tìm CartImageItem tương ứng và toggle highlight (multi, không tắt item khác)
            foreach (Transform child in cartItemsContainer)
            {
                var cell = child.GetComponent<CartImageItem>();
                if (cell != null && cell.GetCurrentItem() == item)
                {
                    cell.ToggleHighlightMultiSelect(); // ← dùng method mới, không clear item khác
                    break;
                }
            }
            // Reset double-click để không vô tình mở detail
            lastClickedItem = null;
            return; // Không chạy logic double-click bên dưới
        }
        // Logic click: 1 click để chọn, 2 click để xem chi tiết
        if (lastClickedItem == item && (currentTime - lastClickTime) < doubleClickThreshold)
        {
            // Double click - Mở ProductDetailUI thay vì panel cũ
            ShowProductDetailInMainUI(item);
            lastClickedItem = null;
        }
        else
        {
            // Single click - Chỉ chọn item
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

    private void OnAddSelectedToCartClicked()
    {
        /* if (selectedItem == null || ShoppingCart.Instance == null) return;
         ShoppingCart.Instance.SelectItemForCheckout(selectedItem.productId, selectedItem.selectedSize, true);
         UpdateTotalAmount();
         RefreshAllCartIndicators();*/   

        if (selectedItem == null || ShoppingCart.Instance == null) return;
        ShoppingCart.Instance.SelectItemForCheckout(selectedItem.productId, selectedItem.selectedSize, true);
        UpdateTotalAmount();
        RefreshAllCartIndicators();
    }

    private void UpdateTotalAmount()
    {
        if (totalAmountText != null && ShoppingCart.Instance != null)
        {
            float total = ShoppingCart.Instance.TotalAmount;
            var select = ShoppingCart.Instance.GetUnpaidItems().FindAll(i => i.isSelectedForCheckout);
            totalAmountText.text = $"Tổng: {select.Count:0 món} \n {total:N0} VND";
        }
    }

    private void ToggleCartPanel()
    {
        if (cartPanel != null)
        {
            bool isActive = !cartPanel.activeSelf;
            cartPanel.SetActive(isActive);
            if (isActive) RefreshCurrentTabContent();
            PlayerController.Instance?.SetCanMove(!isActive);
        }
    }

    private void CloseCartPanel()
    {
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
        if (customerInfoPanel != null)
            customerInfoPanel.SetActive(true);
        // Hiện panel chọn phương thức, ẩn nút đổi
        if (paymentMethodPanel != null) paymentMethodPanel.SetActive(true);
        if (changePaymentButton != null) changePaymentButton.gameObject.SetActive(false);
        selectedPaymentMethod = "COD";
        if (selectedPaymentText != null) selectedPaymentText.text = "";
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

        System.Func<bool> isSelected = () =>
            EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == button.gameObject;

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


