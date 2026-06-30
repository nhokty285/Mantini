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

    private readonly List<GameObject> _imagePages = new List<GameObject>();
    private ProductDetail _currentDetail;
    private CartItem _currentCartItem;
    private string _currentSelectedSize = "";
    private string _currentProductId = "";
    private string _lastSelectedSize = "";

    // Variant thật resolve theo size đã chọn (storims: mỗi size = 1 variant riêng).
    // _currentVariantId       = variant.id (UUID) -> gửi server làm tenantProductVariantId
    // _currentVariantCustomId = variant.customId (vd 200599/201445) -> định danh mặt hàng
    private string _currentVariantId = "";
    private string _currentVariantCustomId = "";

    // Main-image CTS (cancel khi mở sản phẩm mới hoặc đóng panel)
    private CancellationTokenSource _mainImageCts;
    private bool _ownsMainSprite;

    // Preload window: load ngay page 0..1, lazy-load phần còn lại khi swipe
    private const int PreloadPages = 2;
    private int _lastLoadedPageIndex = -1;
    private int _lastDotIndex = -1;

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
        // Refactor: RemoveAllListeners trước AddListener theo Mantini convention
        if (addToCartButton != null)
        {
            addToCartButton.onClick.RemoveAllListeners();
            addToCartButton.onClick.AddListener(OnAddToCartButtonClicked);
        }

        if (buyNowButton != null)
        {
            buyNowButton.onClick.RemoveAllListeners();
            buyNowButton.onClick.AddListener(OnBuyNowButtonClicked);
        }

        if (closeDetailButton != null)
        {
            closeDetailButton.onClick.RemoveAllListeners();
            closeDetailButton.onClick.AddListener(CloseProductDetail);
        }

        if (sizeDropdown != null)
        {
            sizeDropdown.onValueChanged.RemoveAllListeners();
            sizeDropdown.onValueChanged.AddListener(OnSizeChanged);
        }

        if (productDetailPanel != null) productDetailPanel.SetActive(false);
    }

    // Refactor: tách lambda Popup thành method có tên — đỡ alloc closure mỗi Awake
    private void OnAddToCartButtonClicked()
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
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Hiển thị sản phẩm đã mua (từ Inventory).</summary>
    public void ShowPaidProductDetail(CartItem paidItem)
    {
#if UNITY_EDITOR
        GameLog.Info($"[ProductDetailUI] Showing PAID item: {paidItem.productName}");
#endif
        _currentDetail = new ProductDetail(paidItem);
        OpenPanel();
        PopulateCommonUI();
        SetupPaidItemUI();
    }

    /// <summary>Hiển thị sản phẩm chưa mua (từ Shop) – có gọi API.</summary>
    public void ShowUnpaidProductDetail(string customId, string preSelectedSize = "")
    {
#if UNITY_EDITOR
        GameLog.Info($"[ProductDetailUI] Fetching shop item: {customId}");
#endif
        OpenPanel();
        _lastSelectedSize = preSelectedSize;

        _currentCartItem = ShoppingCart.Instance?.GetUnpaidItems()
            .Find(i => i.customId == customId && i.selectedSize == preSelectedSize);

        string detailUrl =
            $"https://data.storims.c1.hubcom.tech/api/v1/TenantProduct/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/{customId}";

        APIClient.Instance.GetFull(
            detailUrl,
            json =>
            {
                var shopItem = JsonUtility.FromJson<APIProductItem>(json);
                _currentDetail = new ProductDetail(shopItem)
                {
                    selectedSize = _lastSelectedSize
                };

                PopulateCommonUI();
                SetupUnpaidItemUI();

                _chatManager?.SetProductContext(_currentDetail);
                _chatManager?.ReparentChatPanelTo(chatAnchor);
                _chatManager?.ShowProductWelcome();
            },
            error => Debug.LogError($"[ProductDetailUI] Failed to load detail: {error}")
        );
    }

    /// <summary>Shortcut gọi từ shop item click.</summary>
    public void ShowProductDetail(APIProductItem item)
    {
        if (item == null) return;
        if (!string.IsNullOrEmpty(item.customId))
            ShowUnpaidProductDetail(item.customId, item.selectSize);
        else
            GameLog.Warn("[ProductDetailUI] ShowProductDetail: item missing customId.");
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
        if (_currentDetail == null) return;

        // ── Text ──────────────────────────────────────────────────────────────
        if (productNameText != null) productNameText.text = _currentDetail.title;
        if (productBrandText != null) productBrandText.text = $"Brand: {_currentDetail.brandName}";
        if (productDescriptionText != null) productDescriptionText.text = _currentDetail.description;
        if (productReviewsText != null)
            productReviewsText.text = $"⭐ {_currentDetail.reviewScore}/5 ({_currentDetail.reviewCount} reviews)";

        // ── Price ─────────────────────────────────────────────────────────────
        if (productPriceText != null)
            productPriceText.text = $"{_currentDetail.price:N0} VND";

        if (productOriginalPriceText != null)
        {
            bool hasDiscount = _currentDetail.originalPrice > _currentDetail.price && !_currentDetail.isPaidItem;
            productOriginalPriceText.gameObject.SetActive(hasDiscount);
            if (hasDiscount)
            {
                productOriginalPriceText.text = $"{_currentDetail.originalPrice:N0} VND";
                productOriginalPriceText.fontStyle = FontStyles.Strikethrough;
            }
        }

        // ── Gallery + Main Image ──────────────────────────────────────────────
        SetupSwipeableGallery(_currentDetail.galleryUrls);
        LoadMainImage(_currentDetail.mainImageUrl);
    }

    private void SetupPaidItemUI()
    {
        if (addToCartButton != null) addToCartButton.gameObject.SetActive(false);
        if (buyNowButton != null) buyNowButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);

        if (sizeDropdown != null)
        {
            sizeDropdown.ClearOptions();
            sizeDropdown.AddOptions(new List<string> { _currentDetail.selectedSize });
            sizeDropdown.interactable = false;
        }

        if (selectedSizeText != null)
            selectedSizeText.text = $"Đã chọn: {_currentDetail.selectedSize}";
    }

    private void SetupUnpaidItemUI()
    {
        if (addToCartButton != null) addToCartButton.gameObject.SetActive(true);
        if (buyNowButton != null) buyNowButton.gameObject.SetActive(true);

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(_currentCartItem != null);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => ShoppingCart.Instance.ClearUnpaidItems(_currentCartItem));
        }

        if (sizeDropdown != null)
        {
            sizeDropdown.interactable = true;
            sizeDropdown.ClearOptions();

            var targetGroup = _currentDetail.originalShopItem?.attributeGroups?
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
        if (!string.IsNullOrEmpty(_currentDetail.selectedSize))
        {
            int idx = sizeOptions.FindIndex(s =>
                s.Equals(_currentDetail.selectedSize, StringComparison.OrdinalIgnoreCase));

            if (idx > 0)
            {
                sizeDropdown.value = idx;
                _currentSelectedSize = _currentDetail.selectedSize;
            }
            else
            {
                sizeDropdown.value = 0;
                _currentSelectedSize = "";
            }
        }
        else
        {
            sizeDropdown.value = 0;
            _currentSelectedSize = "";
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
            error => GameLog.Warn($"[ProductDetailUI] Main image failed: {url} | {error}")
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
        _imagePages.Clear();
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

            _imagePages.Add(page);
        }

        // Force layout rebuild trước khi load ảnh
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(imageScrollContent as RectTransform);

        // Preload window ban đầu (page 0 .. PreloadPages-1)
        int preloadCount = Mathf.Min(PreloadPages, images.Count);
        for (int i = 0; i < preloadCount; i++)
            RequestPageLoad(i, images);

        _lastLoadedPageIndex = Mathf.Min(PreloadPages - 1, images.Count - 1);

        ResetGalleryScroll();
    }

    /// <summary>Yêu cầu load ảnh cho page index nếu chưa load.</summary>
    private void RequestPageLoad(int pageIndex, List<string> urls)
    {
        if (pageIndex < 0 || pageIndex >= _imagePages.Count) return;
        if (pageIndex >= urls.Count) return;

        var holder = _imagePages[pageIndex].GetComponent<DetailGalleryImage>();
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
        if (imageScrollRect == null || _imagePages.Count == 0) return;
        if (productDetailPanel == null || !productDetailPanel.activeSelf) return;

        SnapToNearestPage();
        TryLazyLoadNextPage();
    }

    private void SnapToNearestPage()
    {
        float pageWidth = 1f / Mathf.Max(_imagePages.Count - 1, 1);
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
            if (carouselIndicator != null)
                carouselIndicator.UpdateDots(currentPageIndex, _imagePages.Count);
        }
    }

    /// <summary>Khi user swipe đến gần cuối vùng đã load → load thêm PreloadPages.</summary>
    private void TryLazyLoadNextPage()
    {
        if (_currentDetail?.galleryUrls == null) return;

        float pageWidth = 1f / Mathf.Max(_imagePages.Count - 1, 1);
        int visiblePage = Mathf.RoundToInt(imageScrollRect.horizontalNormalizedPosition / pageWidth);
        int wantUpTo = visiblePage + PreloadPages;

        if (wantUpTo <= _lastLoadedPageIndex) return;

        for (int i = _lastLoadedPageIndex + 1; i <= wantUpTo; i++)
            RequestPageLoad(i, _currentDetail.galleryUrls);

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
        _imagePages.Clear();

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
        foreach (var page in _imagePages)
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXOneShot("Button_High");

        if (_currentDetail == null || _currentDetail.isPaidItem) return;

        if (index > 0 && sizeDropdown != null)
        {
            _currentSelectedSize = sizeDropdown.options[index].text;
            ResolveVariantForSize(_currentSelectedSize);
        }
        else
        {
            _currentSelectedSize = "";
            _currentProductId = "";
            _currentVariantId = "";
            _currentVariantCustomId = "";
        }

        UpdateButtonsState();
    }

    // Resolve variant theo size đã chọn: tra trong variants[] (mỗi variant = 1 size).
    // Set _currentVariantId = variant.id (UUID thật) và _currentVariantCustomId = variant.customId.
    // Fallback: nếu không tìm thấy variant, dùng lại attribute id (giữ behavior cũ, tránh null).
    private void ResolveVariantForSize(string sizeName)
    {
        _currentVariantId = "";
        _currentVariantCustomId = "";
        _currentProductId = "";

        var item = _currentDetail?.originalShopItem;
        if (item == null) return;

        // 1) Ưu tiên match trong variants[] theo tên size trong attributeGroups của variant
        if (item.variants != null)
        {
            foreach (var v in item.variants)
            {
                if (v == null || v.attributeGroups == null) continue;
                bool matched = false;
                foreach (var g in v.attributeGroups)
                {
                    if (g?.attributes == null) continue;
                    if (g.attributes.Exists(a =>
                            a != null && a.name != null &&
                            a.name.Equals(sizeName, StringComparison.OrdinalIgnoreCase)))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    _currentVariantId = v.id ?? "";
                    _currentVariantCustomId = v.customId ?? "";
                    _currentProductId = _currentVariantId; // productId = variant UUID thật
                    return;
                }
            }
        }

        // 2) Fallback: không có variants[] khớp -> dùng attribute id như cũ
        if (item.attributeGroups != null)
        {
            foreach (var group in item.attributeGroups)
            {
                if (group.attributes == null) continue;
                var attr = group.attributes.Find(a =>
                    a.name.Equals(sizeName, StringComparison.OrdinalIgnoreCase));
                if (attr != null)
                {
                    _currentProductId = attr.id ?? "";
                    _currentVariantId = _currentProductId;
                    // customId fallback: dùng product-level customId nếu không có variant
                    _currentVariantCustomId = _currentDetail?.customId ?? "";
                    GameLog.Warn($"[ProductDetailUI] No variant matched size '{sizeName}', fallback to attribute id.");
                    return;
                }
            }
        }
    }

    private void UpdateButtonsState()
    {
        bool canBuy = !string.IsNullOrEmpty(_currentSelectedSize);
        if (addToCartButton != null) addToCartButton.interactable = canBuy;
        if (buyNowButton != null) buyNowButton.interactable = canBuy;
    }

    private void OnAddToCartClicked()
    {
        if (_currentDetail == null || string.IsNullOrEmpty(_currentSelectedSize))
        {
            GameLog.Warn("[ProductDetailUI] No size selected.");
            return;
        }

        ShoppingCart.Instance?.AddItem(new CartItem
        {
            // customId = variant customId (200599/201445) — định danh mặt hàng theo nghiệp vụ
            customId = !string.IsNullOrEmpty(_currentVariantCustomId)
                ? _currentVariantCustomId
                : _currentDetail.customId,
            // parentCustomId = customId cấp sản phẩm gốc (vd "23612") — dùng để ProductDetailUI
            // mở lại ĐÚNG endpoint + load đủ gallery ảnh khi xem lại từ giỏ (CartUI long-press).
            parentCustomId = _currentDetail.customId,
            productId = _currentProductId, // = variant.id (UUID thật) sau resolve
            productName = _currentDetail.title,
            brandName = _currentDetail.brandName,
            price = _currentDetail.price,
            selectedSize = _currentSelectedSize,
            imageUrl = _currentDetail.mainImageUrl,
            quantity = 1,
        });

#if UNITY_EDITOR
        GameLog.Info($"[ProductDetailUI] Added to cart: {_currentDetail.title} – {_currentSelectedSize}");
#endif
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUY NOW — mua ngay đúng vật phẩm đang xem (kiểu Shopee)
    // ═════════════════════════════════════════════════════════════════════════

    private void OnBuyNowButtonClicked()
    {
        if (_currentDetail == null || string.IsNullOrEmpty(_currentSelectedSize))
        {
            PopupManager.Instance.ShowPopup("Thông báo", "Vui lòng chọn size trước khi mua.", null, "Đóng");
            return;
        }

        if (ShoppingCart.Instance == null || ShoppingCart.Instance.cartUI == null)
        {
            Debug.LogError("[ProductDetailUI] ShoppingCart/cartUI not available for Buy Now.");
            return;
        }

        var item = BuildCurrentCartItem();

        // Mở panel nhập thông tin + thanh toán dùng chung của CartUI ở chế độ Buy Now.
        // Chỉ khi player điền đủ và bấm xác nhận (BT_BuyItemToCart) mới thật sự đặt đơn.
        ShoppingCart.Instance.cartUI.OpenCheckoutForBuyNow(item);
    }

    // Tạo CartItem từ vật phẩm đang xem (dùng variant id/customId đã resolve).
    private CartItem BuildCurrentCartItem()
    {
        return new CartItem
        {
            customId = !string.IsNullOrEmpty(_currentVariantCustomId)
                ? _currentVariantCustomId
                : _currentDetail.customId,
            // parentCustomId = customId cấp sản phẩm gốc — đồng bộ với OnAddToCartClicked
            parentCustomId = _currentDetail.customId,
            productId = _currentProductId, // = variant.id (UUID thật) sau resolve
            productName = _currentDetail.title,
            brandName = _currentDetail.brandName,
            price = _currentDetail.price,
            selectedSize = _currentSelectedSize,
            imageUrl = _currentDetail.mainImageUrl,
            quantity = 1,
        };
    }
}