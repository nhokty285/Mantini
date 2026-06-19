using UnityEngine;

public class NPCChatAdapter : MonoBehaviour, IChatParticipant
{
    [SerializeField] private BaseNPC targetNPC;

    private void Awake()
    {
        if (targetNPC == null)
            targetNPC = GetComponent<BaseNPC>();
    }

    public string GetParticipantName() => targetNPC != null ? targetNPC.GetNPCName() : string.Empty;

    public ChatParticipantType GetParticipantType()
    {
        // Refactor: switch expression gọn hơn if-else
        return targetNPC switch
        {
            CompanionNPC => ChatParticipantType.Companion,
            VendorNPC    => ChatParticipantType.VendorNPC,
            _            => ChatParticipantType.AIBot
        };
    }

    public string ProcessMessage(string incomingMessage = "", string senderID = "")
    {
        // Refactor: gộp ProcessCompanionMessage/ProcessVendorMessage
        // (cả 2 chỉ gọi GetAIResponse) → dùng base.GetAIResponse polymorphism.
        if (targetNPC == null) return null;
        targetNPC.GetAIResponse(incomingMessage);
        return null;
    }

    public string GetParticipantID()
    {
        return targetNPC != null
            ? targetNPC.GetInstanceID().ToString()
            : gameObject.GetInstanceID().ToString();
    }

    public void OnJoinChat()
    {
#if UNITY_EDITOR
        GameLog.Info($"[NPCChatAdapter] {GetParticipantName()} joined the chat");
#endif
    }

    public void OnLeaveChat() { }

    public bool IsActive()
        => targetNPC != null && targetNPC.gameObject.activeInHierarchy;

    // ✅ PUBLIC METHOD để setup từ bên ngoài (NPCManager dùng)
    public void SetTargetNPC(BaseNPC npc) => targetNPC = npc;

    public Sprite GetParticipantIcon()
        => targetNPC != null ? targetNPC.GetParticipantIcon() : null;
}