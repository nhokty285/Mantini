using System;
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
    [SerializeField] private PlayerController _playerController;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _chatManager = FindAnyObjectByType<MultiChatManager>();
        _playerController ??= FindFirstObjectByType<PlayerController>();
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
                () => {
                    // Khi bấm "Đồng ý" trên Popup thì mới chạy hàm này
                    OnAddToCartClicked();
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
            json => {
                var shopItem = JsonUtility.FromJson<APIProductItem>(json);
                currentDetail = new ProductDetail(shopItem);
                currentDetail.selectedSize = _lastSelectedSize;

                PopulateCommonUI();
                SetupUnpaidItemUI(); // Setup dropdown, buttons cho việc mua hàng

                _chatManager?.SetProductContext(currentDetail);
                _chatManager?.ReparentChatPanelTo(chatAnchor);
                _chatManager?.ShowProductWelcome();
            },
            error => {
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

        if (_playerController == null)
            Debug.LogWarning("[ProductDetailUI] PlayerController not found! Player movement not locked.");
        _playerController?.SetCanMove(false);
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

        // 3. Images
        if (productMainImage != null && !string.IsNullOrEmpty(currentDetail.mainImageUrl))
            StartCoroutine(LoadImage(currentDetail.mainImageUrl, productMainImage));

        SetupSwipeableGallery(currentDetail.galleryUrls);
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
    /* private void SetupSwipeableGallery(List<string> images)
     {
         if (imageScrollContent == null || imagePagePrefab == null) return;

         foreach (Transform child in imageScrollContent) Destroy(child.gameObject);
         imagePages.Clear();

         if (images == null) return;

         foreach (var url in images)
         {
             var page = Instantiate(imagePagePrefab, imageScrollContent);
             var imgComp = page.GetComponent<Image>();
             if (imgComp != null)
             {
                 imgComp.preserveAspect = true;
                 StartCoroutine(LoadImage(url, imgComp));
             }
             imagePages.Add(page);
         }
         ResetGalleryScroll();
     }*/

    private void SetupSwipeableGallery(List<string> images)
    {
        if (imageScrollContent == null || imagePagePrefab == null) return;

        // ✅ STOP COROUTINES TRƯỚC KHI DESTROY
        StopAllCoroutines();

        // ✅ SAFE DESTROY cũ
        for (int i = imageScrollContent.childCount - 1; i >= 0; i--)
        {
            Transform child = imageScrollContent.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }

        imagePages.Clear();

        if (images == null || images.Count == 0) return;

        foreach (var url in images)
        {
            if (string.IsNullOrEmpty(url)) continue;

            var page = Instantiate(imagePagePrefab, imageScrollContent);
            var imgComp = page.GetComponent<Image>();

            if (imgComp != null)
            {
                imgComp.preserveAspect = true;
                // ✅ START SAFE COROUTINE
                StartCoroutine(LoadImage(url, imgComp));
            }
            imagePages.Add(page);
        }

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

    /* private IEnumerator LoadImage(string url, Image target)
     {
         using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
         {
             yield return request.SendWebRequest();
             if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
             {
                 var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                 target.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
                 target.preserveAspect = true;
             }
         }
     }*/

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

        using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            // ✅ CHECK REQUEST THÀNH CÔNG
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LoadImage] Failed to load {url}: {request.error}");
                yield break;
            }

            // ✅ CHECK LẠI TARGET TRƯỚC KHI ASSIGN (CRITICAL!)
            if (target == null || target.gameObject == null)
            {
                Debug.LogWarning("[LoadImage] Target destroyed during download");
                yield break;
            }

            // ✅ CHECK GAMEOBJECT CÒN ACTIVE trong hierarchy
            if (!target.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[LoadImage] Target inactive in hierarchy");
                yield break;
            }

            // ✅ SAFE ASSIGN SPRITE
            try
            {
                var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                if (texture != null)
                {
                    target.sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    target.preserveAspect = true;
                    Debug.Log($"[LoadImage] Success: {url}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LoadImage] Sprite creation failed: {e.Message}");
            }
        }
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

    /*public void CloseProductDetail()
    {
        if (productDetailPanel != null) productDetailPanel.SetActive(false);
        currentSelectedSize = "";
        ResetGalleryScroll();
    }*/

    public void CloseProductDetail()
    {
        // ✅ STOP TẤT CẢ COROUTINES (AN TOÀN 100%)
        StopAllCoroutines();

        _chatManager?.ClearProductContext();
        _chatManager?.RestoreChatPanel();

        if (productDetailPanel != null)
            productDetailPanel.SetActive(false);
        _playerController ??= FindFirstObjectByType<PlayerController>();

        if (_playerController == null)
            Debug.LogWarning("[ProductDetailUI] PlayerController not found! Player movement not restored.");
        _playerController?.SetCanMove(true);


        //currentSelectedSize = "";
        ResetGalleryScroll();
        imagePages.Clear();
        //Debug.Log("[ProductDetailUI] Panel closed safely");
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
