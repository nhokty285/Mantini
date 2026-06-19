// VendorNPC.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VendorNPC : BaseNPC
{
    [Header("Vendor Specific")]
    [SerializeField] private NPCAPIConfig vendorConfig;
    [SerializeField] private ShopData defaultShopData; // Fallback nếu API fail
    [SerializeField] private Sprite vendorIconBT;
    [SerializeField] private MultiChatManager _chatManager;

    [Header("Idle Animation Settings")]
    [SerializeField] private int totalIdleVariations = 3; // Tổng số idle đặc biệt

    [Header("Vendor Configuration")]
    [SerializeField] private Sprite vendorImage; // Avatar của vendor
    [SerializeField] private Sprite vendorIcon;  // Icon nhỏ trong chat
    [SerializeField] private string shopCategory = "General";
    [SerializeField] private TextMeshProUGUI nameTagUI;

    [Header("Vendor Dialogue Config")]
    [SerializeField] private bool customDialogueEnabled = true;

    [Header("Vendor Image Layout")]
    [SerializeField] private ImageLayout myLayout = ImageLayout.Default;

    [Header("Dify Chat State")]
    [SerializeField] private string difyApiKey;

    // Public Action (giữ public API)
    public System.Action OnTypingStarted;
    public System.Action OnTypingStopped;

    // Internal state
    private int lastActionId = 0;
    private bool isCustomerNearby = false;
    private ShopData dynamicShopData;
    private bool isShopDataLoaded = false;

    // Animator Hashes — readonly cache
    private readonly int actionIdHash = Animator.StringToHash("ActionID");
    private readonly int isIdlingHash = Animator.StringToHash("IsIdling");

    protected override MultiChatManager GetChatManager() => _chatManager;

    private void Start()
    {
        if (NameplateManager.Instance != null)
            NameplateManager.Instance.Register(this.transform, npcName);

        // Refactor: chỉ Find khi Inspector chưa wire (tránh override SerializeField)
        if (_chatManager == null)
            _chatManager = FindFirstObjectByType<MultiChatManager>();
    }

    private void FixedUpdate()
    {
        // Nếu có khách -> Tắt chế độ Idling
        if (isCustomerNearby)
        {
            if (npcAnimator != null && npcAnimator.GetBool(isIdlingHash))
                npcAnimator.SetBool(isIdlingHash, false);
            return;
        }

        HandleRandomIdleActions();
    }

    public override void InitializeNPCData()
    {
        npcType = NPCType.Vendor;
        SetupNameTag();

        if (vendorConfig != null)
        {
            npcName = vendorConfig.npcName;
            npcId = vendorConfig.npcId;
        }

#if UNITY_EDITOR
        GameLog.Info($"[VendorNPC] '{npcName}' initialized - Category: {vendorConfig?.shopCategory}");
#endif
    }

    private void SetupNameTag()
    {
        // Refactor: thêm null check cho nameTagUI và dùng IsNullOrEmpty (string null/empty đều fail)
        if (nameTagUI != null && !string.IsNullOrEmpty(npcName))
            nameTagUI.text = npcName;
    }

    private void HandleRandomIdleActions()
    {
        if (npcAnimator == null || totalIdleVariations <= 0) return;

        // Luôn bật Bool này khi không có khách
        if (!npcAnimator.GetBool(isIdlingHash))
            npcAnimator.SetBool(isIdlingHash, true);

        AnimatorStateInfo stateInfo = npcAnimator.GetCurrentAnimatorStateInfo(0);

        // Kiểm tra xem animation hiện tại sắp hết chưa
        bool isFinishing = stateInfo.normalizedTime >= 0.95f && !npcAnimator.IsInTransition(0);
        bool isDefaultState = npcAnimator.GetInteger(actionIdHash) == 0;

        if (!(isFinishing || isDefaultState)) return;

        // Random số mới
        int newActionId;
        if (totalIdleVariations == 1)
        {
            newActionId = 1;
        }
        else
        {
            do
            {
                newActionId = Random.Range(1, totalIdleVariations + 1);
            } while (newActionId == lastActionId);
        }
        lastActionId = newActionId;

        // Chỉ cần đổi ID, Bool IsIdling đã bật sẵn rồi
        npcAnimator.SetInteger(actionIdHash, newActionId);
    }

    public override void OnPlayerEnterRange()
    {
#if UNITY_EDITOR
        GameLog.Info($"[VendorNPC] {npcName}: Player entered range");
#endif
        isCustomerNearby = true;

        if (npcAnimator != null)
        {
            // 1. Tắt công tắc Trigger ngay lập tức (Xóa lệnh cũ)
            npcAnimator.SetBool(isIdlingHash, false);
            // 2. Ép ActionID về 0 -> Kích hoạt Transition thoát hiểm
            npcAnimator.SetInteger(actionIdHash, 0);
        }

        // Load shop data từ API
        if (!isShopDataLoaded)
            FetchShopDataFromAPI();

        TutorialGamePlay.Instance?.OnPlayerEnterNPCRange();
    }

    public override void OnPlayerExitRange()
    {
        _chatManager?.RemoveParticipant(this);
#if UNITY_EDITOR
        GameLog.Info($"[VendorNPC] {npcName}: Player left range");
#endif
        isCustomerNearby = false;

        if (MainMenuView.Instance != null)
        {
            // Gọi SetNPCInteraction với isNear = false để kích hoạt logic Remove trong ShopController
            MainMenuView.Instance.SetNPCInteraction(false, null, null, this);
        }
    }

    public override void ProcessInteraction()
    {
        if (MainMenuView.Instance == null) return;

#if UNITY_EDITOR
        GameLog.Info($"[VendorNPC][RESTORE] ProcessInteraction — " +
                  $"HasActiveConversation={HasActiveConversation}, " +
                  $"SessionRestoredThisPlay={SessionRestoredThisPlay}, " +
                  $"ConversationId='{ConversationId}'");
#endif

        // Nếu đã có conversation trong session này → restore lịch sử
        if (HasActiveConversation && !SessionRestoredThisPlay)
        {
            GameLog.Info($"[VendorNPC] {npcName}: Restoring conversation {ConversationId}");

            RestoreConversationSession((messages) =>
            {
#if UNITY_EDITOR
                GameLog.Info($"[VendorNPC][RESTORE] → Restore callback nhận {messages?.Count ?? 0} messages, mở shop...");
#endif
                // Mở shop bình thường
                MainMenuView.Instance.SetNPCInteraction(
                    true, npcName, dynamicShopData ?? defaultShopData, this);

                // Sau khi UI mở, load lịch sử chat lên
                if (messages != null && messages.Count > 0)
                    MainMenuView.Instance.RestoreChatHistory(messages, this);
                else
                    GameLog.Warn("[VendorNPC][RESTORE] → messages rỗng, không restore UI");
            });
        }
        else
        {
            // Lần đầu vào shop hoặc đã restore rồi → mở bình thường
            MainMenuView.Instance.SetNPCInteraction(
                true, npcName, dynamicShopData ?? defaultShopData, this);
        }

        TutorialGamePlay.Instance?.OnPlayerClickedNPC();
    }

    public override Sprite GetParticipantIcon()
    {
        // Ưu tiên trả về vendorIcon nếu có
        return vendorIcon != null ? vendorIcon : base.GetParticipantIcon();
    }

    protected override string GetDefaultResponse()
        => $"Xin chào! Tôi là {npcName}. Chào mừng đến cửa hàng của tôi!";

    // Vendor-specific methods (từ SellerTrigger cũ)
    private void FetchShopDataFromAPI()
    {
        if (ShopAPIManager.Instance == null || vendorConfig == null) return;

        ShopAPIManager.Instance.FetchShopItemsForNPC(
            vendorConfig.npcId,
            OnAPISuccess,
            OnAPIError
        );
    }

    private void OnAPISuccess(List<ShopItem> shopItems)
    {
        GameLog.Info($"[VendorNPC] API Success: Received {shopItems.Count} items for vendor {npcName}");

        // Tạo dynamic shop data
        dynamicShopData = ScriptableObject.CreateInstance<ShopData>();
        dynamicShopData.shopName = $"{vendorConfig.npcName}-{vendorConfig.shopCategory} Store";

        SetDynamicItems(dynamicShopData, shopItems);
        isShopDataLoaded = true;

        GameLog.Info($"[VendorNPC] Shop data loaded successfully for {npcName}");
    }

    private void OnAPIError(string error)
    {
        Debug.LogError($"[VendorNPC] Failed to load shop data for {npcName}: {error}");

        // Sử dụng default shop data
        if (defaultShopData != null)
        {
            dynamicShopData = defaultShopData;
            isShopDataLoaded = true;
            GameLog.Info($"[VendorNPC] Using default shop data for {npcName}");
        }
    }

    // ─── IChatParticipant overrides ─────────────────────────────────────
    // Refactor: bỏ duplicate (GetParticipantName/ID/Type, OnJoin/Leave) — base đã cover.
    // IsActive() khác base (active khi có customer) → giữ override.
    public override bool IsActive() => isCustomerNearby;

    public override ChatParticipantType GetParticipantType() => ChatParticipantType.VendorNPC;

    private void SetDynamicItems(ShopData shopData, List<ShopItem> items)
    {
        var itemsListField = typeof(ShopData).GetField("itemsList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (itemsListField != null)
            itemsListField.SetValue(shopData, items);
    }

    // Override GetDialogueSequence để setup dialogue của Vendor
    public override List<DialogueEntry> GetDialogueSequence()
    {
        if (!customDialogueEnabled || dialogueSequence.Count == 0)
            return GenerateDefaultVendorDialogue();

        return new List<DialogueEntry>(dialogueSequence);
    }

    /// Tạo dialogue mặc định cho vendor nếu không có custom dialogue
    private List<DialogueEntry> GenerateDefaultVendorDialogue()
    {
        // Refactor: pre-allocate capacity = 3 (tránh resize List)
        var defaultDialogue = new List<DialogueEntry>(3)
        {
            new DialogueEntry(npcName, $"Xin chào! Tôi là {npcName}.", vendorImage, 0f, myLayout),
            new DialogueEntry(npcName, $"Mình bán những sản phẩm {shopCategory} rất chất lượng.", vendorImage, 0f, myLayout),
            new DialogueEntry(npcName, "Bạn muốn xem hàng của mình không?", vendorImage, 0f, myLayout)
        };
        return defaultDialogue;
    }

    public override string ProcessMessage(string message, string sender)
    {
        if (enableAIChat && !string.IsNullOrEmpty(aiPersonality))
        {
            GetAIResponse(message);
            return null;
        }
        return GetDefaultResponse();
    }

    // Getter cho shop data
    public ShopData GetShopData() => dynamicShopData ?? defaultShopData;
    public NPCAPIConfig GetVendorConfig() => vendorConfig;
    public Sprite GetVendorImage() => vendorIconBT;
}