/*// ChatMessageUI.cs  (bản cũ giữ lại tham khảo)
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChatMessageUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI senderText;
    [SerializeField] private VerticalLayoutGroup layout;

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

    [Header("Bubble")]
    [SerializeField] private TextMeshProUGUI senderText;     // tên người gửi, nằm TRÊN bong bóng
    [SerializeField] private Image bubbleBackground;         // nền bong bóng (gắn sprite 9-slice ở Inspector)
    [SerializeField] private Color npcBubbleColor = new Color(0.788f, 0.910f, 0.863f, 1f);   // mint  #C9E8DC
    [SerializeField] private Color playerBubbleColor = new Color(0.949f, 0.918f, 0.827f, 1f); // kem   #F2EAD3
    [SerializeField] private string senderColorHex = "#673A28";

    private void Start()
    {

    }

    public void Setup(string message, string sender, bool isPlayer, Sprite iconSprite)
    {
        if (iconSmall != null && iconSprite != null)
        {
            iconSmall.sprite = iconSprite;
            iconSmall.gameObject.SetActive(true);
        }

        // Tên người gửi: tách hẳn lên trên, căn theo phía của bong bóng
        if (senderText != null)
        {
            senderText.text = $"<color={senderColorHex}><b>{sender}</b></color>";
            senderText.alignment = isPlayer ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        }

        if (messageText != null)
        {
            // sender đã tách lên trên -> message giữ nguyên (có thể kèm <link> sản phẩm)
            messageText.text = message;
            messageText.raycastTarget = true; // cần để bắt click link
        }

        // Nền bong bóng: mint cho NPC, kem cho player
        if (bubbleBackground != null)
            bubbleBackground.color = isPlayer ? playerBubbleColor : npcBubbleColor;

        // Tìm UI Camera (Screen Space - Camera); Overlay thì null cũng được
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (messageText == null) return;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(messageText, eventData.position, uiCamera);
        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = messageText.textInfo.linkInfo[linkIndex];
            string productID = linkInfo.GetLinkID();

            GameLog.Info($"[Chat] User clicked product link: {productID}");
            OpenProduct(productID);
        }
    }

    private void OpenProduct(string productID)
    {
        var shopController = FindFirstObjectByType<ShopController>();
        if (shopController != null)
            shopController.OnProductLinkCallback(productID);
        else
            Debug.LogError("[ChatMessageUI] ShopController not found!");
    }
}