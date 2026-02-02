// BaseNPC.cs
using OpenAI;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum NPCType
{
    Companion,
    Vendor,
    QuestGiver,
    Guard
}

public abstract class BaseNPC : MonoBehaviour, IChatParticipant
{
    [Header("Base NPC Configuration")]
    [SerializeField] protected string npcName = "Unknown NPC";
    [SerializeField] protected string npcId;
    [SerializeField] protected NPCType npcType;
    [SerializeField] protected float interactionRange = 3f;
    [SerializeField] protected Sprite npcIcon;
    [Header("Dialogue Configuration")]
    [SerializeField] protected List<DialogueEntry> dialogueSequence = new List<DialogueEntry>();

    [Header("AI Chat Integration - Chuẩn bị cho tương lai")]
    [SerializeField] protected bool enableAIChat = false;
    [SerializeField] protected string aiPersonality; // "Friendly Companion" hoặc "Professional Vendor"

    [Header("Animation & Visual")]
    [SerializeField] protected Animator npcAnimator;
    [SerializeField] protected GameObject interactionIndicator;
    // Events cho MainMenuView subscribe
    public System.Action<bool, string, BaseNPC> OnPlayerInteraction;
    protected bool isPlayerNearby = false;
    protected Transform playerTransform;


    // Abstract methods - bắt buộc implement ở child classes
    public abstract void InitializeNPCData();
    public abstract void OnPlayerEnterRange();
    public abstract void OnPlayerExitRange();
    public abstract void ProcessInteraction();

    // Virtual methods - có thể override nếu cần
    public virtual void Start()
    {
        InitializeNPCData();
        SetupInteractionIndicator();
     
        // Tìm player reference
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        // Nếu dialogueSequence trống, gọi method setup mặc định
        if (dialogueSequence.Count == 0)
        {
            SetupDefaultDialogue();
        }
    }

    private void OnDestroy()
    {
        // Báo Manager thu hồi tên khi NPC bị hủy
        if (NameplateManager.Instance != null)
        {
            NameplateManager.Instance.Unregister(this.transform);
        }
    }

    protected virtual void SetupInteractionIndicator()
    {
        if (interactionIndicator != null)
            interactionIndicator.SetActive(false);
    }

    // ✅ NEW: Lấy dialogue sequence - Có thể được override bởi child classes
    public virtual List<DialogueEntry> GetDialogueSequence()
    {
        return new List<DialogueEntry>(dialogueSequence);
    }

    // ✅ NEW: Setup dialogue mặc định - Override trong child classes nếu cần
    protected virtual void SetupDefaultDialogue()
    {
        dialogueSequence.Add(new DialogueEntry(
            npcName,
            "Xin chào! Mình là " + npcName,
            null,
            0f
        ));
    }


    // Collision Detection - Common cho tất cả NPCs
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            OnPlayerEnterRange();

            // ✅ THÊM debug log
            Debug.Log($"🔍 {npcName} - OnTriggerEnter: this={this}, npcId={npcId}");

            // Notify UI System
            OnPlayerInteraction?.Invoke(true, npcName, this);

            if (interactionIndicator != null)
                interactionIndicator.SetActive(true);
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            OnPlayerExitRange();

            // Notify UI System
            OnPlayerInteraction?.Invoke(false, npcName, this);

            if (interactionIndicator != null)
                interactionIndicator.SetActive(false);
        }
    }

    // Chuẩn bị cho AI Chat System
    public virtual string GetAIResponse(string playerMessage)
    {
        if (!enableAIChat) return GetDefaultResponse();

        // TODO: Tích hợp với ChatGPT API sau này
        // return await ChatGPTManager.Instance.SendMessageAsync(playerMessage, aiPersonality);

        string baseResponse = GetDefaultResponse();

        // Thêm context nếu player chat từ xa
     /*   if (!isPlayerNearby)
        {
            baseResponse = AddRemoteContext(baseResponse);
        }*/

        return GetDefaultResponse();
    }


  /*  private string AddRemoteContext(string response)
    {
        return $"(Qua radio) {response}";
    }*/

    protected abstract string GetDefaultResponse();


    // Getter methods
    public string GetNPCName() => npcName;
    public string GetNPCId() => npcId;
    public NPCType GetNPCType() => npcType;
    public bool IsPlayerNearby() => isPlayerNearby;

    public virtual Sprite GetParticipantIcon()
    {
        return npcIcon;
    }

    // IChatParticipant Implementation
    // ========================================

    // ✅ THÊM METHOD NÀY
    public virtual string GetParticipantName()
    {
        Debug.Log($"[GetParticipantName] Returning: '{npcName}'");  // ← Debug log
        return npcName;
    }

    // ✅ THÊM METHOD NÀY
    public virtual string GetParticipantID()
    {
        return npcId;
    }

    // ✅ THÊM METHOD NÀY (nếu chưa có)
    public virtual ChatParticipantType GetParticipantType()
    {
        // Map NPCType sang ChatParticipantType
        switch (npcType)
        {
            case NPCType.Companion:
                return ChatParticipantType.Companion;
            case NPCType.Vendor:
                return ChatParticipantType.VendorNPC;
            default:
                return ChatParticipantType.AIBot;
        }
    }

    // ✅ THÊM METHOD NÀY (nếu chưa có)
    public virtual bool IsActive()
    {
        return gameObject.activeInHierarchy && enabled;
    }

    // ✅ THÊM 2 METHOD NÀY (callbacks)
    public virtual void OnJoinChat()
    {
        Debug.Log($"[{npcName}] Joined chat");
    }

    public virtual void OnLeaveChat()
    {
        Debug.Log($"[{npcName}] Left chat");
    }

    // ✅ Abstract method - child class bắt buộc implement
    public abstract string ProcessMessage(string message, string sender);
}

