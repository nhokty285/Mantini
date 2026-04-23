using UnityEngine;
using System.Collections;
public class NPCChatAdapter : MonoBehaviour, IChatParticipant
{
    [SerializeField] private BaseNPC targetNPC;
    //public event System.Action<IChatParticipant, string> OnAsyncResponseReady;
    private void Awake()
    {
        if (targetNPC == null)
            targetNPC = GetComponent<BaseNPC>();
    }


    public string GetParticipantName() => targetNPC.GetNPCName();
    public ChatParticipantType GetParticipantType()
    {
        if (targetNPC is CompanionNPC)
            return ChatParticipantType.Companion;
        else if (targetNPC is VendorNPC)
            return ChatParticipantType.VendorNPC;
        else
            return ChatParticipantType.AIBot;
    }

    public string ProcessMessage(string incomingMessage = "", string senderID = "")
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
        return null;
    }

    private string ProcessCompanionMessage(CompanionNPC companion, string message)
    {
        companion.GetAIResponse(message);
        return null;
    }


    private string ProcessVendorMessage(VendorNPC vendor, string message)
    {
        vendor.GetAIResponse(message);
        return null;
    }

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

