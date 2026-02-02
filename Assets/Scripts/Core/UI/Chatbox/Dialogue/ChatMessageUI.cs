/*// ChatMessageUI.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChatMessageUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI senderText;
    [SerializeField] private VerticalLayoutGroup layout; // tùy chọn để align trái/phải

    public void Setup(string message, string sender, bool isPlayer)
    {
        if (messageText) messageText.text = message;
        if (senderText) senderText.text = sender;
    }
}
*/

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChatMessageUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image iconSmall;     // Cache UI Camera để raycast chính xác link
    [SerializeField] private Camera uiCamera;
  
    private void Start()
    {
        
    }
    public void Setup(string message, string sender, bool isPlayer,Sprite iconSprite)
    {
        if (iconSmall != null)
        {
            if (iconSprite != null)
            {
                iconSmall.sprite = iconSprite;
                iconSmall.gameObject.SetActive(true);
            }
        }
        if (messageText)
        {
            string senderColorHex = "#673A28";

            messageText.text = $"<color={senderColorHex}><b>{sender}:</b></color>\u00A0“{message}.”";
            // Bắt buộc phải bật raycastTarget để nhận click
            messageText.raycastTarget = true;
        }

        //if (senderText) senderText.text = sender;

        // Tìm UI Camera (nếu Canvas để Screen Space - Camera, còn Overlay thì null cũng được)
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (messageText == null) return;

        // Kiểm tra xem click có trúng Link nào không
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(messageText, eventData.position, uiCamera);

        if (linkIndex != -1)
        {
            // Lấy thông tin link
            TMP_LinkInfo linkInfo = messageText.textInfo.linkInfo[linkIndex];
            string productID = linkInfo.GetLinkID();

            Debug.Log($"[Chat] User clicked product link: {productID}");

            // Gửi yêu cầu mở sản phẩm
            OpenProduct(productID);
        }
    }

    private void OpenProduct(string productID)
    {
        // Tìm ShopController trong scene để xử lý mở popup
        var shopController = FindFirstObjectByType<ShopController>();
        if (shopController != null)
        {
            // Gọi hàm mở chi tiết sản phẩm (cần thêm hàm này vào ShopController)
            shopController.OnProductLinkCallback(productID);
        }
        else
        {
            Debug.LogError("[ChatMessageUI] ShopController not found!");
        }
    }
}
