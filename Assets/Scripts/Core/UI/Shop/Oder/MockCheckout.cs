using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class MockCheckout : MonoBehaviour
{
    [Header("Mock Data Button")]
    [SerializeField] private Button mockPurchaseButton;

    private void Start()
    {
        // Gán sự kiện cho nút mock
        if (mockPurchaseButton != null)
            mockPurchaseButton.onClick.AddListener(MockPurchaseAndCheckout);
    }

    [ContextMenu("Mock Purchase & Checkout")]
    public void MockPurchaseAndCheckout()
    {
        Debug.Log("=== MOCK CHECKOUT STARTED ===");

        // Tạo mock data cart items
        List<CartItem> mockCartItems = CreateMockCartItems();

        // Thêm vào giỏ hàng thật
        foreach (var item in mockCartItems)
        {
            ShoppingCart.Instance.AddItem(item);
        }

        // Tạo mock order request theo format API
        var mockOrderRequest = CreateMockOrderRequest();

        // Gửi đơn hàng
        StartCoroutine(SendMockOrderToBackend(mockOrderRequest));
    }

    private List<CartItem> CreateMockCartItems()
    {
        List<CartItem> mockItems = new List<CartItem>();

        // Mock item 1
        CartItem item1 = new CartItem
        {
            productId = "59c1b838-0741-4a27-ab17-0d7515696139",
            customId = "8552",
            productName = "Mock Giày Nike Air",
            brandName = "Nike",
            price = 2500000f,
            selectedSize = "42",
            quantity = 1,
            imageUrl = "https://via.placeholder.com/150x150/FF0000/FFFFFF?text=Nike"
        };

        // Mock item 2
        CartItem item2 = new CartItem
        {
            productId = "f9a5c77c-1d15-4b9e-a419-d2c75a0b0e45",
            customId = "21147",
            productName = "Mock Áo Adidas",
            brandName = "Adidas",
            price = 1200000f,
            selectedSize = "L",
            quantity = 2,
            imageUrl = "https://via.placeholder.com/150x150/0000FF/FFFFFF?text=Adidas"
        };

        mockItems.Add(item1);
        mockItems.Add(item2);

        return mockItems;
    }

    private MockOrderRequest CreateMockOrderRequest()
    {
        var orderRequest = new MockOrderRequest
        {
           
            orderTypeId = "COD",
            departmentId = "62bc4cb7-51c9-4e03-662b-09a9e145dda7",
            buyerName = "NGUYEN VAN A",
            buyerPhone = "0123456789",
            recipientAddress = "12/3 bt",
            recipientCountryId = "E2C96513-1D11-4531-8E62-31CE91946556",
            recipientCountryName = "Vietnam",
            tenantCustomerCouponIds = new List<string>(),
            items = new List<MockOrderItem>()
        };

        // Thêm mock items
        orderRequest.items.Add(new MockOrderItem
        {
            tenantProductVariantId = "59c1b838-0741-4a27-ab17-0d7515696139",
            amount = 1,
            newProductSkuTitle = "42"
        });

        orderRequest.items.Add(new MockOrderItem
        {
            tenantProductVariantId = "f9a5c77c-1d15-4b9e-a419-d2c75a0b0e45",
            amount = 2,
            newProductSkuTitle = "L"
        });

        return orderRequest;
    }

    private IEnumerator SendMockOrderToBackend(MockOrderRequest orderRequest)
    {
        string url = "https://api.staging.storims.com/api/v1/RetailOrder/45A26BFC-F2B2-4CA2-AB49-9EE8E9ADCFEC/AnonymousOrder?PageIndex=0&PageSize=20";

        string jsonBody = JsonConvert.SerializeObject(orderRequest, Formatting.Indented);

        Debug.Log("=== MOCK ORDER REQUEST ===");
        Debug.Log($"URL: {url}");
        Debug.Log($"JSON Body: {jsonBody}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            Debug.Log("=== MOCK ORDER RESPONSE ===");
            Debug.Log($"Response Code: {request.responseCode}");
            Debug.Log($"Response Body: {request.downloadHandler.text}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ Mock Order sent successfully!");
                // Parse response nếu cần
                try
                {
                    var response = JsonConvert.DeserializeObject<RetailOrderResult>(request.downloadHandler.text);
                    Debug.Log($"Order ID: {response.retailOrderId}");
                    Debug.Log($"Order Number: {response.retailOrderNumber}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not parse response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"❌ Mock Order failed: {request.error}");
            }
        }
    }
}

// Classes riêng cho mock request
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
