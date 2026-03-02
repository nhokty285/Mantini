using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Networking;

public class CartImageItem : MonoBehaviour
{
    [SerializeField] private Image productImage;
    [SerializeField] private Button button;
    [SerializeField] private Image highlightProduct;
    [SerializeField] private TextMeshProUGUI selectedSize;
    [SerializeField] private TextMeshProUGUI quality;

    // ✅ THÊM: Visual indicator cho items đã thêm vào giỏ
    [SerializeField] private GameObject addedToCartIndicator; // Icon/Image overlay
    [SerializeField] private Image addedToCartIcon; // Icon cụ thể (shopping cart, check mark, etc.)

    [SerializeField] private CartItem itemData;
    private Action<CartItem> onClickCallback;
    private bool isHighlighted = false;

    private static CartImageItem currentHighlightedItem;
    private static readonly HashSet<CartImageItem> _highlighted = new HashSet<CartImageItem>();

    public void Setup(CartItem item, Action<CartItem> clickCallback)
    {
        itemData = item;
        onClickCallback = clickCallback;

        // Setup click event
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySFXOneShot("Button_High");
                SelectThisItem();
                onClickCallback?.Invoke(itemData);
            });
        }


        InitializeHighlight();

        // Restore highlight state khi item được rebuild sau SelectAll
        if (itemData != null && itemData.isSelectedForCheckout)
            SetHighlight(true);

        // Load image
        LoadImage();
        UpdatePriceDisplay();
        UpdateQualityDisplay();
        // ✅ THÊM: Cập nhật visual indicator
        UpdateAddedToCartIndicator();
    }

    // ✅ THÊM: Method cập nhật visual indicator
    private void UpdateAddedToCartIndicator()
    {
        bool isAddedToCart = itemData != null && itemData.isSelectedForCheckout;

        // Hiển thị/ẩn indicator overlay
        if (addedToCartIndicator != null)
        {
            addedToCartIndicator.SetActive(isAddedToCart);
        }

        // Thay đổi màu sắc icon
        if (addedToCartIcon != null)
        {
            addedToCartIcon.color = isAddedToCart ? Color.green : Color.gray;
        }

        // Thêm border highlight cho product image
        if (isAddedToCart)
        {
            if (productImage != null)
            {
                // Thêm outline component nếu có
                var outline = productImage.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = true;
                    outline.effectColor = Color.green;
                    outline.effectDistance = new Vector2(2, 2);
                }
            }
        }
        else
        {
            // Tắt các hiệu ứng khi chưa thêm vào giỏ
            if (productImage != null)
            {
                var outline = productImage.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                }
            }
        }
    }

    // ✅ THÊM: Public method để refresh indicator từ bên ngoài
    public void RefreshCartIndicator()
    {
        UpdateAddedToCartIndicator();
    }

    private void UpdatePriceDisplay()
    {
        if (selectedSize != null && itemData != null)
        {
            // Hiển thị giá nguyên từ API, không format
            selectedSize.text = itemData.selectedSize.ToString();
        }
    }

    private void UpdateQualityDisplay()
    {
        if (quality != null && itemData != null)
        {
            //Hiển thị số lượng nguyên từ API, không format
            quality.text = itemData.quantity.ToString();
        }
    }

    private void InitializeHighlight()
    {
        if (highlightProduct != null)
        {
            highlightProduct.gameObject.SetActive(false);
            isHighlighted = false;
        }

        // ✅ THÊM: Khởi tạo indicator ở trạng thái ẩn
        if (addedToCartIndicator != null)
        {
            addedToCartIndicator.SetActive(false);
        }
    }

    private void SelectThisItem()
    {
        // Tắt highlight của item trước đó (nếu có)
        if (currentHighlightedItem != null && currentHighlightedItem != this)
        {
            currentHighlightedItem.SetHighlight(false);
        }

        // Bật highlight cho item này
        SetHighlight(true);

        // Cập nhật reference
        currentHighlightedItem = this;
    }

    public void SetHighlight(bool highlight)
    {
        if (highlightProduct == null) return;

        isHighlighted = highlight;
        highlightProduct.gameObject.SetActive(isHighlighted);

        if (highlight) _highlighted.Add(this);
        else _highlighted.Remove(this);

        if (!highlight && currentHighlightedItem == this)
            currentHighlightedItem = null;
    }

    public bool IsHighlighted()
    {
        return isHighlighted;
    }

    // Method để clear tất cả highlight (single hoặc multi)
    public static void ClearAllHighlights()
    {
        var toClean = new List<CartImageItem>(_highlighted);
        foreach (var item in toClean)
            item?.SetHighlight(false);
        // _highlighted đã empty sau khi SetHighlight(false) remove từng item
        currentHighlightedItem = null;
    }

    private void OnDestroy()
    {
        _highlighted.Remove(this);
        if (currentHighlightedItem == this)
            currentHighlightedItem = null;
    }

    private void LoadImage()
    {
        if (productImage == null || itemData == null) return;

        // Set default/placeholder first
        productImage.sprite = null;
        productImage.color = Color.gray;

        if (!string.IsNullOrEmpty(itemData.imageUrl))
        {
            // Use existing ImageDownloadManager if available
            if (ImageDownloadManager.Instance != null)
            {
                ImageDownloadManager.Instance.DownloadImage(
                    itemData.imageUrl,
                    texture =>
                    {
                        if (productImage != null && texture != null)
                        {
                            productImage.sprite = Sprite.Create(texture,
                                new Rect(0, 0, texture.width, texture.height),
                                Vector2.one * 0.5f);
                            productImage.color = Color.white;
                        }
                    },
                    error => Debug.LogWarning($"Failed to load image: {error}")
                );
            }
            else
            {
                // Fallback to coroutine
                StartCoroutine(LoadImageFromURL(itemData.imageUrl));
            }
        }
    }

    private System.Collections.IEnumerator LoadImageFromURL(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && productImage != null)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null)
                {
                    productImage.sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height),
                        Vector2.one * 0.5f);
                    productImage.color = Color.white;
                }
            }
        }
    }
    public CartItem GetCurrentItem() => itemData;
}
