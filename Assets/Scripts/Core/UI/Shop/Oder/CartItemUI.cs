// CartItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using static UnityEditor.Profiling.HierarchyFrameDataView;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using Unity.VisualScripting;

public class CartItemUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image productImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI sizeText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button removeButton;

    private CartItem currentData;

    private bool imageLoaded = false; // Track image loading state
    private string currentImageUrl = ""; // Cache current image URL

    [Header("✅ NEW: Trial Info")]
    [SerializeField] private TextMeshProUGUI trialTimeText; // Hiển thị thời gian trial
    [SerializeField] private GameObject trialInfoPanel; // Panel chứa thông tin trial
    /* // Hàm này được CartUI gọi
     public void Setup(CartItem data)
     {
         if (data == null)
         {
             Debug.LogError("CartItem data is null!");
             return;
         }
         currentData = data;

         nameText.text = data.productName;
         sizeText.text = $"Size: {data.selectedSize}";
         priceText.text = $"{data.TotalPrice:N0} VND";
         quantityInput.text = data.quantity.ToString();

         // Load ảnh (tùy chọn)
         if (!string.IsNullOrEmpty(data.imageUrl))
             StartCoroutine(LoadImage(data.imageUrl));

         // Bind sự kiện
         quantityInput.onEndEdit.RemoveAllListeners();
         quantityInput.onEndEdit.AddListener(OnQuantityChanged);

         removeButton.onClick.RemoveAllListeners();
         removeButton.onClick.AddListener(RemoveSelf);

     }

     // ✅ NEW: Update data without recreating UI
     public void UpdateData(CartItem data)
     {
         if (data == null) return;

         currentData = data;
         UpdateUIElements(data);

         // ✅ Chỉ reload ảnh nếu URL khác
         if (data.imageUrl != currentImageUrl)
         {
             imageLoaded = false;
             currentImageUrl = "";
             LoadImageIfActive();
         }
     }

     public void LoadImageIfActive()
     {
         if (gameObject.activeInHierarchy && !string.IsNullOrEmpty(currentData?.imageUrl))
         {
             StartCoroutine(LoadImage(currentData.imageUrl));
         }
     }

     private void UpdateUIElements(CartItem data)
     {
         // Update text elements
         if (nameText != null)
             nameText.text = data.productName;
         if (sizeText != null)
             sizeText.text = $"Size: {data.selectedSize}";
         if (priceText != null)
             priceText.text = $"{data.TotalPrice:N0} VND";
         if (quantityInput != null)
         {
             quantityInput.text = data.quantity.ToString();
             quantityInput.onEndEdit.RemoveAllListeners();
             quantityInput.onEndEdit.AddListener(OnQuantityChanged);
         }
         if (removeButton != null)
         {
             removeButton.onClick.RemoveAllListeners();
             removeButton.onClick.AddListener(RemoveSelf);
         }
     }

     private void OnQuantityChanged(string value)
     {
         if (int.TryParse(value, out int qty) && qty > 0)
         {
             ShoppingCart.Instance.UpdateQuantity(
                 currentData.productId,
                 currentData.selectedSize,
                 qty);
         }
         else
         {
             // Khôi phục giá trị hợp lệ
             quantityInput.text = currentData.quantity.ToString();
         }
     }

     private void RemoveSelf()
     {
         ShoppingCart.Instance.RemoveItem(
             currentData.productId,
             currentData.selectedSize);
     }

     private System.Collections.IEnumerator LoadImage(string url)
     {
         using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
         {
             yield return req.SendWebRequest();
             if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success &&
                 productImage != null &&
                 currentData != null &&
                 currentData.imageUrl == url)
             {
                 Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                 productImage.sprite = Sprite.Create(tex,
                     new Rect(0, 0, tex.width, tex.height),
                     Vector2.one * 0.5f);

                 imageLoaded = true; // ✅ Mark as loaded
                 Debug.Log($"Image loaded for {currentData.productName}");
             }
         }
     }*/



    public void Setup(CartItem data)
    {
        if (data == null)
        {
            Debug.LogError("CartItem data is null!");
            return;
        }

        currentData = data;
        UpdateUIElements(data);
        SetupEventListeners();
        LoadImageIfNeeded();
    }

    // ✅ THÊM: Get current item for selection
    public CartItem GetCurrentItem()
    {
        return currentData;
    }

    private void UpdateUIElements(CartItem data)
    {
        // Basic info
        if (nameText != null)
            nameText.text = data.productName;
        if (sizeText != null)
            sizeText.text = $"Size: {data.selectedSize}";
        if (priceText != null)
            priceText.text = $"{data.TotalPrice:N0} VND";
        if (quantityInput != null)
            quantityInput.text = data.quantity.ToString();

        // ✅ THÊM: Trial time display
        UpdateTrialInfo(data);

    }


    // ✅ THÊM: Update trial information
    private void UpdateTrialInfo(CartItem data)
    {
        if (trialInfoPanel != null)
        {
            trialInfoPanel.SetActive(!data.isPaid); // Chỉ hiện với unpaid items
        }

        if (trialTimeText != null)
        {
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
    }

    private void SetupEventListeners()
    {
        // Quantity input (only for unpaid items)
        if (quantityInput != null)
        {
            quantityInput.interactable = !currentData.isPaid;
            quantityInput.onEndEdit.RemoveAllListeners();

            if (!currentData.isPaid)
            {
                quantityInput.onEndEdit.AddListener(OnQuantityChanged);
            }
        }

        // Remove button (only for unpaid items)
        if (removeButton != null)
        {
            //removeButton.gameObject.SetActive(!currentData.isPaid);
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() =>
            {
                // Với item đã sở hữu (isPaid == true), xóa bằng item_id do server tạo
                var id = currentData?.gameItemId;   // ✅ dùng currentData thay cho boundCartItem
                if (!string.IsNullOrEmpty(id))
                    ShoppingCart.Instance.DeleteOwnedItemById(id);  // gọi DELETE và cập nhật UI
            });

            /*if (!currentData.isPaid)
            {
                removeButton.onClick.AddListener(RemoveSelf);
            }*/
        }
    }

    public void UpdateData(CartItem data)
    {
        if (data == null) return;

        currentData = data;
        UpdateUIElements(data);
        SetupEventListeners();

        // Reload image if URL changed
        if (data.imageUrl != currentImageUrl)
        {
            imageLoaded = false;
            currentImageUrl = "";
            LoadImageIfNeeded();
        }
    }

    private void LoadImageIfNeeded()
    {
        if (!imageLoaded && !string.IsNullOrEmpty(currentData?.imageUrl))
        {
            LoadImageIfActive();
        }
    }

    public void LoadImageIfActive()
    {
        if (gameObject.activeInHierarchy && !string.IsNullOrEmpty(currentData?.imageUrl))
        {
            StartCoroutine(LoadImage(currentData.imageUrl));
        }
    }

    private void OnQuantityChanged(string value)
    {
        if (currentData.isPaid) return; // Can't change quantity of paid items

        if (int.TryParse(value, out int qty) && qty > 0)
        {
            ShoppingCart.Instance.UpdateQuantity(
                currentData.productId,
                currentData.selectedSize,
                qty);
        }
        else
        {
            quantityInput.text = currentData.quantity.ToString();
        }
    }

    private void RemoveSelf()
    {
        if (currentData.isPaid) return; // Can't remove paid items

        ShoppingCart.Instance.RemoveItem(
            currentData.productId,
            currentData.selectedSize);
    }

    private System.Collections.IEnumerator LoadImage(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success &&
                productImage != null &&
                currentData != null &&
                currentData.imageUrl == url)
            {
                Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                productImage.sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    Vector2.one * 0.5f);

                imageLoaded = true;
                currentImageUrl = url;
                Debug.Log($"Image loaded for {currentData.productName}");
            }
        }
    }

    // Ví dụ trong handler của nút Remove
  /*  public void OnRemoveClicked()
    {
        if (string.IsNullOrEmpty(boundItem?.gameItemId))
        {
            Debug.LogWarning("No gameItemId to delete");
            return;
        }
        ShoppingCart.Instance.DeleteOwnedItemById(boundItem.gameItemId);
    }*/

}