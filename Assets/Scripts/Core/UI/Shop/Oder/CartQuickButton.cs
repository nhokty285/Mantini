using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CartQuickButton : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Button openCartButton;
    [SerializeField] private GameObject notificationBadge; // Chấm đỏ
    [SerializeField] private TextMeshProUGUI countText;    // Số lượng (tùy chọn)

    [Header("Target UI")]
    [SerializeField] private GameObject cartPanelToOpen;   // Panel CartUI cần mở

    private void Start()
    {
        // 1. Đăng ký sự kiện click theo Mantini convention (RemoveAll trước Add)
        if (openCartButton != null)
        {
            openCartButton.onClick.RemoveAllListeners();
            openCartButton.onClick.AddListener(OnCartButtonClicked);
        }

        // 2. Đăng ký lắng nghe thay đổi từ ShoppingCart
        if (ShoppingCart.Instance != null)
        {
            ShoppingCart.Instance.OnUnpaidItemsUpdated += OnUnpaidItemsChanged;
            // Cập nhật ngay trạng thái ban đầu
            UpdateBadge(ShoppingCart.Instance.GetUnpaidItems());
        }
        else
        {
            GameLog.Warn("[CartQuickButton] ShoppingCart Instance not found!");
            if (notificationBadge != null) notificationBadge.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký để tránh memory leak
        if (ShoppingCart.Instance != null)
            ShoppingCart.Instance.OnUnpaidItemsUpdated -= OnUnpaidItemsChanged;
    }

    // Refactor: tách lambda click thành method có tên — dễ debug + null-safe AudioManager
    private void OnCartButtonClicked()
    {
        OnOpenCartClicked();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXOneShot("Button");
        TutorialGamePlay.Instance?.OnCartOpened();
    }

    // Callback khi dữ liệu thay đổi
    private void OnUnpaidItemsChanged(List<CartItem> items) => UpdateBadge(items);

    // Logic cập nhật giao diện
    private void UpdateBadge(List<CartItem> items)
    {
        if (items == null) return;

        int count = items.Count;
        bool hasUnpaidItems = count > 0;

        // Hiện chấm đỏ nếu có hàng chưa thanh toán
        if (notificationBadge != null)
            notificationBadge.SetActive(hasUnpaidItems);

        // Cập nhật số lượng nếu có text
        if (countText != null)
        {
            countText.text = count > 99 ? "99+" : count.ToString();
            countText.gameObject.SetActive(hasUnpaidItems);
        }
    }

    private void OnOpenCartClicked()
    {
        // Cách 1: Mở trực tiếp GameObject Panel nếu được tham chiếu
        if (cartPanelToOpen != null)
        {
            cartPanelToOpen.SetActive(true);
            return;
        }

        // Cách 2: Tìm CartUI fallback (chậm hơn, chỉ dùng khi Inspector chưa wire)
        if (FindFirstObjectByType<CartUI>() is CartUI ui)
            ui.gameObject.SetActive(true);
        else
            Debug.LogError("[CartQuickButton] Cannot find Cart Panel to open!");
    }
}