/*using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel; // Để dùng APIProductItem

// 1. Adapter Class: Chứa dữ liệu chuẩn hóa để hiển thị lên UI
[System.Serializable]
public class ProductDetail
{
    // Thông tin hiển thị cơ bản
    public string title;
    public string brandName;
    public float price;
    public float originalPrice; // Giá gốc (để gạch ngang nếu có giảm giá)
    public string description;
    public float reviewScore;
    public int reviewCount;
    public string mainImageUrl;
    public List<string> galleryUrls = new List<string>();

    // Logic logic
    public bool isPaidItem;       // Cờ đánh dấu hàng đã mua
    public string selectedSize;   // Size đã chọn (nếu là hàng đã mua)
    public string customId;       // ID sản phẩm cha (nếu có)

    // Dữ liệu gốc (chỉ dùng khi cần logic sâu hơn)
    public APIProductItem originalShopItem;


    // Constructor 1: Tạo từ API Shop (Hàng chưa mua)
    public ProductDetail(APIProductItem shopItem)
    {
        title = shopItem.title;
        brandName = shopItem.brandName;
        price = shopItem.price;
        originalPrice = shopItem.regularPrice;
        selectedSize = shopItem.selectSize;

        // Tạo mô tả
        description = $"Product ID: {shopItem.customId}\n";
        description += $"Brand: {shopItem.brandName}\n";
        description += $"Reviews: {shopItem.totalReviews} customers rated {shopItem.reviewStatFiveScale}★";

        reviewScore = shopItem.reviewStatFiveScale;
        reviewCount = shopItem.totalReviews;

        // Xử lý ảnh
        if (shopItem.images != null && shopItem.images.Count > 0)
        {
            mainImageUrl = shopItem.images[0].origin;
            foreach (var img in shopItem.images)
            {
                if (!string.IsNullOrEmpty(img.origin))
                    galleryUrls.Add(img.origin);
            }
        }

        isPaidItem = false;
        customId = shopItem.customId;
        originalShopItem = shopItem;
    }

    // Constructor 2: Tạo từ Inventory (Hàng đã mua)
    public ProductDetail(CartItem paidItem)
    {
        title = paidItem.productName;
        price = paidItem.price;
        originalPrice = 0;

        // ✅ TẬN DỤNG DỮ LIỆU CÓ SẴN TỪ CARTITEM
        // Không cần parse chuỗi description nữa vì CartItem đã lưu sẵn
        brandName = !string.IsNullOrEmpty(paidItem.brandName) ? paidItem.brandName : "Unknown Brand";
        selectedSize = !string.IsNullOrEmpty(paidItem.selectedSize) ? paidItem.selectedSize : "Freesize";

        // Tạo nội dung hiển thị text mô tả
        description = $"<color=green><b>ĐÃ SỞ HỮU</b></color>\n"; // Thêm màu cho nổi bật
        description += $"----------------------\n";
        description += $"<b>Thương hiệu:</b> {brandName}\n";
        description += $"<b>Kích thước:</b> {selectedSize}\n";

        // Nếu có description gốc từ backend (ví dụ: "Jeep - Size 36"), có thể hiển thị thêm nếu muốn
        // description += $"\nChi tiết: {paidItem.description}"; 

        if (paidItem.purchaseDate != default(System.DateTime))
            description += $"<b>Ngày mua:</b> {paidItem.purchaseDate:dd/MM/yyyy}\n";

        // Các thông số hiển thị khác
        reviewScore = 5; // Mặc định 5 sao cho hàng mình đã mua ^^
        reviewCount = 1;

        mainImageUrl = paidItem.imageUrl;
        if (!string.IsNullOrEmpty(paidItem.imageUrl))
            galleryUrls.Add(paidItem.imageUrl);

        isPaidItem = true;
        customId = paidItem.customId;
        originalShopItem = null;
    }
}

public class ProductDetailUI : MonoBehaviour
{
    public static ProductDetailUI Instance { get; private set; }

    [Header("Product Detail UI")]
    [SerializeField] private GameObject productDetailPanel;
    [SerializeField] private Image productMainImage;
    [SerializeField] private TextMeshProUGUI productNameText;
    [SerializeField] private TextMeshProUGUI productBrandText;
    [SerializeField] private TextMeshProUGUI productPriceText;
    [SerializeField] private TextMeshProUGUI productOriginalPriceText;
    [SerializeField] private TextMeshProUGUI productDescriptionText;
    [SerializeField] private TextMeshProUGUI productReviewsText;

    [Header("Size Selection")]
    [SerializeField] private TMP_Dropdown sizeDropdown;
    [SerializeField] private TextMeshProUGUI selectedSizeText;

    [Header("Buttons")]
    [SerializeField] private Button addToCartButton;
    [SerializeField] private Button buyNowButton;
    [SerializeField] private Button closeDetailButton;
    [SerializeField] private Button deleteButton;

    [Header("Image Gallery")]
    [SerializeField] private Transform imageScrollContent;
    [SerializeField] private GameObject imagePagePrefab;
    [SerializeField] private float snapSpeed = 10f;
    [SerializeField] private ScrollRect imageScrollRect;

    // State
    private List<GameObject> imagePages = new List<GameObject>();
    private ProductDetail currentDetail;
    private CartItem currentCartItem; // Reference để delete
    private string currentSelectedSize = "";
    private string currentVariantId = "";
    private string _lastSelectedSize = "";
    [SerializeField] private CarouselIndicator carouselIndicator;

    [Header("Chat Integration")]
    [SerializeField] private RectTransform chatAnchor;

    private MultiChatManager _chatManager;

    private Coroutine _mainImageCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _chatManager = FindAnyObjectByType<MultiChatManager>();
        InitializeUI();
    }

    private void InitializeUI()
    {
        //addToCartButton?.onClick.AddListener(OnAddToCartClicked);
        addToCartButton.onClick.AddListener(() =>
        {
            // Gọi Popup thay vì gọi hàm mua
            PopupManager.Instance.ShowPopup(
                "Xác nhận",
                "Bạn có muốn thêm vật phẩm này vào giỏ hàng không?",
                () =>
                {
                    // Khi bấm "Đồng ý" trên Popup thì mới chạy hàm này
                    OnAddToCartClicked();
                    TutorialGamePlay.Instance?.OnAddToCartSuccess();
                }
            );
        });
        closeDetailButton?.onClick.AddListener(CloseProductDetail);
        sizeDropdown.onValueChanged.RemoveAllListeners();
        sizeDropdown.onValueChanged.AddListener(OnSizeChanged);

        if (productDetailPanel != null) productDetailPanel.SetActive(false);
    }

    // =================================================================================
    // PUBLIC API: ENTRY POINTS
    // =================================================================================

    // 1. Gọi khi xem hàng đã mua (Từ Inventory) - KHÔNG GỌI API SHOP
    public void ShowPaidProductDetail(CartItem paidItem)
    {
        Debug.Log($"[ProductDetail] Showing PAID item: {paidItem.productName}");

        // Convert CartItem -> ProductDetail
        currentDetail = new ProductDetail(paidItem);

        OpenPanel();
        PopulateCommonUI();

        // Setup UI riêng cho hàng đã mua (Read-only)
        SetupPaidItemUI();
    }

    // 2. Gọi khi xem hàng chưa mua (Từ Shop) - CÓ GỌI API SHOP
    public void ShowUnpaidProductDetail(string customId, string preSelectedSize = "")
    {
        Debug.Log($"[ProductDetail] Fetching shop item: {customId}");
        OpenPanel();
        _lastSelectedSize = preSelectedSize;

        // Lưu CartItem để dùng cho nút Delete
        currentCartItem = ShoppingCart.Instance?.GetUnpaidItems()
            .Find(i => i.customId == customId && i.selectedSize == preSelectedSize);
        string detailUrl = $"https://data.storims.c1.hubcom.tech/api/v1/TenantProduct/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/{customId}";

        APIClient.Instance.GetFull(detailUrl,
            json =>
            {
                var shopItem = JsonUtility.FromJson<APIProductItem>(json);
                currentDetail = new ProductDetail(shopItem);
                currentDetail.selectedSize = _lastSelectedSize;

                PopulateCommonUI();
                SetupUnpaidItemUI(); // Setup dropdown, buttons cho việc mua hàng

                _chatManager?.SetProductContext(currentDetail);
                _chatManager?.ReparentChatPanelTo(chatAnchor);
                _chatManager?.ShowProductWelcome();
            },
            error =>
            {
                Debug.LogError($"[ProductDetail] Failed to load detail: {error}");
                // Có thể hiển thị thông báo lỗi lên UI tại đây
            }
        );
    }

    // =================================================================================
    // CORE UI LOGIC
    // =================================================================================

    private void OpenPanel()
    {
        if (productDetailPanel != null) productDetailPanel.SetActive(true);

        PlayerController.Instance?.SetCanMove(false);
    }

    // Hiển thị các thông tin chung (Tên, giá, ảnh, mô tả)
    private void PopulateCommonUI()
    {
        if (currentDetail == null) return;

        // 1. Text Info
        if (productNameText != null) productNameText.text = currentDetail.title;
        if (productBrandText != null) productBrandText.text = $"Brand: {currentDetail.brandName}";
        if (productDescriptionText != null) productDescriptionText.text = currentDetail.description;

        if (productReviewsText != null)
            productReviewsText.text = $"⭐ {currentDetail.reviewScore}/5 ({currentDetail.reviewCount} reviews)";

        // 2. Price Logic
        if (productPriceText != null)
            productPriceText.text = $"{currentDetail.price:N0} VND";

        if (productOriginalPriceText != null)
        {
            if (currentDetail.originalPrice > currentDetail.price && !currentDetail.isPaidItem)
            {
                productOriginalPriceText.text = $"{currentDetail.originalPrice:N0} VND";
                productOriginalPriceText.gameObject.SetActive(true);
                productOriginalPriceText.fontStyle = FontStyles.Strikethrough;
            }
            else
            {
                productOriginalPriceText.gameObject.SetActive(false);
            }
        }

        SetupSwipeableGallery(currentDetail.galleryUrls);

        // ✅ BƯỚC 2: Load mainImage SAU — an toàn, không bị kill nữa
        if (productMainImage != null && !string.IsNullOrEmpty(currentDetail.mainImageUrl))
            StartCoroutine(LoadImage(currentDetail.mainImageUrl, productMainImage));
    }

    // Setup UI cho hàng ĐÃ MUA (Khóa nút mua, hiện size đã chọn)
    private void SetupPaidItemUI()
    {
        // Ẩn nút mua và nút xóa
        if (addToCartButton != null) addToCartButton.gameObject.SetActive(false);
        if (buyNowButton != null) buyNowButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);

        // Khóa dropdown size và chỉ hiện size đã mua
        if (sizeDropdown != null)
        {
            sizeDropdown.ClearOptions();
            sizeDropdown.AddOptions(new List<string> { currentDetail.selectedSize });
            sizeDropdown.interactable = false;
        }

        if (selectedSizeText != null)
            selectedSizeText.text = $"Đã chọn: {currentDetail.selectedSize}";
    }

    // Setup UI cho hàng CHƯA MUA (Hiện nút mua, load danh sách size)
    private void SetupUnpaidItemUI()
    {
        // Hiện nút mua
        if (addToCartButton != null) addToCartButton.gameObject.SetActive(true);
        if (buyNowButton != null) buyNowButton.gameObject.SetActive(true);

        // Wire nút Delete — closure capture currentCartItem
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(currentCartItem != null);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() =>
            {
                ShoppingCart.Instance.ClearUnpaidItems(currentCartItem);
            });
        }

        if (sizeDropdown != null)
        {
            sizeDropdown.interactable = true;
            sizeDropdown.ClearOptions();

            // 1. Tìm nhóm attribute đầu tiên có dữ liệu (Size, Perfume, Color...)
            var targetGroup = currentDetail.originalShopItem?.attributeGroups?.FirstOrDefault(g => g.attributes != null && g.attributes.Count > 0);

            // 2. Tạo text mặc định dựa trên tên nhóm (VD: "Chọn Size", "Chọn Perfume")
            string defaultText = targetGroup != null ? $"{targetGroup.name}" : "Size";
            List<string> options = new List<string> { defaultText };

            // 3. Add dữ liệu nếu tìm thấy group
            if (targetGroup != null)
            {
                foreach (var attr in targetGroup.attributes) options.Add(attr.name);
            }

            sizeDropdown.AddOptions(options);
            SizeCustomer(options); // (Lưu ý: Bạn cần update logic hàm này để check theo defaultText mới)
        }

        UpdateButtonsState();
    }

    private void SizeCustomer(List<string> sizeOptions)
    {
        Debug.Log($"🔍 CHECK 0: currentDetail.selectedSize = '{currentDetail.selectedSize}'"); // ← MỚI

        if (!string.IsNullOrEmpty(currentDetail.selectedSize))
        {
            int targetIndex = sizeOptions.FindIndex(s => s.Equals(currentDetail.selectedSize,
                StringComparison.OrdinalIgnoreCase));
            Debug.Log($"🔍 Tìm size cũ: '{currentDetail.selectedSize}'");
            Debug.Log($"📋 Size options: [{string.Join(" | ", sizeOptions)}]");
            Debug.Log($"📊 Target index: {targetIndex}");
            Debug.Log($"🔍 CHECK 1: targetIndex > 0 = {targetIndex > 0}");
            if (targetIndex > 0)
            {
                sizeDropdown.value = targetIndex;
                currentSelectedSize = currentDetail.selectedSize;
                Debug.Log($"✅ [ProductDetail] Auto-selected size: {currentSelectedSize}");
            }
            else
            {
                Debug.Log($"❌ FAILED AUTO-SELECT: targetIndex={targetIndex}, sizeOptions count={sizeOptions.Count}"); // ← MỚI
                sizeDropdown.value = 0;
                currentSelectedSize = "";
            }
        }
        else
        {
            Debug.Log("❌ selectedSize is null/empty → Reset");
            sizeDropdown.value = 0;
            currentSelectedSize = "";
        }
    }


    public void ShowProductDetail(APIProductItem item)
    {
        if (item == null) return;

        // Nếu item có customId (thường là từ Shop API), gọi logic Unpaid
        if (!string.IsNullOrEmpty(item.customId))
        {
            ShowUnpaidProductDetail(item.customId, item.selectSize);
        }
        else
        {
            Debug.LogWarning("[ProductDetailUI] ShowProductDetail called with item missing customId.");
        }
    }

    // =================================================================================
    // IMAGE GALLERY SYSTEM
    // =================================================================================

    private void SetupSwipeableGallery(List<string> images)
    {
        if (imageScrollContent == null || imagePagePrefab == null) return;
        StopAllCoroutines();

        for (int i = imageScrollContent.childCount - 1; i >= 0; i--)
            Destroy(imageScrollContent.GetChild(i).gameObject);
        imagePages.Clear();

        if (images == null || images.Count == 0) return;

        // ✅ Tạo TẤT CẢ pages trước
        var targetImages = new List<Image>();
        foreach (var url in images)
        {
            if (string.IsNullOrEmpty(url)) continue;
            var page = Instantiate(imagePagePrefab, imageScrollContent);
            var imgComp = page.GetComponent<Image>();
            if (imgComp != null)
            {
                imgComp.preserveAspect = true;
                imgComp.type = Image.Type.Simple;
                targetImages.Add(imgComp);
            }
            imagePages.Add(page);
        }

        // ✅ Force layout rebuild TRƯỚC khi load ảnh
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            imageScrollContent as RectTransform
        );

        // ✅ Load ảnh SAU khi layout đã đúng vị trí
        for (int i = 0; i < targetImages.Count; i++)
        {
            // Dùng index thay vì foreach để tránh closure issue
            int index = i;
            string url = images[index];
            StartCoroutine(LoadImage(url, targetImages[index]));
        }

        // ✅ Load productMainImage SAU CÙNG
        if (productMainImage != null && !string.IsNullOrEmpty(currentDetail?.mainImageUrl))
            StartCoroutine(LoadImage(currentDetail.mainImageUrl, productMainImage));

        ResetGalleryScroll();
    }


    private void ResetGalleryScroll()
    {
        if (imageScrollRect != null)
        {
            imageScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void Update()
    {
        if (imageScrollRect != null) SnapToNearestPage();
    }

    private void SnapToNearestPage()
    {
        if (imagePages.Count == 0) return;

        float pageWidth = 1f / Mathf.Max(imagePages.Count - 1, 1);
        int currentPageIndex = Mathf.RoundToInt(imageScrollRect.horizontalNormalizedPosition / pageWidth);
        float targetScrollPosition = currentPageIndex * pageWidth;

        imageScrollRect.horizontalNormalizedPosition = Mathf.Lerp(
            imageScrollRect.horizontalNormalizedPosition,
            targetScrollPosition,
            Time.deltaTime * snapSpeed
        );
        carouselIndicator.UpdateDots(currentPageIndex, imagePages.Count);
    }

    private IEnumerator LoadImage(string url, Image target)
    {
        // ✅ CHECK NULL + ACTIVE ngay đầu (QUAN TRỌNG)
        if (target == null || target.gameObject == null)
        {
            Debug.LogWarning("[LoadImage] Target Image is null or destroyed");
            yield break;
        }

        // ✅ CHECK panel còn active không
        if (productDetailPanel != null && !productDetailPanel.activeInHierarchy)
        {
            Debug.LogWarning("[LoadImage] Product panel is inactive");
            yield break;
        }

        ImageDownloadManager.Instance.DownloadImage(
     url,
     tex => {*//* apply to Image *//* },
     err => {*//* log error, set default sprite nếu muốn *//* }
        );

        Debug.Log($"[DEBUG] MainImage color: {productMainImage.color}");
        Debug.Log($"[DEBUG] MainImage sprite: {productMainImage.sprite}");
        Debug.Log($"[DEBUG] MainImage enabled: {productMainImage.enabled}");
        Debug.Log($"[DEBUG] MainImage GO active: {productMainImage.gameObject.activeInHierarchy}");
    }


    // =================================================================================
    // INTERACTION LOGIC
    // =================================================================================

    private void OnSizeChanged(int index)
    {
        AudioManager.Instance.PlaySFXOneShot("Button_High");
        if (currentDetail.isPaidItem) return;
        if (index > 0 && sizeDropdown != null)
        {
            currentSelectedSize = sizeDropdown.options[index].text;
            currentVariantId = GetVariantIdForSize(currentSelectedSize);
            Debug.Log($"🎮 Player selected: {currentSelectedSize}");
        }
        else
        {
            currentSelectedSize = "";
            currentVariantId = "";
        }
        UpdateButtonsState();
    }

    private string GetVariantIdForSize(string sizeName)
    {
        var item = currentDetail.originalShopItem;
        if (item?.variants == null) return item?.id ?? "";

        var variant = item.variants.FirstOrDefault(v =>
            v.attributeGroups != null &&
            v.attributeGroups.Any(g =>
                g.attributes != null &&
                g.attributes.Any(a =>
                    a.name != null && a.name.Equals(sizeName, StringComparison.OrdinalIgnoreCase)
                )
            )
        );
        return variant?.id ?? item.id ?? "";
    }

    private void UpdateButtonsState()
    {
        if (currentDetail == null || currentDetail.isPaidItem) return;

        bool hasSize = !string.IsNullOrEmpty(currentSelectedSize);
        if (addToCartButton != null) addToCartButton.interactable = hasSize;
        if (buyNowButton != null) buyNowButton.interactable = hasSize;
    }

    public void CloseProductDetail()
    {
        // ✅ STOP TẤT CẢ COROUTINES (AN TOÀN 100%)
        StopAllCoroutines();

        _chatManager?.ClearProductContext();
        _chatManager?.RestoreChatPanel();

        if (productDetailPanel != null)
            productDetailPanel.SetActive(false);
        PlayerController.Instance?.SetCanMove(true);


        ResetGalleryScroll();
        imagePages.Clear();
        //Debug.Log("[ProductDetailUI] Panel closed safely");
        TutorialGamePlay.Instance?.OnPlayerBackToShop();
    }

    // =================================================================================
    // BUYING ACTIONS
    // =================================================================================

    private void OnAddToCartClicked()
    {
        if (currentDetail.isPaidItem || string.IsNullOrEmpty(currentSelectedSize)) return;

        CartItem cartItem = new CartItem
        {
            customId = currentDetail.customId,
            productId = currentVariantId,
            productName = currentDetail.title,
            brandName = currentDetail.brandName,
            price = currentDetail.price,
            selectedSize = currentSelectedSize,
            imageUrl = currentDetail.mainImageUrl,
            quantity = 1,
            isPaid = false, // ✅ QUAN TRỌNG: Mặc định chưa thanh toán
            isSelectedForCheckout = false // ← Mặc định KHÔNG select để checkout
        };

        if (ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.AddToInventory(cartItem);
            Debug.Log("Added to cart successfully!");

        }

    }




}


*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel;

// ─────────────────────────────────────────────────────────────────────────────
// Adapter: data class chuẩn hóa để hiển thị UI
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class ProductDetail
{
    public string title;
    public string brandName;
    public float price;
    public float originalPrice;
    public string description;
    public float reviewScore;
    public int reviewCount;
    public string mainImageUrl;
    public List<string> galleryUrls = new List<string>();

    public bool isPaidItem;
    public string selectedSize;
    public string customId;
    public APIProductItem originalShopItem;

    // ── Constructor 1: từ Shop API (chưa mua) ────────────────────────────────
    public ProductDetail(APIProductItem shopItem)
    {
        title = shopItem.title;
        brandName = shopItem.brandName;
        price = shopItem.price;
        originalPrice = shopItem.regularPrice;
        selectedSize = shopItem.selectSize;

        description = $"Product ID: {shopItem.customId}\n";
        description += $"Brand: {shopItem.brandName}\n";
        description += $"Reviews: {shopItem.totalReviews} customers rated {shopItem.reviewStatFiveScale}★";

        reviewScore = shopItem.reviewStatFiveScale;
        reviewCount = shopItem.totalReviews;

        if (shopItem.images != null && shopItem.images.Count > 0)
        {
            mainImageUrl = shopItem.images[0].origin;
            foreach (var img in shopItem.images)
                if (!string.IsNullOrEmpty(img.origin)) galleryUrls.Add(img.origin);
        }

        isPaidItem = false;
        customId = shopItem.customId;
        originalShopItem = shopItem;
    }

    // ── Constructor 2: từ Inventory (đã mua) ────────────────────────────────
    public ProductDetail(CartItem paidItem)
    {
        title = paidItem.productName;
        price = paidItem.price;
        originalPrice = 0;

        brandName = !string.IsNullOrEmpty(paidItem.brandName) ? paidItem.brandName : "Unknown Brand";
        selectedSize = !string.IsNullOrEmpty(paidItem.selectedSize) ? paidItem.selectedSize : "Freesize";

        description = "ĐÃ SỞ HỮU\n";
        description += "----------------------\n";
        description += $"Thương hiệu: {brandName}\n";
        description += $"Kích thước: {selectedSize}\n";
        if (paidItem.purchaseDate != default(DateTime))
            description += $"Ngày mua: {paidItem.purchaseDate:dd/MM/yyyy}\n";

        reviewScore = 5;
        reviewCount = 1;

        mainImageUrl = paidItem.imageUrl;
        if (!string.IsNullOrEmpty(paidItem.imageUrl)) galleryUrls.Add(paidItem.imageUrl);

        isPaidItem = true;
        customId = paidItem.customId;
        originalShopItem = null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ProductDetailUI – refactored
// ─────────────────────────────────────────────────────────────────────────────
public class ProductDetailUI : MonoBehaviour
{
    public static ProductDetailUI Instance { get; private set; }

    // ── Serialized fields ────────────────────────────────────────────────────

    [Header("Product Detail UI")]
    [SerializeField] private GameObject productDetailPanel;
    [SerializeField] private Image productMainImage;
    [SerializeField] private TextMeshProUGUI productNameText;
    [SerializeField] private TextMeshProUGUI productBrandText;
    [SerializeField] private TextMeshProUGUI productPriceText;
    [SerializeField] private TextMeshProUGUI productOriginalPriceText;
    [SerializeField] private TextMeshProUGUI productDescriptionText;
    [SerializeField] private TextMeshProUGUI productReviewsText;

    [Header("Size Selection")]
    [SerializeField] private TMP_Dropdown sizeDropdown;
    [SerializeField] private TextMeshProUGUI selectedSizeText;

    [Header("Buttons")]
    [SerializeField] private Button addToCartButton;
    [SerializeField] private Button buyNowButton;
    [SerializeField] private Button closeDetailButton;
    [SerializeField] private Button deleteButton;

    [Header("Image Gallery")]
    [SerializeField] private Transform imageScrollContent;
    [SerializeField] private GameObject imagePagePrefab;
    [SerializeField] private float snapSpeed = 10f;
    [SerializeField] private ScrollRect imageScrollRect;
    [SerializeField] private CarouselIndicator carouselIndicator;

    [Header("Chat Integration")]
    [SerializeField] private RectTransform chatAnchor;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private List<GameObject> imagePages = new List<GameObject>();
    private ProductDetail currentDetail;
    private CartItem currentCartItem;
    private string currentSelectedSize = "";
    private string currentProductId = "";
    private string _lastSelectedSize = "";

    // Main-image CTS (cancel khi mở sản phẩm mới hoặc đóng panel)
    private CancellationTokenSource _mainImageCts;
    private bool _ownsMainSprite;

    // Preload window: load ngay page 0..1, lazy-load phần còn lại khi swipe
    private const int PreloadPages = 2;
    private int _lastLoadedPageIndex = -1;

    private MultiChatManager _chatManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _chatManager = FindAnyObjectByType<MultiChatManager>();
        InitializeUI();
    }

    private void InitializeUI()
    {
        addToCartButton?.onClick.AddListener(() =>
        {
            PopupManager.Instance.ShowPopup(
                "Xác nhận",
                "Bạn có muốn thêm vật phẩm này vào giỏ hàng không?",
                () =>
                {
                    OnAddToCartClicked();
                    TutorialGamePlay.Instance?.OnAddToCartSuccess();
                }
            );
        });

        closeDetailButton?.onClick.AddListener(CloseProductDetail);
        sizeDropdown.onValueChanged.RemoveAllListeners();
        sizeDropdown.onValueChanged.AddListener(OnSizeChanged);

        if (productDetailPanel != null) productDetailPanel.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Hiển thị sản phẩm đã mua (từ Inventory).</summary>
    public void ShowPaidProductDetail(CartItem paidItem)
    {
        Debug.Log($"[ProductDetail] Showing PAID item: {paidItem.productName}");
        currentDetail = new ProductDetail(paidItem);
        OpenPanel();
        PopulateCommonUI();
        SetupPaidItemUI();
    }

    /// <summary>Hiển thị sản phẩm chưa mua (từ Shop) – có gọi API.</summary>
    public void ShowUnpaidProductDetail(string customId, string preSelectedSize = "")
    {
        Debug.Log($"[ProductDetail] Fetching shop item: {customId}");
        OpenPanel();
        _lastSelectedSize = preSelectedSize;

        currentCartItem = ShoppingCart.Instance?.GetUnpaidItems()
            .Find(i => i.customId == customId && i.selectedSize == preSelectedSize);

        string detailUrl =
            $"https://data.storims.c1.hubcom.tech/api/v1/TenantProduct/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/{customId}";

        APIClient.Instance.GetFull(
            detailUrl,
            json =>
            {
                var shopItem = JsonUtility.FromJson<APIProductItem>(json);
                currentDetail = new ProductDetail(shopItem)
                {
                    selectedSize = _lastSelectedSize
                };

                PopulateCommonUI();
                SetupUnpaidItemUI();

                _chatManager?.SetProductContext(currentDetail);
                _chatManager?.ReparentChatPanelTo(chatAnchor);
                _chatManager?.ShowProductWelcome();
            },
            error => Debug.LogError($"[ProductDetail] Failed to load detail: {error}")
        );
    }

    /// <summary>Shortcut gọi từ shop item click.</summary>
    public void ShowProductDetail(APIProductItem item)
    {
        if (item == null) return;
        if (!string.IsNullOrEmpty(item.customId))
            ShowUnpaidProductDetail(item.customId, item.selectSize);
        else
            Debug.LogWarning("[ProductDetailUI] ShowProductDetail: item missing customId.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CORE UI
    // ═════════════════════════════════════════════════════════════════════════

    private void OpenPanel()
    {
        // Hủy mọi download đang chạy từ panel trước
        CancelMainImage();
        ReleaseAllGallerySprites();

        if (productDetailPanel != null) productDetailPanel.SetActive(true);
        PlayerController.Instance?.SetCanMove(false);
    }

    private void PopulateCommonUI()
    {
        if (currentDetail == null) return;

        // ── Text ──────────────────────────────────────────────────────────────
        if (productNameText != null) productNameText.text = currentDetail.title;
        if (productBrandText != null) productBrandText.text = $"Brand: {currentDetail.brandName}";
        if (productDescriptionText != null) productDescriptionText.text = currentDetail.description;
        if (productReviewsText != null)
            productReviewsText.text = $"⭐ {currentDetail.reviewScore}/5 ({currentDetail.reviewCount} reviews)";

        // ── Price ─────────────────────────────────────────────────────────────
        if (productPriceText != null)
            productPriceText.text = $"{currentDetail.price:N0} VND";

        if (productOriginalPriceText != null)
        {
            bool hasDiscount = currentDetail.originalPrice > currentDetail.price && !currentDetail.isPaidItem;
            productOriginalPriceText.gameObject.SetActive(hasDiscount);
            if (hasDiscount)
            {
                productOriginalPriceText.text = $"{currentDetail.originalPrice:N0} VND";
                productOriginalPriceText.fontStyle = FontStyles.Strikethrough;
            }
        }

        // ── Gallery + Main Image ──────────────────────────────────────────────
        SetupSwipeableGallery(currentDetail.galleryUrls);
        LoadMainImage(currentDetail.mainImageUrl);
    }

    private void SetupPaidItemUI()
    {
        if (addToCartButton != null) addToCartButton.gameObject.SetActive(false);
        if (buyNowButton != null) buyNowButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);

        if (sizeDropdown != null)
        {
            sizeDropdown.ClearOptions();
            sizeDropdown.AddOptions(new List<string> { currentDetail.selectedSize });
            sizeDropdown.interactable = false;
        }

        if (selectedSizeText != null)
            selectedSizeText.text = $"Đã chọn: {currentDetail.selectedSize}";
    }

    private void SetupUnpaidItemUI()
    {
        if (addToCartButton != null) addToCartButton.gameObject.SetActive(true);
        if (buyNowButton != null) buyNowButton.gameObject.SetActive(true);

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(currentCartItem != null);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => ShoppingCart.Instance.ClearUnpaidItems(currentCartItem));
        }

        if (sizeDropdown != null)
        {
            sizeDropdown.interactable = true;
            sizeDropdown.ClearOptions();

            var targetGroup = currentDetail.originalShopItem?.attributeGroups?
                .FirstOrDefault(g => g.attributes != null && g.attributes.Count > 0);

            string defaultText = targetGroup != null ? targetGroup.name : "Size";
            var options = new List<string> { defaultText };
            if (targetGroup != null)
                foreach (var attr in targetGroup.attributes) options.Add(attr.name);

            sizeDropdown.AddOptions(options);
            SizeCustomer(options);
        }

        UpdateButtonsState();
    }

    private void SizeCustomer(List<string> sizeOptions)
    {
        if (!string.IsNullOrEmpty(currentDetail.selectedSize))
        {
            int idx = sizeOptions.FindIndex(s =>
                s.Equals(currentDetail.selectedSize, StringComparison.OrdinalIgnoreCase));

            if (idx > 0)
            {
                sizeDropdown.value = idx;
                currentSelectedSize = currentDetail.selectedSize;
            }
            else
            {
                sizeDropdown.value = 0;
                currentSelectedSize = "";
            }
        }
        else
        {
            sizeDropdown.value = 0;
            currentSelectedSize = "";
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // IMAGE LOADING – dùng ImageDownloadManager (shared cache)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Load ảnh main (lớn, bên phải). Cancel load trước đó nếu có.</summary>
    private void LoadMainImage(string url)
    {
        CancelMainImage();
        if (string.IsNullOrEmpty(url) || productMainImage == null) return;

        _mainImageCts = new CancellationTokenSource();
        var token = _mainImageCts.Token;

        ImageDownloadManager.Instance.DownloadImage(
            url,
            texture =>
            {
                if (token.IsCancellationRequested) return;
                if (productDetailPanel == null || !productDetailPanel.activeInHierarchy) return;
                ApplyTextureToImage(texture, productMainImage, ref _ownsMainSprite);
            },
            error => Debug.LogWarning($"[Detail] Main image failed: {url} | {error}")
        );
    }

    private void CancelMainImage()
    {
        if (_mainImageCts == null) return;
        _mainImageCts.Cancel();
        _mainImageCts.Dispose();
        _mainImageCts = null;
    }

    /// <summary>
    /// Tạo Sprite từ texture đã có trong cache.
    /// Chỉ destroy sprite cũ (local), KHÔNG destroy texture (thuộc CacheService).
    /// </summary>
    private void ApplyTextureToImage(Texture2D texture, Image target, ref bool ownsFlag)
    {
        if (texture == null || target == null) return;

        if (ownsFlag && target.sprite != null)
        {
            Destroy(target.sprite);
            target.sprite = null;
        }

        target.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
        ownsFlag = true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GALLERY SYSTEM – lazy-load với preload window
    // ═════════════════════════════════════════════════════════════════════════

    private void SetupSwipeableGallery(List<string> images)
    {
        if (imageScrollContent == null || imagePagePrefab == null) return;

        // Dọn pages cũ (DetailGalleryImage.OnDestroy tự release sprite)
        for (int i = imageScrollContent.childCount - 1; i >= 0; i--)
            Destroy(imageScrollContent.GetChild(i).gameObject);
        imagePages.Clear();
        _lastLoadedPageIndex = -1;

        if (images == null || images.Count == 0) return;

        // Tạo tất cả page objects (chưa load ảnh)
        foreach (var url in images)
        {
            if (string.IsNullOrEmpty(url)) continue;

            var page = Instantiate(imagePagePrefab, imageScrollContent);
            var imgComp = page.GetComponent<Image>();

            if (imgComp != null)
            {
                imgComp.preserveAspect = true;
                imgComp.type = Image.Type.Simple;

                // Gắn component quản lý sprite
                var holder = page.AddComponent<DetailGalleryImage>();
                holder.targetImage = imgComp;
            }

            imagePages.Add(page);
        }

        // Force layout rebuild trước khi load ảnh
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(imageScrollContent as RectTransform);

        // Preload window ban đầu (page 0 .. PreloadPages-1)
        for (int i = 0; i < Mathf.Min(PreloadPages, images.Count); i++)
            RequestPageLoad(i, images);

        _lastLoadedPageIndex = Mathf.Min(PreloadPages - 1, images.Count - 1);

        ResetGalleryScroll();
    }

    /// <summary>Yêu cầu load ảnh cho page index nếu chưa load.</summary>
    private void RequestPageLoad(int pageIndex, List<string> urls)
    {
        if (pageIndex < 0 || pageIndex >= imagePages.Count) return;
        if (pageIndex >= urls.Count) return;

        var holder = imagePages[pageIndex].GetComponent<DetailGalleryImage>();
        if (holder == null) return;

        // Nếu đã có sprite thì skip (đã load rồi)
        if (holder.targetImage != null && holder.targetImage.sprite != null) return;

        holder.LoadImage(urls[pageIndex]);
    }

    private void ResetGalleryScroll()
    {
        if (imageScrollRect != null)
            imageScrollRect.horizontalNormalizedPosition = 0f;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPDATE – snap + lazy-load trigger
    // ═════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (imageScrollRect == null || imagePages.Count == 0) return;
        if (productDetailPanel == null || !productDetailPanel.activeSelf) return;

        SnapToNearestPage();
        TryLazyLoadNextPage();
    }
    private int _lastDotIndex = -1;
    private void SnapToNearestPage()
    {
        /* float pageWidth = 1f / Mathf.Max(imagePages.Count - 1, 1);
         int currentPageIndex = Mathf.RoundToInt(
             imageScrollRect.horizontalNormalizedPosition / pageWidth);

         float target = currentPageIndex * pageWidth;
         imageScrollRect.horizontalNormalizedPosition = Mathf.Lerp(
             imageScrollRect.horizontalNormalizedPosition, target, Time.deltaTime * snapSpeed);

         carouselIndicator.UpdateDots(currentPageIndex, imagePages.Count);*/
        float pageWidth = 1f / Mathf.Max(imagePages.Count - 1, 1);
        int currentPageIndex = Mathf.RoundToInt(
            imageScrollRect.horizontalNormalizedPosition / pageWidth);

        float target = currentPageIndex * pageWidth;
        float current = imageScrollRect.horizontalNormalizedPosition;

        // ✅ Đã snap xong → đừng Lerp nữa (Lerp gần đích gây dirty layout mỗi frame)
        if (Mathf.Abs(current - target) > 0.0005f)
            imageScrollRect.horizontalNormalizedPosition =
                Mathf.Lerp(current, target, Time.deltaTime * snapSpeed);

        // ✅ Chỉ update dots khi index THAY ĐỔI, không phải mỗi frame
        if (currentPageIndex != _lastDotIndex)
        {
            _lastDotIndex = currentPageIndex;
            carouselIndicator.UpdateDots(currentPageIndex, imagePages.Count);
        }

    }

    /// <summary>Khi user swipe đến gần cuối vùng đã load → load thêm PreloadPages.</summary>
    private void TryLazyLoadNextPage()
    {
        if (currentDetail?.galleryUrls == null) return;

        float pageWidth = 1f / Mathf.Max(imagePages.Count - 1, 1);
        int visiblePage = Mathf.RoundToInt(imageScrollRect.horizontalNormalizedPosition / pageWidth);
        int wantUpTo = visiblePage + PreloadPages;

        if (wantUpTo <= _lastLoadedPageIndex) return;

        for (int i = _lastLoadedPageIndex + 1; i <= wantUpTo; i++)
            RequestPageLoad(i, currentDetail.galleryUrls);

        _lastLoadedPageIndex = Mathf.Max(_lastLoadedPageIndex, wantUpTo);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CLOSE & CLEANUP
    // ═════════════════════════════════════════════════════════════════════════

    public void CloseProductDetail()
    {
        // 1. Cancel tất cả download đang bay
        CancelMainImage();

        // 2. Đóng panel
        _chatManager?.ClearProductContext();
        _chatManager?.RestoreChatPanel();

        if (productDetailPanel != null) productDetailPanel.SetActive(false);

        PlayerController.Instance?.SetCanMove(true);

        // 3. Release sprite local (texture vẫn ở CacheService)
        ReleaseMainSprite();
        ReleaseAllGallerySprites();

        // 4. Xóa pages
        ResetGalleryScroll();
        imagePages.Clear();

        TutorialGamePlay.Instance?.OnPlayerBackToShop();
    }

    private void ReleaseMainSprite()
    {
        if (_ownsMainSprite && productMainImage != null && productMainImage.sprite != null)
        {
            Destroy(productMainImage.sprite);
            productMainImage.sprite = null;
        }
        _ownsMainSprite = false;
    }

    private void ReleaseAllGallerySprites()
    {
        foreach (var page in imagePages)
        {
            if (page == null) continue;
            var holder = page.GetComponent<DetailGalleryImage>();
            holder?.ReleaseSprite();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // INTERACTION – size dropdown, add to cart, buy now
    // ═════════════════════════════════════════════════════════════════════════

    private void OnSizeChanged(int index)
    {
        AudioManager.Instance.PlaySFXOneShot("Button_High");
        if (currentDetail.isPaidItem) return;

        if (index > 0 && sizeDropdown != null)
        {
            currentSelectedSize = sizeDropdown.options[index].text;
            currentProductId = GetVariantIdForSize(currentSelectedSize);
        }
        else
        {
            currentSelectedSize = "";
            currentProductId = "";
        }

        UpdateButtonsState();
    }

    private string GetVariantIdForSize(string sizeName)
    {
        var item = currentDetail.originalShopItem;
        if (item?.attributeGroups == null) return "";

        foreach (var group in item.attributeGroups)
        {
            if (group.attributes == null) continue;
            var attr = group.attributes.Find(a =>
                a.name.Equals(sizeName, StringComparison.OrdinalIgnoreCase));
            if (attr != null) return attr.id ?? "";
        }
        return "";
    }

    private void UpdateButtonsState()
    {
        bool canBuy = !string.IsNullOrEmpty(currentSelectedSize);
        if (addToCartButton != null) addToCartButton.interactable = canBuy;
        if (buyNowButton != null) buyNowButton.interactable = canBuy;
    }

    private void OnAddToCartClicked()
    {
        if (currentDetail == null || string.IsNullOrEmpty(currentSelectedSize))
        {
            Debug.LogWarning("[ProductDetail] No size selected.");
            return;
        }

        ShoppingCart.Instance?.AddItem(new CartItem
        {
            customId = currentDetail.customId,
            productId = currentProductId,
            productName = currentDetail.title,
            brandName = currentDetail.brandName,
            price = currentDetail.price,
            selectedSize = currentSelectedSize,
            imageUrl = currentDetail.mainImageUrl,
            quantity = 1,
        });

        Debug.Log($"[ProductDetail] Added to cart: {currentDetail.title} – {currentSelectedSize}");
    }
}