// CartItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CartItemUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image productImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI sizeText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button removeButton;

    [Header("Trial Info")]
    [SerializeField] private TextMeshProUGUI trialTimeText;
    [SerializeField] private GameObject trialInfoPanel;

    private CartItem _currentData;
    private bool _imageLoaded = false;
    private string _currentImageUrl = "";

    public void Setup(CartItem data)
    {
        if (data == null)
        {
            Debug.LogError("[CartItemUI] CartItem data is null!");
            return;
        }

        _currentData = data;
        UpdateUIElements(data);
        SetupEventListeners();
        LoadImageIfNeeded();
    }

    public CartItem GetCurrentItem() => _currentData;

    private void UpdateUIElements(CartItem data)
    {
        if (nameText != null) nameText.text = data.productName;
        if (sizeText != null) sizeText.text = $"Size: {data.selectedSize}";
        if (priceText != null) priceText.text = $"{data.TotalPrice:N0} VND";
        if (quantityInput != null) quantityInput.text = data.quantity.ToString();

        UpdateTrialInfo(data);
    }

    private void UpdateTrialInfo(CartItem data)
    {
        if (trialInfoPanel != null)
            trialInfoPanel.SetActive(!data.isPaid); // Chỉ hiện với unpaid items

        if (trialTimeText == null) return;

        if (!data.isPaid)
        {
            int daysRemaining = data.trialDaysRemaining;
            if (daysRemaining > 0)
            {
                trialTimeText.text = $"Trial: {daysRemaining} days left";
                trialTimeText.color = daysRemaining <= 1 ? Color.red : Color.white;
            }
            else
            {
                trialTimeText.text = "Trial Expired";
                trialTimeText.color = Color.red;
            }
        }
        else
        {
            trialTimeText.text = "Owned";
            trialTimeText.color = Color.green;
        }
    }

    private void SetupEventListeners()
    {
        // Quantity input (only for unpaid items)
        if (quantityInput != null)
        {
            quantityInput.interactable = !_currentData.isPaid;
            quantityInput.onEndEdit.RemoveAllListeners();

            if (!_currentData.isPaid)
                quantityInput.onEndEdit.AddListener(OnQuantityChanged);
        }

        // Remove button — DELETE owned item by server item_id
        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() =>
            {
                var id = _currentData?.gameItemId;
                if (!string.IsNullOrEmpty(id))
                    ShoppingCart.Instance.DeleteOwnedItemById(id);
            });
        }
    }

    public void UpdateData(CartItem data)
    {
        if (data == null) return;

        _currentData = data;
        UpdateUIElements(data);
        SetupEventListeners();

        // Reload image nếu URL changed
        if (data.imageUrl != _currentImageUrl)
        {
            _imageLoaded = false;
            _currentImageUrl = "";
            LoadImageIfNeeded();
        }
    }

    private void LoadImageIfNeeded()
    {
        if (!_imageLoaded && !string.IsNullOrEmpty(_currentData?.imageUrl))
            LoadImageIfActive();
    }

    public void LoadImageIfActive()
    {
        if (gameObject.activeInHierarchy && !string.IsNullOrEmpty(_currentData?.imageUrl))
            StartCoroutine(LoadImage(_currentData.imageUrl));
    }

    private void OnQuantityChanged(string value)
    {
        if (_currentData.isPaid) return; // Can't change quantity of paid items

        if (int.TryParse(value, out int qty) && qty > 0)
        {
            // Dùng customId làm khoá định danh (đồng bộ ShoppingCart.UpdateQuantity mới)
            ShoppingCart.Instance.UpdateQuantity(
                _currentData.customId,
                _currentData.selectedSize,
                qty);
        }
        else
        {
            quantityInput.text = _currentData.quantity.ToString();
        }
    }

    // ⚠️ NOTE: LoadImage tạo Sprite mới mỗi lần — texture cũ KHÔNG được Destroy.
    // Có thể gây memory leak nếu reload nhiều lần. Xem ProductDetailUI/DetailGalleryImage
    // để có pattern owner-tracking đúng. Ở đây giữ behavior cũ.
    private System.Collections.IEnumerator LoadImage(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success &&
                productImage != null &&
                _currentData != null &&
                _currentData.imageUrl == url)
            {
                Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                productImage.sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    Vector2.one * 0.5f);

                _imageLoaded = true;
                _currentImageUrl = url;
#if UNITY_EDITOR
                GameLog.Info($"[CartItemUI] Image loaded for {_currentData.productName}");
#endif
            }
        }
    }
}