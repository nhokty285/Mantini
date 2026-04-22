using UnityEngine;
using System.Collections;
public class NPCChatAdapter : MonoBehaviour, IChatParticipant
{
    [SerializeField] private BaseNPC targetNPC;
    public event System.Action<IChatParticipant, string> OnAsyncResponseReady;
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

    public string ProcessMessage(string incomingMessage = "", string senderID = "")
    {
        /*// ✅ DELEGATE TO CORRECT NPC TYPE
        if (targetNPC is CompanionNPC companion)
        {
            return ProcessCompanionMessage(companion, incomingMessage);
        }
        else if (targetNPC is VendorNPC vendor)
        {
            return ProcessVendorMessage(vendor, incomingMessage);
        }*/
        return null;
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

        // ✅ THÊM: Nếu enableAIChat = true → gọi Dify async
        if (vendor.GetEnableAIChat() && !string.IsNullOrEmpty(vendor.GetAIPersonality()))
        {
            // Subscribe một lần rồi gọi
            vendor.OnDifyResponseReceived = null; // reset tránh duplicate
            vendor.OnDifyResponseReceived += (answer) =>
            {
                // Khi Dify trả về → báo cho MultiChatManager qua event
                vendor.OnDifyResponseReceived = null;
                OnAsyncResponseReady?.Invoke(this, answer);
            };

            vendor.GetAIResponse(message); // Kick off async call
            return null; // null = "đang chờ async"
        }

        // Fallback sync nếu AI tắt
        return vendor.GetAIResponse(message);
    }

    // ✅ THÊM method này
    private string GetDefaultResponse(string message)
    {
       return null;
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
        
    }

    public bool IsActive()
    {
        return targetNPC != null && targetNPC.gameObject.activeInHierarchy;
    }
    // ✅ PUBLIC METHOD để setup từ bên ngoài
    public void SetTargetNPC(BaseNPC npc)
    {
        targetNPC = npc;
    }

    public Sprite GetParticipantIcon()
    {
        return targetNPC != null ? targetNPC.GetParticipantIcon() : null;
    }


}

