using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CartImageItem : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Image productImage;
    [SerializeField] private Button button;
    [SerializeField] private Image highlightProduct;
    [SerializeField] private TextMeshProUGUI selectedSize;
    [SerializeField] private TextMeshProUGUI quality;

    [Header("Added-to-Cart Indicator")]
    [SerializeField] private GameObject addedToCartIndicator;
    [SerializeField] private Image addedToCartIcon;

    [SerializeField] private CartItem itemData;

    private Action<CartItem> _onClickCallback;
    private CartUI _cartUI;
    private bool _isHighlighted = false;
    private Outline _cachedOutline; // Refactor: cache Outline thay vì GetComponent mỗi lần

    private static CartImageItem _currentHighlightedItem;
    private static readonly HashSet<CartImageItem> _highlighted = new HashSet<CartImageItem>();

    public void OnPointerDown(PointerEventData eventData) => _cartUI?.BeginLongPress(itemData);
    public void OnPointerUp(PointerEventData eventData) => _cartUI?.CancelLongPress();
    public void OnPointerExit(PointerEventData eventData) => _cartUI?.CancelLongPress();

    private void Awake()
    {
        // Refactor: cache Outline 1 lần
        if (productImage != null)
            _cachedOutline = productImage.GetComponent<Outline>();
    }

    public void Setup(CartItem item, Action<CartItem> clickCallback, CartUI cartUIRef = null)
    {
        itemData = item;
        _onClickCallback = clickCallback;
        _cartUI = cartUIRef;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFXOneShot("Button_High");
                _onClickCallback?.Invoke(itemData);
            });
        }

        InitializeHighlight();

        if (itemData != null && itemData.isSelectedForCheckout)
            SetHighlight(true);

        LoadImage();
        UpdatePriceDisplay();
        UpdateQualityDisplay();
        UpdateAddedToCartIndicator();
    }

    private void UpdateAddedToCartIndicator()
    {
        bool isAddedToCart = itemData != null && itemData.isSelectedForCheckout;

        if (addedToCartIndicator != null)
            addedToCartIndicator.SetActive(isAddedToCart);

        if (addedToCartIcon != null)
            addedToCartIcon.color = isAddedToCart ? Color.green : Color.gray;

        // Refactor: dùng cached _cachedOutline (set ở Awake) thay vì GetComponent<Outline>() mỗi call
        if (_cachedOutline != null)
        {
            _cachedOutline.enabled = isAddedToCart;
            if (isAddedToCart)
            {
                _cachedOutline.effectColor = Color.green;
                _cachedOutline.effectDistance = new Vector2(2, 2);
            }
        }
    }

    public void RefreshCartIndicator() => UpdateAddedToCartIndicator();

    private void UpdatePriceDisplay()
    {
        if (selectedSize != null && itemData != null)
            selectedSize.text = itemData.selectedSize ?? string.Empty;
    }

    private void UpdateQualityDisplay()
    {
        if (quality != null && itemData != null)
            quality.text = itemData.quantity.ToString();
    }

    private void InitializeHighlight()
    {
        if (highlightProduct != null)
        {
            highlightProduct.gameObject.SetActive(false);
            _isHighlighted = false;
        }

        if (addedToCartIndicator != null)
            addedToCartIndicator.SetActive(false);
    }

    public void SelectThisItem()
    {
        // Tắt highlight của item trước đó (nếu có)
        if (_currentHighlightedItem != null && _currentHighlightedItem != this)
            _currentHighlightedItem.SetHighlight(false);

        SetHighlight(true);
        _currentHighlightedItem = this;
    }

    public void ToggleHighlightMultiSelect()
    {
        // SetHighlight không đụng đến currentHighlightedItem của item khác
        SetHighlight(!_isHighlighted);
    }

    public void SetHighlightVisual(bool value) => SetHighlight(value);

    public static IReadOnlyCollection<CartImageItem> GetHighlightedItems() => _highlighted;

    public void SetHighlight(bool highlight)
    {
        if (highlightProduct == null) return;

        _isHighlighted = highlight;
        highlightProduct.gameObject.SetActive(_isHighlighted);

        if (highlight) _highlighted.Add(this);
        else _highlighted.Remove(this);

        if (!highlight && _currentHighlightedItem == this)
            _currentHighlightedItem = null;
    }

    public bool IsHighlighted() => _isHighlighted;

    public static void ClearAllHighlights()
    {
        // Snapshot để tránh modify collection trong loop
        var toClean = new List<CartImageItem>(_highlighted);
        foreach (var item in toClean)
            item?.SetHighlight(false);
        _currentHighlightedItem = null;
    }

    private void OnDestroy()
    {
        _highlighted.Remove(this);
        if (_currentHighlightedItem == this)
            _currentHighlightedItem = null;
    }

    private void LoadImage()
    {
        if (productImage == null || itemData == null) return;

        // Set default/placeholder first
        productImage.sprite = null;
        productImage.color = Color.gray;

        if (string.IsNullOrEmpty(itemData.imageUrl)) return;

        // Ưu tiên ImageDownloadManager (shared cache) nếu có
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
                error => GameLog.Warn($"[CartImageItem] Failed to load image: {error}")
            );
        }
        else
        {
            // Fallback to coroutine nếu chưa có manager
            StartCoroutine(LoadImageFromURL(itemData.imageUrl));
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