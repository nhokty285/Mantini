using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using static MainMenuViewModel;

public class ShopItemUI : MonoBehaviour /*IPointerClickHandler*/
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI regularPriceText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button buyButton;

    [Header("Default Settings")]
    [SerializeField] private Sprite defaultItemSprite;

    [Header("Product Detail")]
    [SerializeField] private Button productDetailButton;

    private APIProductItem originalAPIItem;
/*    private Action onBuyClicked;
*/    private string currentImageUrl = "";
    private bool isLoadingImage = false;

    public ShopItem CurrentItem { get; private set; }

    [Header("🎠 NEW: Carousel Details")]
    [SerializeField] private GameObject detailPanel; // Panel chứa thông tin chi tiết
    [SerializeField] private CanvasGroup detailCanvasGroup; // Để fade in/out smooth
    [SerializeField] private Image backgroundImage; // Background để highlight
    [SerializeField] private GameObject infoPanel; // Panel chứa text elements
    //private bool isCarouselMode = false;

    public void Setup(ShopItem shopItem, Action buyCallback)
    {
        CurrentItem = shopItem;

        // Setup UI
        nameText.text = shopItem.itemName;
        priceText.text = $"{shopItem.price:N0} VND";
        if(shopItem.regularPrice > shopItem.price)
        {
            regularPriceText.gameObject.SetActive(true);
            regularPriceText.text = $"{shopItem.regularPrice:N0} VND";
        }
        else
        {
            regularPriceText.gameObject.SetActive(false);
        }   
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

        if (priceText != null)
            priceText.gameObject.SetActive(isCarouselCenter);

        if(regularPriceText != null)
            regularPriceText.gameObject.SetActive(isCarouselCenter);
  
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
            // Tính toán khoảng thời gian từ lần click trước đến hiện tại
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
            }
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

        /*// Use ImageDownloadManager instead of direct coroutine
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
            // Fallback to old method if ImageDownloadManager not available
            StartCoroutine(LoadImageFromAPI(imageUrl, shopItem));
        }*/

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

        // Only create sprite if not from cache (cache already has sprite)
        Sprite sprite;
        if (fromCache)
        {
            // Find existing sprite for this texture
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
        }
        else
        {
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
        }

        if (sprite != null)
        {
            iconImage.sprite = sprite;
            shopItem.icon = sprite; // Cache in ShopItem for future use

            string source = fromCache ? "cache" : "API";
            Debug.Log($"Applied texture from {source} for {shopItem.itemName}");
        }
    }

    public void SetDefaultSprite(Sprite defaultSprite)
    {
        defaultItemSprite = defaultSprite;
    }

    private void OnDestroy()
    {
        // Cancel any ongoing image loading
        isLoadingImage = false;
    }
}
