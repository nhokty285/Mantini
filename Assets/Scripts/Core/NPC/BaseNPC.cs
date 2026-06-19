// BaseNPC.cs
using System.Collections.Generic;
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

    // ── Conversation / session state ────────────────────────────────────────
    private string _conversationId = "";
    private bool _isWaitingResponse = false;
    private bool _sessionRestoredThisPlay = false;
    private GameObject _typingBubble;

    public string ConversationId
    {
        get => _conversationId;
        set => _conversationId = value;
    }
    // Refactor: bỏ [SerializeField] vô tác dụng trên expression-bodied property (Unity không serialize được)
    public bool HasActiveConversation => !string.IsNullOrEmpty(_conversationId);
    public bool SessionRestoredThisPlay => _sessionRestoredThisPlay;

    // Events
    public event System.Action<IChatParticipant, string> OnDifyResponseReceived;
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
            SetupDefaultDialogue();
    }

    private void OnDestroy()
    {
        // Báo Manager thu hồi tên khi NPC bị hủy
        if (NameplateManager.Instance != null)
            NameplateManager.Instance.Unregister(this.transform);
    }

    protected virtual void SetupInteractionIndicator()
    {
        if (interactionIndicator != null)
            interactionIndicator.SetActive(false);
    }

    // Lấy dialogue sequence - Có thể được override bởi child classes
    public virtual List<DialogueEntry> GetDialogueSequence()
        => new List<DialogueEntry>(dialogueSequence);

    // Setup dialogue mặc định - Override trong child classes nếu cần
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
        if (!other.CompareTag("Player")) return;

        isPlayerNearby = true;
        OnPlayerEnterRange();

#if UNITY_EDITOR
        GameLog.Info($"[BaseNPC] {npcName} OnTriggerEnter: npcId={npcId}");
#endif

        // Notify UI System
        OnPlayerInteraction?.Invoke(true, npcName, this);

        if (interactionIndicator != null)
            interactionIndicator.SetActive(true);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerNearby = false;
        OnPlayerExitRange();

        // Notify UI System
        OnPlayerInteraction?.Invoke(false, npcName, this);

        if (interactionIndicator != null)
            interactionIndicator.SetActive(false);
    }

    public virtual string GetAIResponse(string playerMessage)
    {
        if (!enableAIChat || string.IsNullOrEmpty(aiPersonality))
            return GetDefaultResponse();

        if (_isWaitingResponse)
            return "Hãy đợi mình một chút...";

        string userId = string.IsNullOrEmpty(npcId) ? "player-guest" : npcId;
        _isWaitingResponse = true;

        // Typing bubble — dùng _chatManager từ child nếu có
        _typingBubble = GetChatManager()?.AddTypingBubble(
            sender: GetParticipantName(),
            icon: GetParticipantIcon()
        );

        DifyChatService.Instance.SendMessageAI(
            apiKey: aiPersonality,
            userId: userId,
            query: playerMessage,
            conversationId: _conversationId,
            onSuccess: (answer, newConvId) =>
            {
                _conversationId = newConvId;
                _isWaitingResponse = false;
                DestroyTypingBubble();
                OnDifyResponseReceived?.Invoke(this, answer);
            },
            onError: (err) =>
            {
                _isWaitingResponse = false;
                DestroyTypingBubble();
                Debug.LogError($"[BaseNPC] {npcName} Dify error: {err}");
                OnDifyResponseReceived?.Invoke(this, GetDefaultResponse());
            }
        );

        return null;
    }

    // Helper — tránh duplicate destroy logic
    private void DestroyTypingBubble()
    {
        if (_typingBubble != null)
        {
            Destroy(_typingBubble);
            _typingBubble = null;
        }
    }

    public void RestoreConversationSession(System.Action<List<DifyMessage>> onRestored)
    {
#if UNITY_EDITOR
        GameLog.Info($"[BaseNPC][RESTORE] {npcName} — enableAIChat={enableAIChat}, " +
                  $"aiPersonality='{aiPersonality}', _conversationId='{_conversationId}'");
#endif
        if (!enableAIChat || string.IsNullOrEmpty(aiPersonality)) return;
        if (string.IsNullOrEmpty(_conversationId)) return;

        string userId = string.IsNullOrEmpty(npcId) ? "player" : npcId;

        DifyChatService.Instance.GetConversationMessages(
            apiKey: aiPersonality,
            userId: userId,
            conversationId: _conversationId,
            onSuccess: (messages) =>
            {
                _sessionRestoredThisPlay = true;
                GameLog.Info($"[BaseNPC] {npcName} Restored {messages.Count} messages from conversation {_conversationId}");
                onRestored?.Invoke(messages);
            },
            onError: (err) =>
            {
                GameLog.Warn($"[BaseNPC] {npcName} Could not restore conversation: {err}");
                onRestored?.Invoke(new List<DifyMessage>());
            }
        );
    }

    // Abstract/Virtual để child inject _chatManager của mình
    protected virtual MultiChatManager GetChatManager() => null;

    protected abstract string GetDefaultResponse();

    // ── Getter methods ──────────────────────────────────────────────────────
    public string GetNPCName() => npcName;
    public string GetNPCId() => npcId;
    public NPCType GetNPCType() => npcType;
    public bool IsPlayerNearby() => isPlayerNearby;

    public virtual Sprite GetParticipantIcon() => npcIcon;

    // ── IChatParticipant Implementation ─────────────────────────────────────
    public virtual string GetParticipantName() => npcName;

    public virtual string GetParticipantID() => npcId;

    // Refactor: switch expression cho gọn + đỡ allocation/JIT
    public virtual ChatParticipantType GetParticipantType() => npcType switch
    {
        NPCType.Companion => ChatParticipantType.Companion,
        NPCType.Vendor    => ChatParticipantType.VendorNPC,
        _                 => ChatParticipantType.AIBot
    };

    public virtual bool IsActive() => gameObject.activeInHierarchy && enabled;

    public virtual void OnJoinChat()
    {
#if UNITY_EDITOR
        GameLog.Info($"[BaseNPC] {npcName} Joined chat");
#endif
    }

    public virtual void OnLeaveChat()
    {
#if UNITY_EDITOR
        GameLog.Info($"[BaseNPC] {npcName} Left chat");
#endif
    }

    // Abstract method - child class bắt buộc implement
    public abstract string ProcessMessage(string message, string sender);

    public string GetAIPersonality() => aiPersonality;
    public bool GetEnableAIChat() => enableAIChat;
}