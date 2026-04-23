// VendorNPC.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
public class VendorNPC : BaseNPC, IChatParticipant
{
    [Header("Vendor Specific")]
    [SerializeField] private NPCAPIConfig vendorConfig;
    [SerializeField] private ShopData defaultShopData; // Fallback nếu API fail
    [SerializeField] private Sprite vendorIconBT;
    [SerializeField] private MultiChatManager _chatManager;
    protected override MultiChatManager GetChatManager() => _chatManager;
    [Header("Idle Animation Settings")]

    [SerializeField] private int totalIdleVariations = 3; // Tổng số idle đặc biệt 
    private int lastActionId = 0;
    private bool isCustomerNearby = false;
    // Animator Hashes
    private readonly int actionIdHash = Animator.StringToHash("ActionID");
    private readonly int isIdlingHash = Animator.StringToHash("IsIdling");

    private ShopData dynamicShopData;
    private bool isShopDataLoaded = false;

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
/*    private string _conversationId = "";     
    private bool _isWaitingResponse = false;
    private GameObject _typingBubble;*/
    public System.Action OnTypingStarted;   // Fire khi bắt đầu chờ API
    public System.Action OnTypingStopped;   // Fire khi nhận được response (hoặc lỗi)
    [SerializeField] private string difyApiKey;
    private void Start()
    {
        NameplateManager.Instance.Register(this.transform, npcName);
        _chatManager = FindFirstObjectByType<MultiChatManager>();


    }
    private void FixedUpdate()
    {
        // Nếu có khách -> Tắt chế độ Idling
        if (isCustomerNearby)
        {
            if (npcAnimator.GetBool(isIdlingHash))
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

        Debug.Log($"Vendor NPC '{npcName}' initialized - Category: {vendorConfig?.shopCategory}");
    }

    private void SetupNameTag()
    {
        if (npcName != null)
        {
            nameTagUI.text = npcName;
        }
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

        if (isFinishing || isDefaultState)
        {
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
    }

  
    public override void OnPlayerEnterRange()
    {
        Debug.Log($"Vendor {npcName}: Player entered range");
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
        {
            FetchShopDataFromAPI();
        }
    }

    public override void OnPlayerExitRange()
    {
        _chatManager?.RemoveParticipant(this);
        Debug.Log($"Vendor {npcName}: Player left range");
        isCustomerNearby = false;

        if (MainMenuView.Instance != null)
        {
            // Gọi SetNPCInteraction với isNear = false để kích hoạt logic Remove trong ShopController
            MainMenuView.Instance.SetNPCInteraction(false, null, null, this);
        }
    }

    public override void ProcessInteraction()
    {
        if (MainMenuView.Instance != null)
        {
            // ✅ THÊM tham số this
            MainMenuView.Instance.SetNPCInteraction(true, npcName, dynamicShopData ?? defaultShopData, this);
        }
    }

    public override Sprite GetParticipantIcon()
    {
        // Ưu tiên trả về vendorImage hoặc vendorIconBT tùy logic của bạn
        return vendorIcon != null ? vendorIcon : base.GetParticipantIcon();
    }


    protected override string GetDefaultResponse()
    {
        return $"Xin chào! Tôi là {npcName}. Chào mừng đến cửa hàng của tôi!";
    }

    // ✅ THÊM Method để VendorNPC cũng có AI Response
/*    public override string GetAIResponse(string playerMessage)
    {   
        if (!enableAIChat || string.IsNullOrEmpty(aiPersonality))
            return GetDefaultResponse();

        // Tránh gửi nhiều request cùng lúc
        if (_isWaitingResponse)
        return "Hãy đợi mình một chút...";

        // userId = npcId của vendor này để phân biệt session
        string userId = string.IsNullOrEmpty(npcId) ? "player-guest" : npcId;
        _isWaitingResponse = true;

        _typingBubble = _chatManager?.AddTypingBubble(
       sender: GetParticipantName(),
       icon: GetParticipantIcon()
   );

        DifyChatService.Instance.SendMessageAI(
            apiKey: aiPersonality,     // ← aiPersonality IS the API key
            userId: userId,
            query: playerMessage,
            conversationId: _conversationId,
            onSuccess: (answer, newConvId) =>
            {
                _conversationId = newConvId;   // Lưu lại để giữ ngữ cảnh
                _isWaitingResponse = false;

                if (_typingBubble != null)
                {
                    Object.Destroy(_typingBubble);
                    _typingBubble = null;
                }

                Debug.Log($"[{npcName}] Dify response: {answer}");
                OnDifyResponseReceived?.Invoke(answer);
            },
            onError: (err) =>
            {
                _isWaitingResponse = false;
                if (_typingBubble != null)
                {
                    Object.Destroy(_typingBubble);
                    _typingBubble = null;
                }

                Debug.LogError($"[{npcName}] Dify error: {err}");
                OnDifyResponseReceived?.Invoke(GetDefaultResponse());
            }
        );

        return null;
    }*/


    // Vendor-specific methods (từ SellerTrigger cũ)
    private void FetchShopDataFromAPI()
    {
        if (ShopAPIManager.Instance != null && vendorConfig != null)
        {
            ShopAPIManager.Instance.FetchShopItemsForNPC(
                vendorConfig.npcId,
                OnAPISuccess,
                OnAPIError
            );
        }
    }

    private void OnAPISuccess(List<ShopItem> shopItems)
    {
        Debug.Log($"API Success: Received {shopItems.Count} items for vendor {npcName}");

        // Tạo dynamic shop data
        dynamicShopData = ScriptableObject.CreateInstance<ShopData>();
        dynamicShopData.shopName = $"{vendorConfig.npcName}-{vendorConfig.shopCategory} Store";

        SetDynamicItems(dynamicShopData, shopItems);
        isShopDataLoaded = true;

        Debug.Log($"Shop data loaded successfully for {npcName}");
    }

    private void OnAPIError(string error)
    {
        Debug.LogError($"Failed to load shop data for {npcName}: {error}");

        // Sử dụng default shop data
        if (defaultShopData != null)
        {
            dynamicShopData = defaultShopData;
            isShopDataLoaded = true;
            Debug.Log($"Using default shop data for {npcName}");
        }
    }

    // ========== IMPLEMENT IChatParticipant ==========
    public string GetParticipantName()
    {
        return npcName;
    }

    public string GetParticipantID()
    {
        return npcId;
    }

    public ChatParticipantType GetParticipantType()
    {
        return ChatParticipantType.VendorNPC;
    }

    public bool IsActive()
    {
        return isCustomerNearby; // Vendor active khi có khách
    }

    public void OnJoinChat()
    {
        Debug.Log($"Vendor {npcName} available in chat");
    }

    public void OnLeaveChat()
    {
        Debug.Log($"Vendor {npcName} closed chat");
    }

    private void SetDynamicItems(ShopData shopData, List<ShopItem> items)
    {
        var itemsListField = typeof(ShopData).GetField("itemsList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (itemsListField != null)
        {
            itemsListField.SetValue(shopData, items);
        }
    }

    //Override GetDialogueSequence để setup dialogue của Vendor
    public override List<DialogueEntry> GetDialogueSequence()
    {
        if (!customDialogueEnabled || dialogueSequence.Count == 0)
        {
            return GenerateDefaultVendorDialogue();
        }

        return new List<DialogueEntry>(dialogueSequence);
    }

    /// Tạo dialogue mặc định cho vendor nếu không có custom dialogue
    private List<DialogueEntry> GenerateDefaultVendorDialogue()
    {
        List<DialogueEntry> defaultDialogue = new List<DialogueEntry>();

        // Greeting
        defaultDialogue.Add(new DialogueEntry(
            npcName,
            $"Xin chào! Tôi là {npcName}.",
            vendorImage,
            0f,
            myLayout
        ));

        // Introduction
        defaultDialogue.Add(new DialogueEntry(
            npcName,
            $"Mình bán những sản phẩm {shopCategory} rất chất lượng.",
            vendorImage,
            0f,
            myLayout
        ));

        // Call to action
        defaultDialogue.Add(new DialogueEntry(
            npcName,
            "Bạn muốn xem hàng của mình không?",
            vendorImage,
            0f,
            myLayout
        ));

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
