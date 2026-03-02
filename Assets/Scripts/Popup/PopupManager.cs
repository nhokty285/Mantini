using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel; // Panel cha chứa toàn bộ popup (đen mờ)
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    private Action onConfirmAction; // Lưu hành động sẽ làm khi bấm OK

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        popupPanel.SetActive(false); // Ẩn mặc định

        // Gắn sự kiện sẵn
        confirmButton.onClick.AddListener(()=> { 
            AudioManager.Instance.PlaySFXOneShot("Button_High");
            OnConfirmClicked();         
        });
        cancelButton.onClick.AddListener(()=> {
            AudioManager.Instance.PlaySFXOneShot("Close");
            HidePopup();         
        });
    }

    // Hàm gọi Popup chuẩn (Dùng cho mọi nơi)
    public void ShowPopup(string title, string message, Action onConfirm, string btnText = "Đồng ý")
    {
        titleText.text = title;
        messageText.text = message;
        confirmButtonText.text = btnText;

        onConfirmAction = onConfirm; // Lưu hành động lại

        // Logic hiển thị nút Cancel (nếu onConfirm == null thì là popup thông báo -> ẩn Cancel)
        cancelButton.gameObject.SetActive(onConfirm != null);

        popupPanel.SetActive(true);
    }

    private void OnConfirmClicked()
    {
        HidePopup();
        onConfirmAction?.Invoke(); // Thực hiện hành động
    }

    public void HidePopup()
    {
        popupPanel.SetActive(false);
    }
}
