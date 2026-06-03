/*using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using static MainMenuViewModel;

public class ShopItemUI : MonoBehaviour *//*IPointerClickHandler*//*
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    private TextMeshProUGUI priceText;
    string pendingPriceText = "";
    //[SerializeField] private TextMeshProUGUI regularPriceText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button buyButton;

    [Header("Default Settings")]
    [SerializeField] private Sprite defaultItemSprite;

    [Header("Product Detail")]
    [SerializeField] private Button productDetailButton;
    [SerializeField] private MarqueeText nameMarquee;

    private APIProductItem originalAPIItem;
*//*    private Action onBuyClicked;
*//*    private string currentImageUrl = "";
    private bool isLoadingImage = false;

    public ShopItem CurrentItem { get; private set; }

    [Header("🎠 NEW: Carousel Details")]
    [SerializeField] private GameObject detailPanel; // Panel chứa thông tin chi tiết
    [SerializeField] private CanvasGroup detailCanvasGroup; // Để fade in/out smooth
    [SerializeField] private Image backgroundImage; // Background để highlight
    [SerializeField] private GameObject infoPanel; // Panel chứa text elements
                                                   //private bool isCarouselMode = false;

    [Header("Carousel Visual")]
    [SerializeField] private Color sideBackgroundColor = new Color32(0x6D, 0x6D, 0x6D, 0xFF);

    private Color originalBackgroundColor;
    private bool hasCachedOriginalBackgroundColor;

    private void Awake()
    {
        CacheOriginalBackgroundColor();
    }

    private void CacheOriginalBackgroundColor()
    {
        if (hasCachedOriginalBackgroundColor || backgroundImage == null) return;

        originalBackgroundColor = backgroundImage.color;
        hasCachedOriginalBackgroundColor = true;
    }

    public void ApplyCarouselBackgroundState(bool isCenter)
    {
        CacheOriginalBackgroundColor();

        if (backgroundImage == null) return;

        backgroundImage.color = isCenter
            ? originalBackgroundColor
            : sideBackgroundColor;
    }

    public void SetExternalPriceText(TextMeshProUGUI external)
    {
        priceText = external;

        // ✅ Sản phẩm đầu tiên: Setup() đã chạy rồi nhưng priceText lúc đó null
        // → apply pending value ngay tại đây
        if (priceText != null && !string.IsNullOrEmpty(pendingPriceText))
        {
            priceText.text = pendingPriceText;
            priceText.gameObject.SetActive(isCarouselCenter); // sync lại visibility
        }
    }



    public void Setup(ShopItem shopItem, Action buyCallback)
    {
        CurrentItem = shopItem;

        // Setup UI
        nameText.text = shopItem.itemName;
        nameMarquee?.StartScroll();
        if (priceText != null)
            priceText.text = $"{shopItem.price:N0} VND";
        *//*  if(shopItem.regularPrice > shopItem.price)
          {
              regularPriceText.gameObject.SetActive(true);
              regularPriceText.text = $"{shopItem.regularPrice:N0} VND";
          }
          else
          {
              regularPriceText.gameObject.SetActive(false);
          }   *//*
        descriptionText.text = shopItem.description;

        SetupItemIcon(shopItem);
        SetupButton();

        UpdateInfoDisplay();
    }
    private bool isCarouselCenter = false;

    // ✅ THÊM method này vào ShopItemUI
    public void SetCarouselMode(bool isCenter)
    {
        isCarouselCenter = isCenter;
        UpdateInfoDisplay();
    }

    private void UpdateInfoDisplay()
    {
        // Hiển thị thông tin chi tiết chỉ khi ở center
        if (nameText != null)
            nameText.gameObject.SetActive(isCarouselCenter);

        if (nameText != null)
        {
            nameText.gameObject.SetActive(isCarouselCenter);
            if (isCarouselCenter) nameMarquee?.StartScroll(); // ← scroll khi hiện
            else nameMarquee?.StopScroll();                   // ← dừng khi ẩn
        }

        if (priceText != null)
            priceText.gameObject.SetActive(isCarouselCenter);

      //  if(regularPriceText != null)
          //  regularPriceText.gameObject.SetActive(isCarouselCenter);
  
        // if (brandtext != null)
        //      itemBrandText.gameObject.SetActive(isCarouselCenter);

        if (infoPanel != null)
          infoPanel.SetActive(isCarouselCenter);

        // Icon luôn hiển thị
        if (detailPanel != null)
            detailPanel.gameObject.SetActive(true);
    }

    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 0.8f; // Thời gian tối đa giữa 2 lần click
    private void SetupButton()
    {
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => {
            *//*   // Tính toán khoảng thời gian từ lần click trước đến hiện tại
               float currentTime = Time.time;
               float timeSinceLastClick = currentTime - lastClickTime;

               if (timeSinceLastClick <= doubleClickThreshold)
               {
                   // Thực hiện logic khi Double Click
                   AudioManager.Instance.PlaySFXOneShot("Button_High");
                   ShowProductDetail();

                   // Reset lại lastClickTime để tránh click lần 3 cũng tính là double click
                   lastClickTime = 0f;
               }
               else
               {
                   // Cập nhật lại thời gian cho lần click đơn này
                   lastClickTime = currentTime;
               }*//*
            AudioManager.Instance.PlaySFXOneShot("Button_High");
            ShowProductDetail();
        });

        var buttonText = buyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.text = "Xem chi tiết";
    }

    public void SetAPIData(APIProductItem apiItem)
    {
        originalAPIItem = apiItem;
    }

    private void ShowProductDetail()
    {
        if (ProductDetailUI.Instance != null && originalAPIItem != null)
        {
            ProductDetailUI.Instance.ShowProductDetail(originalAPIItem);
            TutorialGamePlay.Instance?.OnPlayerTappedItem();
        }
        else
        {
            Debug.LogWarning("ProductDetailUI not found or API data not available");
        }
    }

    private void SetupItemIcon(ShopItem shopItem)
    {
        // Check if we already have an icon
        if (shopItem.icon != null)
        {
            iconImage.sprite = shopItem.icon;
            Debug.Log($"Used existing icon for {shopItem.itemName}");
            return;
        }

        // Set default icon first
        SetDefaultIcon();

        // Try to load from cache or API
        if (!string.IsNullOrEmpty(shopItem.imageUrl))
        {
            LoadImageWithCache(shopItem.imageUrl, shopItem);
        }
    }

    private void SetDefaultIcon()
    {
        if (defaultItemSprite != null)
        {
            iconImage.sprite = defaultItemSprite;
        }
        else
        {
            Debug.LogWarning("Default item sprite is not assigned!");
        }
    }

    private void LoadImageWithCache(string imageUrl, ShopItem shopItem)
    {
        if (isLoadingImage || string.IsNullOrEmpty(imageUrl) || imageUrl == currentImageUrl)
            return;

        currentImageUrl = imageUrl;
        isLoadingImage = true;
        StartCoroutine(DelayedImageLoad(imageUrl, shopItem));
    }

    private IEnumerator DelayedImageLoad(string imageUrl, ShopItem shopItem)
    {
        // Delay ngẫu nhiên 0-1s để spread load
        float delay = UnityEngine.Random.Range(0f, 0.2f);
        yield return new WaitForSeconds(delay);

        if (ImageDownloadManager.Instance != null)
        {
            ImageDownloadManager.Instance.DownloadImage(
                imageUrl,
                texture => {
                    isLoadingImage = false;
                    ApplyTextureToIcon(texture, shopItem, imageUrl, false);
                },
                error => {
                    isLoadingImage = false;
                    Debug.LogWarning($"Failed to load image for {shopItem.itemName}: {error}");
                }
            );
        }
        else
        {
            StartCoroutine(LoadImageFromAPI(imageUrl, shopItem));
        }
    }

    private IEnumerator LoadImageFromAPI(string imageUrl, ShopItem shopItem)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            request.timeout = 10; // 10 second timeout
            yield return request.SendWebRequest();

            isLoadingImage = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null && texture.width > 0 && texture.height > 0)
                {
                    // Cache the texture
                    if (CacheService.Instance != null)
                    {
                        TimeSpan cacheExpiry = TimeSpan.FromMinutes(30); // Cache images for 30 minutes
                        CacheService.Instance.SetTexture(imageUrl, texture, cacheExpiry);
                    }

                    ApplyTextureToIcon(texture, shopItem, imageUrl, false);
                }
                else
                {
                    Debug.LogWarning($"Invalid texture for {shopItem.itemName}");
                }
            }
            else
            {
                Debug.LogWarning($"Failed to load image from API for {shopItem.itemName}: {request.error}");
            }
        }
    }

    private void ApplyTextureToIcon(Texture2D texture, ShopItem shopItem, string imageUrl, bool fromCache)
    {
        if (texture == null || shopItem == null) return;

        Sprite sprite = Sprite.Create(texture,
         new Rect(0, 0, texture.width, texture.height),
         Vector2.one * 0.5f);

        if (sprite != null)
        {
            iconImage.sprite = sprite;
            shopItem.icon = sprite;
            ownsSprite = true;
        }
    }

    public void SetDefaultSprite(Sprite defaultSprite)
    {
        defaultItemSprite = defaultSprite;
    }
    private bool ownsSprite = false;
    private void OnDestroy()
    {
        // Cancel any ongoing image loading
        isLoadingImage = false;
        if (ownsSprite && iconImage != null && iconImage.sprite != null)
        {
            Destroy(iconImage.sprite);
        }
    }


}
*/
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel;

