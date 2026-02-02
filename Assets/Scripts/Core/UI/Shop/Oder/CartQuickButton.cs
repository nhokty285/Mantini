using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CartQuickButton : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Button openCartButton;
    [SerializeField] private GameObject notificationBadge; // Chấm đỏ
    [SerializeField] private TextMeshProUGUI countText;    // Số lượng (Tùy chọn)

    [Header("Target UI")]
    [SerializeField] private GameObject cartPanelToOpen;   // Panel CartUI cần mở

    private void Start()
    {
        // 1. Đăng ký sự kiện click
        if (openCartButton != null)
        {
            openCartButton.onClick.AddListener(() =>
            {
                OnOpenCartClicked();
                AudioManager.Instance.PlaySFXOneShot("Button");
            });
        }

        // 2. Đăng ký lắng nghe thay đổi từ ShoppingCart
        if (ShoppingCart.Instance != null)
        {
            // Lắng nghe thay đổi tổng quát hoặc thay đổi hàng chưa thanh toán
            ShoppingCart.Instance.OnUnpaidItemsUpdated += OnUnpaidItemsChanged;

            // Cập nhật ngay trạng thái ban đầu
            UpdateBadge(ShoppingCart.Instance.GetUnpaidItems());
        }
        else
        {
            Debug.LogWarning("[ShopCartButton] ShoppingCart Instance not found!");
            if (notificationBadge != null) notificationBadge.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // 3. Hủy đăng ký để tránh lỗi memory leak
        if (ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.OnUnpaidItemsUpdated -= OnUnpaidItemsChanged;
        }
    }

    // Callback khi dữ liệu thay đổi
    private void OnUnpaidItemsChanged(System.Collections.Generic.List<CartItem> items)
    {
        UpdateBadge(items);
    }

    // Logic cập nhật giao diện
    private void UpdateBadge(System.Collections.Generic.List<CartItem> items)
    {
        if (items == null) return;

        int count = items.Count;
        bool hasUnpaidItems = count > 0;

        // Hiện chấm đỏ nếu có hàng chưa thanh toán
        if (notificationBadge != null)
        {
            notificationBadge.SetActive(hasUnpaidItems);
        }

        // Cập nhật số lượng nếu có text
        if (countText != null)
        {
            countText.text = count > 99 ? "99+" : count.ToString();
            countText.gameObject.SetActive(hasUnpaidItems);
        }
    }

    // Logic khi bấm nút
    private void OnOpenCartClicked()
    {
        // Cách 1: Mở trực tiếp GameObject Panel nếu được tham chiếu
        if (cartPanelToOpen != null)
        {
            cartPanelToOpen.SetActive(true);

            // Nếu CartUI có script xử lý logic mở (như refresh tab), hãy gọi nó
            // var ui = cartPanelToOpen.GetComponent<CartUI>();
            // if (ui != null) ui.Open(); 
        }
        // Cách 2: Gọi thông qua CartUI Instance (nếu CartUI là Singleton hoặc dễ truy cập)
        else if (FindFirstObjectByType<CartUI>() is CartUI ui)
        {
            // Giả định CartUI có hàm Toggle hoặc Open
            // ui.ToggleCartPanel(); 
            // Hoặc đơn giản là bật GameObject của nó
            ui.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[ShopCartButton] Cannot find Cart Panel to open!");
        }
    }
}
