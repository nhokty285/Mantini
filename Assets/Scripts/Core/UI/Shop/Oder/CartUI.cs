using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using static MainMenuViewModel;

public class CartUI : MonoBehaviour
{
    [SerializeField] private GameObject moreObject;
    [SerializeField] private Button moreButton;
    [SerializeField] private Button selectAllToCartButton;
    [SerializeField] private Button addSelectedToCartButton;

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
    // ĐÃ XÓA: detailPanel và closeDetailButton vì không còn dùng workflow hiển thị cũ

    [Header("Auto Refresh")]
    public bool autoUpdateTotalAmount = true;

    private void Start()
    {
        SetupEventListeners();
        SetupTabSystem();
        InitializeUI();

    }

    // Trong CartUI.cs - Thêm vào cuối Update() hoặc LateUpdate()
    private void LateUpdate()
    {
        // Chỉ chạy khi panel active
        if (cartPanel != null && cartPanel.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!IsPointerOverCartItem())
            {
                CartImageItem.ClearAllHighlights();
            }
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
        addSelectedToCartButton?.onClick.AddListener(OnAddSelectedToCartClicked);
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

    private bool allSelectedCache = false;
    private void OnSelectAllToCartClicked()
    {
        if (ShoppingCart.Instance == null) return;
        allSelectedCache = !allSelectedCache;
        ShoppingCart.Instance.SelectAllUnpaidItems(allSelectedCache);
        UpdateTotalAmount();
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
        Debug.Log($"Opening Product Detail for: {item.productName}");
        if (ProductDetailUI.Instance != null)
        {
            // Tạo APIProductItem giả lập chỉ chứa customId để ProductDetailUI fetch dữ liệu đầy đủ
            APIProductItem apiItem = new APIProductItem();
            apiItem.customId = item.customId;

            ProductDetailUI.Instance.ShowProductDetail(apiItem);
        }
        else
        {
            Debug.LogError("ProductDetailUI Instance not found!");
        }

        Debug.Log($"Opening Detail for: {item.productName} (Paid: {item.isPaid})");

        if (ProductDetailUI.Instance == null) return;

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
        }
    }

    private void CloseCartPanel()
    {
        if (cartPanel != null) cartPanel.SetActive(false);
    }   

    private void InputInfomation()
    {
        if (customerInfoPanel != null)
            customerInfoPanel.SetActive(true);
    }
    private void CloseCheckOut()
    {
        if (customerInfoPanel != null)
            customerInfoPanel.SetActive(false);
    }
    private void OnCheckoutClicked()
    {
        if (currentTab == InventoryTab.Unpaid && ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.ProcessCheckout();
        }
        customerInfoPanel.SetActive(false);
    }
}


