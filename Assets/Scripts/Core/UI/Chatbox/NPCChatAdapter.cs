using UnityEngine;

public class NPCChatAdapter : MonoBehaviour, IChatParticipant
{
    [SerializeField] private BaseNPC targetNPC;
    // [SerializeField] private ChatbotAI aiComponent; // Sẽ thêm sau

    private void Awake()
    {
        if (targetNPC == null)
            targetNPC = GetComponent<BaseNPC>();
    }


    public string GetParticipantName() => targetNPC.GetNPCName();
    public ChatParticipantType GetParticipantType()
    {
        // ❌ SỬATA: Đang hardcode thành VendorNPC
        // return ChatParticipantType.VendorNPC;

        // ✅ DYNAMIC CLASSIFICATION
        if (targetNPC is CompanionNPC)
            return ChatParticipantType.Companion;
        else if (targetNPC is VendorNPC)
            return ChatParticipantType.VendorNPC;
        else
            return ChatParticipantType.AIBot; // Fallback
    }

    public string ProcessMessage(string incomingMessage, string senderID)
    {
        // ✅ DELEGATE TO CORRECT NPC TYPE
        if (targetNPC is CompanionNPC companion)
        {
            return ProcessCompanionMessage(companion, incomingMessage);
        }
        else if (targetNPC is VendorNPC vendor)
        {
            return ProcessVendorMessage(vendor, incomingMessage);
        }

        return GetDefaultResponse(incomingMessage);
    }

    private string ProcessCompanionMessage(CompanionNPC companion, string message)
    {
        string reply = companion.GetHelpForTopic(message);
        if (string.IsNullOrWhiteSpace(reply))
            reply = companion.GetAIResponse(message);
        return reply;
    }

    private string ProcessVendorMessage(VendorNPC vendor, string message)
    {
        // ✅ VENDOR-SPECIFIC RESPONSES
        string lowerMessage = message.ToLower();

        if (lowerMessage.Contains("giá") || lowerMessage.Contains("price"))
            return "Giá cả sản phẩm của chúng tôi rất hợp lý! Bạn muốn xem catalog không?";

        if (lowerMessage.Contains("mua") || lowerMessage.Contains("buy"))
            return "Tuyệt vời! Hãy chọn sản phẩm bạn thích từ catalog nhé!";

        if (lowerMessage.Contains("chất lượng") || lowerMessage.Contains("quality"))
            return $"Tất cả {vendor.GetVendorConfig()?.shopCategory} của chúng tôi đều có chất lượng cao!";

        if (lowerMessage.Contains("giới thiệu") || lowerMessage.Contains("introduce"))
            return $"Tôi là {vendor.GetNPCName()}, chuyên bán {vendor.GetVendorConfig()?.shopCategory}. Có gì tôi có thể giúp bạn?";

        // Fallback to vendor default
        string response = vendor.GetAIResponse(message);
        Debug.Log($"✅ Vendor response: '{response}'");
        return response;
    }

    // ✅ THÊM method này
    private string GetDefaultResponse(string message)
    {
        return $"Xin chào! Tôi là {GetParticipantName()}. Bạn cần gì không?";
    }

    // ✅ THÊM các methods còn thiếu  
    public string GetParticipantID()
    {
        return targetNPC != null ? targetNPC.GetInstanceID().ToString() : gameObject.GetInstanceID().ToString();
    }
    public void OnJoinChat()
    {
        Debug.Log($"{GetParticipantName()} joined the chat");
    }

    public void OnLeaveChat()
    {
        Debug.Log($"{GetParticipantName()} left the chat");
    }

    public bool IsActive()
    {
        return targetNPC != null && targetNPC.gameObject.activeInHierarchy;
    }
    // ✅ PUBLIC METHOD để setup từ bên ngoài
    public void SetTargetNPC(BaseNPC npc)
    {
        targetNPC = npc;
        Debug.Log($"✅ NPCChatAdapter target set to: {npc?.GetNPCName()}");
    }

    public Sprite GetParticipantIcon()
    {
        return targetNPC != null ? targetNPC.GetParticipantIcon() : null;
    }
}