public class ShopItemUI : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  UI References
    // ─────────────────────────────────────────────
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button buyButton;

    [Header("Default Settings")]
    [SerializeField] private Sprite defaultItemSprite;

    [Header("Product Detail")]
    [SerializeField] private Button productDetailButton;
    [SerializeField] private MarqueeText nameMarquee;

    [Header("Carousel Details")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private CanvasGroup detailCanvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject infoPanel;

    [Header("Carousel Visual")]
    [SerializeField] private Color sideBackgroundColor = new Color32(0x6D, 0x6D, 0x6D, 0xFF);

    // ─────────────────────────────────────────────
    //  Private state
    // ─────────────────────────────────────────────
    // Price text is injected externally (shared UI element across carousel slots)
    private TextMeshProUGUI priceText;
    private string pendingPriceText = "";

    private APIProductItem originalAPIItem;
    private bool isCarouselCenter = false;

    // Sprite ownership:
    //   - We own the Sprite we created via Sprite.Create().
    //   - We NEVER own the Texture2D — it lives in CacheService.
    //   - We do NOT cache sprite on ShopItem to avoid dead-sprite race conditions
    //     when multiple carousel slots reference the same ShopItem.
    private bool _ownsSprite = false;
    private bool _loadRequested = false;
    private string _loadedUrl = "";

    // Background color cache
    private Color _originalBgColor;
    private bool _hasCachedBgColor;

    // ─────────────────────────────────────────────
    //  Public
    // ─────────────────────────────────────────────
    public ShopItem CurrentItem { get; private set; }

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────
    private void Awake()
    {
        CacheBgColor();
    }

    private void OnDestroy()
    {
        ReleaseSprite();
    }

    // ─────────────────────────────────────────────
    //  Setup Entry Point
    // ─────────────────────────────────────────────
    /// <summary>
    /// Called by ShopController.SetupSlot() every time this carousel slot is
    /// reused for a (potentially different) ShopItem. Resets all image state first.
    /// </summary>
    public void Setup(ShopItem shopItem, Action buyCallback)
    {
        CurrentItem = shopItem;

        nameText.text = shopItem.itemName;
        nameMarquee?.StartScroll();

        pendingPriceText = $"{shopItem.price:N0} VND";
        if (priceText != null)
            priceText.text = pendingPriceText;

        descriptionText.text = shopItem.description;

        SetupItemIcon(shopItem);
        SetupButton();
        UpdateInfoDisplay();
    }

    // ─────────────────────────────────────────────
    //  External Injections (called by ShopController)
    // ─────────────────────────────────────────────
    /// <summary>
    /// ShopController injects the shared external price TMP for the center slot.
    /// </summary>
    public void SetExternalPriceText(TextMeshProUGUI external)
    {
        priceText = external;
        if (priceText != null && !string.IsNullOrEmpty(pendingPriceText))
        {
            priceText.text = pendingPriceText;
            priceText.gameObject.SetActive(isCarouselCenter);
        }
    }

    public void SetAPIData(APIProductItem apiItem)
    {
        originalAPIItem = apiItem;
    }

    public void SetDefaultSprite(Sprite defaultSprite)
    {
        defaultItemSprite = defaultSprite;
    }

    // ─────────────────────────────────────────────
    //  Carousel Visual State
    // ─────────────────────────────────────────────
    public void SetCarouselMode(bool isCenter)
    {
        isCarouselCenter = isCenter;
        UpdateInfoDisplay();
    }

    public void ApplyCarouselBackgroundState(bool isCenter)
    {
        CacheBgColor();
        if (backgroundImage == null) return;
        backgroundImage.color = isCenter ? _originalBgColor : sideBackgroundColor;
    }

    private void UpdateInfoDisplay()
    {
        if (nameText != null)
        {
            nameText.gameObject.SetActive(isCarouselCenter);
            if (isCarouselCenter) nameMarquee?.StartScroll();
            else nameMarquee?.StopScroll();
        }

        if (priceText != null)
            priceText.gameObject.SetActive(isCarouselCenter);

        if (infoPanel != null)
            infoPanel.SetActive(isCarouselCenter);

        // Icon container always visible
        if (detailPanel != null)
            detailPanel.SetActive(true);
    }

    private void CacheBgColor()
    {
        if (_hasCachedBgColor || backgroundImage == null) return;
        _originalBgColor = backgroundImage.color;
        _hasCachedBgColor = true;
    }

    // ─────────────────────────────────────────────
    //  Button
    // ─────────────────────────────────────────────
    private void SetupButton()
    {
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySFXOneShot("Button_High");
            ShowProductDetail();
        });

        var buttonText = buyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.text = "Xem chi tiết";
    }

    private void ShowProductDetail()
    {
        if (ProductDetailUI.Instance != null && originalAPIItem != null)
        {
            ProductDetailUI.Instance.ShowProductDetail(originalAPIItem);
            TutorialGamePlay.Instance?.OnPlayerTappedItem();
        }
        else
        {
            Debug.LogWarning("[ShopItemUI] ProductDetailUI not found or API data not available");
        }
    }

    // ─────────────────────────────────────────────
    //  Image Loading
    // ─────────────────────────────────────────────

    /// <summary>
    /// Always starts a fresh load. Does NOT reuse shopItem.icon cache to avoid
    /// dead-sprite race conditions across carousel slots sharing the same ShopItem.
    /// ImageDownloadManager returns texture from CacheService instantly on repeat
    /// requests, so the cost is only one Sprite.Create() — acceptable.
    /// </summary>
    private void SetupItemIcon(ShopItem shopItem)
    {
        _loadRequested = false;
        _loadedUrl = "";
        ReleaseSprite();

        // Show placeholder immediately while download is in flight
        SetDefaultIcon();

        if (!string.IsNullOrEmpty(shopItem.imageUrl))
            RequestDownload(shopItem.imageUrl, shopItem);
    }

    private void SetDefaultIcon()
    {
        if (defaultItemSprite != null)
            iconImage.sprite = defaultItemSprite;
        else
            Debug.LogWarning("[ShopItemUI] defaultItemSprite is not assigned!");
    }

    private void RequestDownload(string url, ShopItem shopItem)
    {
        if (_loadRequested || string.IsNullOrEmpty(url)) return;
        _loadRequested = true;

        if (ImageDownloadManager.Instance == null)
        {
            Debug.LogError("[ShopItemUI] ImageDownloadManager.Instance is null — image will not load.");
            _loadRequested = false;
            return;
        }

        ImageDownloadManager.Instance.DownloadImage(
            url,
            texture =>
            {
                _loadRequested = false;

                // Guard: slot destroyed or disabled before callback fired
                if (texture == null) return;
                if (iconImage == null || !iconImage.isActiveAndEnabled) return;

                // Guard: this slot was reassigned to a different item while waiting
                if (CurrentItem != shopItem) return;

                ApplyTexture(texture, url);
            },
            error =>
            {
                _loadRequested = false;
                Debug.LogWarning($"[ShopItemUI] Load failed '{shopItem?.itemName}' | {url} | {error}");
            }
        );
    }

    /// <summary>
    /// Creates a Sprite from the shared Texture2D delivered by ImageDownloadManager.
    /// Texture ownership stays with CacheService — we only own the Sprite wrapper.
    /// Sprite is NOT cached on ShopItem: doing so causes dead-sprite bugs when
    /// multiple carousel slots reference the same ShopItem and one slot destroys
    /// its sprite while another slot still holds a reference to it.
    /// </summary>
    private void ApplyTexture(Texture2D texture, string url)
    {
        ReleaseSprite();

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            Vector2.one * 0.5f
        );

        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;

        _ownsSprite = true;
        _loadedUrl = url;
    }

    // ─────────────────────────────────────────────
    //  Sprite Lifecycle — ONLY destroy Sprite, NEVER Texture
    // ─────────────────────────────────────────────

    /// <summary>
    /// Destroys only the Sprite this slot created.
    /// Texture2D is intentionally untouched — it lives in CacheService.
    /// </summary>
    private void ReleaseSprite()
    {
        if (!_ownsSprite) return;

        if (iconImage != null && iconImage.sprite != null)
        {
            Destroy(iconImage.sprite);
            iconImage.sprite = null;
        }

        _ownsSprite = false;
    }
}