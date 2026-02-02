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


    private void Start()
    {
        NameplateManager.Instance.Register(this.transform, npcName);

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
            aiPersonality = $"You are a {vendorConfig.shopCategory} vendor. Be professional, knowledgeable about your products, and help customers make purchasing decisions.";
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
        Debug.Log($"Vendor {npcName}: Player left range");
        isCustomerNearby = false;
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
        if (vendorConfig != null)
        {
            string[] vendorResponses = {
                $"Chào mừng đến {vendorConfig.shopCategory} store của tôi!",
                "Hôm nay bạn muốn mua gì?",
                $"Tôi có những {vendorConfig.shopCategory} chất lượng nhất!",
                "Bạn có thể xem qua shop của tôi không?"
            };
            
            return vendorResponses[Random.Range(0, vendorResponses.Length)];
        }

        return "Chào mừng đến cửa hàng của tôi!";
    }

    // ✅ THÊM Method để VendorNPC cũng có AI Response
    public override string GetAIResponse(string playerMessage)
    {
        string lowerMessage = playerMessage.ToLower();

        // Shop context responses
        if (lowerMessage.Contains("help") || lowerMessage.Contains("giúp"))
            return "Tôi có thể giúp bạn tìm sản phẩm phù hợp! Bạn đang cần gì?";

        if (lowerMessage.Contains("recommend") || lowerMessage.Contains("gợi ý"))
            return $"Tôi gợi ý bạn xem các sản phẩm {vendorConfig?.shopCategory} bán chạy nhất của shop!";

        return GetDefaultResponse();
    }

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
        dynamicShopData.shopName = $"{vendorConfig.npcName}'s {vendorConfig.shopCategory} Store";

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

    // ✅ NEW: Override GetDialogueSequence để setup dialogue của Vendor
    public override List<DialogueEntry> GetDialogueSequence()
    {
        if (!customDialogueEnabled || dialogueSequence.Count == 0)
        {
            return GenerateDefaultVendorDialogue();
        }

        return new List<DialogueEntry>(dialogueSequence);
    }

    /// <summary>
    /// Tạo dialogue mặc định cho vendor nếu không có custom dialogue
    /// </summary>
    private List<DialogueEntry> GenerateDefaultVendorDialogue()
    {
        List<DialogueEntry> defaultDialogue = new List<DialogueEntry>();

        // Greeting
        defaultDialogue.Add(new DialogueEntry(
            npcName,
            $"Xin chào! Tôi là {npcName}.",
            vendorImage,
            0f
        ));

        // Introduction
        defaultDialogue.Add(new DialogueEntry(
            npcName,
            $"Mình bán những sản phẩm {shopCategory} rất chất lượng.",
            vendorImage,
            0f
        ));

        // Call to action
        defaultDialogue.Add(new DialogueEntry(
            npcName,
            "Bạn muốn xem hàng của mình không?",
            vendorImage,
            0f
        ));

        return defaultDialogue;
    }
    public override string ProcessMessage(string message, string sender)
    {
        Debug.Log($"[{GetParticipantName()}] Processing message from {sender}: '{message}'");

        // TODO: Gọi API OpenAI ở đây (code bạn đã có trong GetAIResponse)
        // Tạm thời return mock response để test
        return $"Xin chào! Tôi là {GetParticipantName()}. Bạn vừa nói: {message}";
    }
    // Getter cho shop data
    public ShopData GetShopData() => dynamicShopData ?? defaultShopData;
    public NPCAPIConfig GetVendorConfig() => vendorConfig;

    public Sprite GetVendorImage() => vendorIconBT;
}
