using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Dev tool — gửi mock order tới backend để test flow checkout.
/// ⚠️ Không attach prefab này vào production scene.
/// </summary>
public class MockCheckout : MonoBehaviour
{
    [Header("Mock Data Button")]
    [SerializeField] private Button mockPurchaseButton;

    private void Start()
    {
        if (mockPurchaseButton != null)
        {
            mockPurchaseButton.onClick.RemoveAllListeners();
            mockPurchaseButton.onClick.AddListener(MockPurchaseAndCheckout);
        }
    }

    [ContextMenu("Mock Purchase & Checkout")]
    public void MockPurchaseAndCheckout()
    {
#if UNITY_EDITOR
        GameLog.Info("[MockCheckout] === MOCK CHECKOUT STARTED ===");
#endif

        // Tạo mock data cart items
        List<CartItem> mockCartItems = CreateMockCartItems();

        // Thêm vào giỏ hàng thật
        foreach (var item in mockCartItems)
            ShoppingCart.Instance.AddItem(item);

        // Tạo mock order request theo format API
        var mockOrderRequest = CreateMockOrderRequest();

        // Gửi đơn hàng
        StartCoroutine(SendMockOrderToBackend(mockOrderRequest));
    }

    private static List<CartItem> CreateMockCartItems()
    {
        // Pre-allocate capacity = 2
        var mockItems = new List<CartItem>(2)
        {
            new CartItem
            {
                productId = "59c1b838-0741-4a27-ab17-0d7515696139",
                customId = "8552",
                productName = "Mock Giày Nike Air",
                brandName = "Nike",
                price = 2500000f,
                selectedSize = "42",
                quantity = 1,
                imageUrl = "https://via.placeholder.com/150x150/FF0000/FFFFFF?text=Nike"
            },
            new CartItem
            {
                productId = "f9a5c77c-1d15-4b9e-a419-d2c75a0b0e45",
                customId = "21147",
                productName = "Mock Áo Adidas",
                brandName = "Adidas",
                price = 1200000f,
                selectedSize = "L",
                quantity = 2,
                imageUrl = "https://via.placeholder.com/150x150/0000FF/FFFFFF?text=Adidas"
            }
        };

        return mockItems;
    }

    private static MockOrderRequest CreateMockOrderRequest()
    {
        return new MockOrderRequest
        {
            orderTypeId = "COD",
            departmentId = "62bc4cb7-51c9-4e03-662b-09a9e145dda7",
            buyerName = "NGUYEN VAN A",
            buyerPhone = "0123456789",
            recipientAddress = "12/3 bt",
            recipientCountryId = "E2C96513-1D11-4531-8E62-31CE91946556",
            recipientCountryName = "Vietnam",
            tenantCustomerCouponIds = new List<string>(),
            items = new List<MockOrderItem>(2)
            {
                new MockOrderItem
                {
                    tenantProductVariantId = "59c1b838-0741-4a27-ab17-0d7515696139",
                    amount = 1,
                    newProductSkuTitle = "42"
                },
                new MockOrderItem
                {
                    tenantProductVariantId = "f9a5c77c-1d15-4b9e-a419-d2c75a0b0e45",
                    amount = 2,
                    newProductSkuTitle = "L"
                }
            }
        };
    }

    private IEnumerator SendMockOrderToBackend(MockOrderRequest orderRequest)
    {
        const string url = "https://api.staging.storims.com/api/v1/RetailOrder/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/AnonymousOrder?PageIndex=0&PageSize=20";
        string jsonBody = JsonConvert.SerializeObject(orderRequest, Formatting.Indented);

#if UNITY_EDITOR
        GameLog.Info($"[MockCheckout] === MOCK ORDER REQUEST === URL: {url}");
        GameLog.Info($"[MockCheckout] JSON Body: {jsonBody}");
#endif

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

#if UNITY_EDITOR
            GameLog.Info($"[MockCheckout] === RESPONSE === code={request.responseCode}");
            GameLog.Info($"[MockCheckout] Body: {request.downloadHandler.text}");
#endif

            if (request.result == UnityWebRequest.Result.Success)
            {
                GameLog.Info("[MockCheckout] ✅ Mock Order sent successfully!");
                try
                {
                    var response = JsonConvert.DeserializeObject<RetailOrderResult>(request.downloadHandler.text);
#if UNITY_EDITOR
                    GameLog.Info($"[MockCheckout] Order ID: {response.retailOrderId}");
                    GameLog.Info($"[MockCheckout] Order Number: {response.retailOrderNumber}");
#endif
                }
                catch (System.Exception e)
                {
                    GameLog.Warn($"[MockCheckout] Could not parse response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[MockCheckout] ❌ Mock Order failed: {request.error}");
            }
        }
    }
}

// ⚠️ Note: MockOrderRequest/MockOrderItem duplicate schema với RetailOrderRequest/CartOrderItem
// trong ShoppingCart.cs. Có thể merge bằng cách dùng RetailOrderRequest cho mock — nhưng để giữ
// tách biệt dev/prod data, vẫn giữ riêng.

[System.Serializable]
public class MockOrderRequest
{
    public string orderTypeId;
    public string departmentId;
    public string buyerName;
    public string buyerPhone;
    public List<MockOrderItem> items;
    public string recipientAddress;
    public string recipientCountryId;
    public string recipientCountryName;
    public List<string> tenantCustomerCouponIds;
}

[System.Serializable]
public class MockOrderItem
{
    public string tenantProductVariantId;
    public int amount;
    public string newProductSkuTitle;
}