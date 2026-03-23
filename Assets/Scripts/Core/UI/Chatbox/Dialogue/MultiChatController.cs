using OpenAI;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Trong MultiChatController:

public class MultiChatManager : MonoBehaviour
{
    [Header("Companion Chat System")]
    [SerializeField] private GameObject companionChatButton;
    [SerializeField] private GameObject companionChatPanel;
    [SerializeField] private ScrollRect companionChatScroll;
    [SerializeField] private Transform companionChatContent;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendChatButton;
    [SerializeField] private Button quickChatButton;

    [Header("ParticipantContainer")]
    [SerializeField] private Transform chatParticipantsContainer;
    [SerializeField] private GameObject participantItemPrefab;
    [SerializeField] private DialogueAudioSync audioSync;
    // ✅ THÊM: Auto-open settings
    [Header("Auto-Open Settings")]
    [SerializeField] private bool autoOpenWithShop = true;

    [SerializeField] private List<IChatParticipant> activeParticipants = new List<IChatParticipant>();
    [SerializeField] private IChatParticipant selectedRecipient = null;
    [SerializeField] private CompanionNPC assignedCompanion;
    [SerializeField] private MainMenuViewModel viewModel;
    private bool chatOpenedWithShop = false;
    private Sprite _playerSprite;

    [Header("Debug / Testing")]
    [SerializeField] private bool testProductSuggestions = true; // Bật cái này để test

    private void Start()
    {
       // var mocks = FindObjectsByType<MockChatParticipant>(FindObjectsSortMode.None);
       // foreach (var mock in mocks) AddParticipant(mock);
    }

    public void InitializeWithoutButton(MainMenuViewModel viewModel)
    {
        this.viewModel = viewModel;
        FindAndAssignCompanion();
        SetupEventListeners();
        SetupInitialState();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Cache avatar sprite từ ProfileData (DontDestroyOnLoad từ Scene 1)
        var profileData = FindAnyObjectByType<ProfileData>();
        if (profileData != null)
        {
            _playerSprite = profileData.AvatarSprite; // có thể null nếu API chưa xong
            profileData.OnAvatarChanged += sprite => _playerSprite = sprite;
        }
    }

    public void AddParticipant(IChatParticipant participant) 
    {
        if (participant == null || activeParticipants.Contains(participant))
            return;

        activeParticipants.Add(participant);
        participant.OnJoinChat();
        Debug.Log($"✅ Added {participant.GetParticipantName()} to chat. Total: {activeParticipants.Count}");
        if (companionChatPanel.activeInHierarchy)
            ShowShopWelcome(participant);
    }
    public void RemoveParticipant(IChatParticipant participant) 
    {
        if (participant == null || !activeParticipants.Contains(participant))
            return;

        activeParticipants.Remove(participant);
        participant.OnLeaveChat();
        Debug.Log($"❌ Removed {participant.GetParticipantName()} from chat. Total: {activeParticipants.Count}");

        //UpdateParticipantsList();

        // Show goodbye message
        if (companionChatPanel.activeInHierarchy)
        {
            string goodbyeMsg = $"{participant.GetParticipantName()} đã rời khỏi cuộc trò chuyện.";
            AddChatBubble(goodbyeMsg, isPlayer: false,sender:participant.GetParticipantName() ,icon: participant.GetParticipantIcon());
        }
    }

    public void SendMessage(string message, IChatParticipant recipient = null)
    {
        if (activeParticipants.Count == 0)
        {
            Debug.LogError("NO PARTICIPANTS!");
            return;
        }

        if (AudioManager.Instance != null)
        {
            // Giả sử bạn đã có clip tên "Chat" trong danh sách uiSounds của AudioManager
            AudioManager.Instance.PlaySFXOneShot("Chat");
        }

        if (string.IsNullOrWhiteSpace(message)) return;

        // 1. Hiển thị tin Player
        AddChatBubble(message, isPlayer: true, sender: "Player", icon: _playerSprite);
        AddToSharedContext("Player", message);

        // ✅ 2. PARALLEL RACE - Không quy định trước/sau
        foreach (var participant in activeParticipants)
        {
            if (participant.IsActive())
            {
                StartCoroutine(ProcessParticipantAsync(participant, message));
            }
        }

        chatInputField.text = "";       
    }

    private IEnumerator ProcessParticipantAsync(IChatParticipant participant, string playerMessage)
    {
        // Typing delay

        yield return new WaitForSeconds(Random.Range(0.3f, 1.0f));

        // Build prompt với context mới nhất
        string context = GetContextSummary();
        string prompt = BuildPromptForParticipant(participant, playerMessage, context);

        // Gọi API
        string response = null;
        bool done = false;
        float elapsed = 0f;
        float timeout = participant.GetParticipantType() == ChatParticipantType.VendorNPC ? 8f : 15f;

        // Sync call (tạm thời - nên chuyển sang async)
        response = participant.ProcessMessage(prompt, "Player");
        done = true;

        // Timeout check (nếu dùng async thật)
        // while (!done && elapsed < timeout) {
        //     yield return new WaitForSeconds(0.1f);
        //     elapsed += 0.1f;
        // }

        // Hiển thị
        if (!string.IsNullOrEmpty(response))
        {
            Sprite npcIcon = participant.GetParticipantIcon();

            AddChatBubble(response, false, participant.GetParticipantName(), npcIcon);
            AddToSharedContext(participant.GetParticipantName(), response);
        }
    }

    private void AddToSharedContext(string sender, string message)
    {
        sharedContext.Enqueue(new ChatContextEntry(sender, message));
        if (sharedContext.Count > MAX_CONTEXT_SIZE)
        {
            sharedContext.Dequeue(); // Xóa tin cũ nhất
        }
    }

    private string BuildPromptForParticipant(IChatParticipant participant, string playerMessage, string sharedContext)
    {
        string role = "";
        string specialInstructions = "";

        // ✅ TÙY BIẾN PROMPT THEO LOẠI NPC
        switch (participant.GetParticipantType())
        {
            case ChatParticipantType.VendorNPC:
                role = $"Bạn là Vendor tên {participant.GetParticipantName()}";
                specialInstructions = @"
- Trả lời về sản phẩm, giá cả, chất lượng.
- Đặt tên sản phẩm trong [ngoặc vuông] để tạo link.
- Trả lời ngắn gọn (1-2 câu).";
                break;

            case ChatParticipantType.Companion:
                role = $"Bạn là Companion tên {participant.GetParticipantName()}";
                specialInstructions = @"
- Bạn là bạn thân của Player, đang đi shopping cùng.
- Nếu Vendor đã trả lời (trong lịch sử), hãy bình luận ngắn về lời Vendor.
- Nếu Vendor chưa trả lời, hãy tư vấn hoặc chat thân thiện.
- Trả lời tự nhiên như bạn bè (1-2 câu).";
                break;

            default:
                role = $"Bạn là {participant.GetParticipantName()}";
                specialInstructions = "Trả lời ngắn gọn.";
                break;
        }

        // Inject product context nếu đang xem sản phẩm cụ thể
        string productBlock = "";
        if (_productContext != null)
        {
            productBlock = $"[SẢN PHẨM ĐANG XEM]\n" +
                           $"Tên: {_productContext.title}\n" +
                           $"Giá: {_productContext.price:N0} VND\n" +
                           $"Brand: {_productContext.brandName}\n\n";
        }

        // ✅ PROMPT HOÀN CHỈNH
        return $@"{productBlock}{sharedContext}

[VAI TRÒ]
{role}

[NHIỆM VỤ]
Player vừa nói: ""{playerMessage}""
{specialInstructions}

Hãy trả lời:";
    }
    // File: MultiChatController.cs

    private void FindAndAssignCompanion()
    {
        CompanionNPC[] companions = FindObjectsByType<CompanionNPC>(FindObjectsSortMode.None);

        if (companions.Length > 0)
        {
            assignedCompanion = companions[0];
            Debug.Log($"Assigned companion: {assignedCompanion.GetNPCName()}");
        }
        else
        {
            Debug.LogWarning("No Companion NPC found in scene, u need load companion with player when u play scene");
        }
    }

    private void SetupEventListeners()
    {
        if (companionChatButton != null && assignedCompanion != null)
        {
            companionChatButton.gameObject.SetActive(true);
            sendChatButton.onClick.AddListener(SendCompanionChat);
        }


        if (sendChatButton != null)
            sendChatButton.onClick.AddListener(()=> {           
            SendCompanionMessage();
            });
        audioSync = FindAnyObjectByType<DialogueAudioSync>();
        /*        if (closeChatButton != null)
                    closeChatButton.onClick.AddListener(CloseCompanionChat);*/
    }

    // ✅ THÊM: Listen for ViewModel changes
    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(viewModel.IsShopVisible):
                HandleShopVisibilityChanged();
                break;
        }
    }

    // ✅ THÊM: Auto-open chat when shop opens
    private void HandleShopVisibilityChanged()
    {
        if (!autoOpenWithShop) return;

        if (viewModel.IsShopVisible)
        {
            // Shop opened - auto-open chat
            OpenDialogWithShop();
        }
    }
    private void SetupInitialState()
    {
        if (companionChatPanel != null)
            companionChatPanel.SetActive(false);
    }

    private void SendCompanionChat()
    {
        if (assignedCompanion == null)
        {
            Debug.LogWarning("No companion assigned to chat with");
            return;
        }

        companionChatPanel?.SetActive(true);

        if (IsFirstTimeOpeningChat())
        {
            string welcomeMsg = $"Xin chào! Tôi là {viewModel.CurrentNPCName}, bạn cần hỗ trợ gì không?";
            AddChatBubble(welcomeMsg, isPlayer: false);
        }

        chatInputField?.ActivateInputField();
    }

    private bool IsFirstTimeOpeningChat()
    {
        return companionChatContent != null && companionChatContent.childCount == 0;
    }

    public void CloseCompanionChat()
    {
        companionChatPanel?.SetActive(false);
        ClearChatHistory();
        chatOpenedWithShop = false;
    }

    // Hàm xóa sạch các tin nhắn cũ trong UI
    private void ClearChatHistory()
    {
        if (companionChatContent == null) return;

        // Duyệt ngược từ cuối lên đầu để xóa an toàn
        for (int i = companionChatContent.childCount - 1; i >= 0; i--)
        {
            Destroy(companionChatContent.GetChild(i).gameObject);
        }
    }

    private void SendCompanionMessage()
    {
        if (chatInputField == null || string.IsNullOrWhiteSpace(chatInputField.text))
            return;

        string userMsg = chatInputField.text.Trim();
        chatInputField.text = "";

        // ✅ SỬ DỤNG MULTI-CHAT SYSTEM THAY VÌ CHỈ COMPANION    
        SendMessage(userMsg); // Này sẽ gửi tới TẤT CẢ participants
    }

    private string FormatMessageWithProductLinks(string originalMessage)
    {
        if (string.IsNullOrEmpty(originalMessage)) return "";
        if (viewModel == null || viewModel.CurrentShopData == null) return originalMessage;

        string formattedMessage = originalMessage;
        var shopItems = viewModel.CurrentShopData.ItemsDictionary.Values;

        // Regex tìm cụm từ trong ngoặc vuông
        var regex = new System.Text.RegularExpressions.Regex(@"\[(.*?)\]");

        formattedMessage = regex.Replace(formattedMessage, match =>
        {
            string bracketContent = match.Groups[0].Value; // "[Versace]"
            string innerText = match.Groups[1].Value;      // "Versace"

            // Chuẩn hóa innerText để so sánh dễ hơn
            string cleanInnerText = innerText.Trim();

            foreach (var item in shopItems)
            {

                // Chuẩn hóa tên item
                string cleanItemName = item.itemName.Trim();

                // 1. So sánh chính xác (Ưu tiên cao nhất)
                if (string.Equals(cleanItemName, cleanInnerText, System.StringComparison.OrdinalIgnoreCase))
                {
                    return $"<link=\"{item.itemID}\"><color=#FF0000><b>[{innerText}]</b></color></link>";
                }

                // 2. So sánh chứa (Nới lỏng): Nếu tên trong ngoặc là một phần của tên item (hoặc ngược lại)
                // Ví dụ: Tin nhắn "[Versace]" nhưng item là "Versace T-Shirt" -> Vẫn bắt dính
                // Lưu ý: Cẩn thận nếu tên quá ngắn (ví dụ "A") sẽ bắt nhầm lung tung.
                if (cleanInnerText.Length > 3 && cleanItemName.IndexOf(cleanInnerText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Tìm thấy item chứa từ khóa này -> Link tới item đó
                    return $"<link=\"{item.itemID}\"><color=#FF0000><b>[{innerText}]</b></color></link>";
                }

                Debug.Log($"Checking: '{cleanInnerText}' vs '{cleanItemName}'");
            }

            return bracketContent; // Không tìm thấy -> giữ nguyên
        });

        return formattedMessage;
    }

    private void AddChatBubble(string message, bool isPlayer, string sender = "", Sprite icon = null)
    {
        if (chatMessagePrefab == null || companionChatContent == null) return;

        var go = Instantiate(chatMessagePrefab, companionChatContent);
        var ui = go.GetComponent<ChatMessageUI>();
     //   Canvas.ForceUpdateCanvases();

        // ✅ FIX 1: Disable layout before setup to prevent intermediate layouts
        var layoutGroup = companionChatContent.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }

        // if (ui != null)
        {
            string displaySender = isPlayer ? "Player" : sender;

            // Nếu là tin nhắn từ NPC (AI), hãy format link sản phẩm
            string finalMessage = message;
            if (!isPlayer)
            {
                finalMessage = FormatMessageWithProductLinks(message);
            }

            ui.Setup(finalMessage, displaySender, isPlayer, icon);
        }

        // ✅ FIX 2: Ensure LayoutElement exists on chat bubble for proper sizing
        if (go.GetComponent<LayoutElement>() == null)
        {
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = -1; // Let it calculate naturally
        }

        // ✅ FIX 3: Re-enable layout and force complete rebuild
        if (layoutGroup != null)
        {
            layoutGroup.enabled = true;
        }

        // ✅ FIX 4: Force canvas updates and layout group rebuild immediately
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(companionChatScroll.content);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private System.Collections.IEnumerator ScrollToBottomNextFrame()
    {
        yield return new WaitForEndOfFrame();

        yield return null;
        ScrollChatToBottom();
    }



    // Hàm này sẽ giả lập NPC đề xuất ngẫu nhiên 2-3 sản phẩm đang có trong shop
    // Hàm này sẽ giả lập NPC đề xuất ngẫu nhiên 2-3 sản phẩm đang có trong shop
    public void Debug_TestProductSuggestion()
    {
        if (viewModel == null || viewModel.CurrentShopData == null)
        {
            Debug.LogWarning("Không thể test vì chưa có dữ liệu Shop!");
            return;
        }

        var items = new List<ShopItem>(viewModel.CurrentShopData.ItemsDictionary.Values);
        if (items.Count == 0) return;

        // Chọn ngẫu nhiên 2 sản phẩm
        string suggestionText = "Hôm nay shop mình có về mấy mẫu mới ";

        // Lấy random 2-3 món
        int count = Mathf.Min(1, items.Count);
        for (int i = 0; i < count; i++)
        {
            var randomItem = items[Random.Range(0, items.Count)];
            // Format quan trọng: Phải đặt trong ngoặc vuông [] để hệ thống tự nhận diện
            suggestionText += $"[{randomItem.itemName}] ";
        }

        suggestionText += ". Bạn thấy sao?";

        // 1. Lấy tên từ ViewModel
        string participantName = viewModel.CurrentNPCName;
        // ✅ TỐI ƯU: Tìm icon của NPC có tên khớp với participantName
        Sprite icon = activeParticipants.FirstOrDefault(p => p.GetParticipantName() == participantName)?.GetParticipantIcon();

        // 3. Giả lập NPC gửi tin nhắn (Có kèm Icon)
        AddChatBubble(suggestionText, isPlayer: false, sender: participantName, icon: icon);
    }


    public void OpenDialogWithShop()
    {
        if (assignedCompanion == null)
        {
            FindAndAssignCompanion();
        }

        if (assignedCompanion == null)
        {
            Debug.LogWarning("No companion assigned to chat with");
            return;
        }

        companionChatPanel.SetActive(true);
        chatOpenedWithShop = true;

        // ✅ QUAN TRỌNG: Add Companion vào chat
        if (assignedCompanion != null && !activeParticipants.Contains(assignedCompanion))
        {
            AddParticipant(assignedCompanion);
        }
        VendorNPC currentVendor = FindFirstObjectByType<VendorNPC>();
        if (currentVendor != null && !activeParticipants.Contains(currentVendor))
        {
            AddParticipant(currentVendor);
        }
        // Add welcome message if first time
        if (IsFirstTimeOpeningChat())
        {
            string welcomeMsg =
            $"Chào {viewModel.CurrentNPCName}! Tôi là {assignedCompanion.GetNPCName()}, tôi có thể hỗ trợ bạn tìm sản phẩm phù hợp!";
            AddChatBubble(welcomeMsg, isPlayer: false, icon: assignedCompanion.GetParticipantIcon());
            // THÊM ĐOẠN NÀY ĐỂ TEST
            if (testProductSuggestions)
            {
                // Delay nhẹ 1 xíu cho tự nhiên
                Invoke(nameof(Debug_TestProductSuggestion), 1.0f);
            }
        }

        // Don't auto-focus input field to avoid interrupting shopping
        // chatInputField?.ActivateInputField();

        Debug.Log("Chat opened automatically with shop");
    }

    private void ScrollChatToBottom()
    {
        if (companionChatScroll == null) return;
        Canvas.ForceUpdateCanvases();
        companionChatScroll.verticalNormalizedPosition = 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)companionChatScroll.content);

    }

    public void OnPlayerLeavingNPC()
    {
        if (companionChatPanel != null)
            companionChatPanel.SetActive(false);
            ClearChatHistory();
    }

    #region Product Context
    private ProductDetail _productContext;

    private Transform _originalChatParent;
    private int _originalSiblingIndex;

    // RectTransform override khi companionChatScroll hiện trong ProductDetailUI
    [Header("Product Detail Chat Layout")]
    [SerializeField] private float productDetailScrollPosY = 0f;
    [SerializeField] private float productDetailScrollHeight = 300f;

    // Lưu giá trị gốc để restore
    private float _originalScrollPosY;
    private float _originalScrollHeight;

    public void SetProductContext(ProductDetail detail)
    {
        _productContext = detail;
    }

    public void ClearProductContext()
    {
        _productContext = null;
    }

    public void ReparentChatPanelTo(Transform anchor)
    {
        if (companionChatPanel == null || anchor == null) return;

        // Chỉ lưu parent gốc 1 lần — tránh overwrite khi chuyển sản phẩm mà chưa đóng panel
        if (_originalChatParent == null)
        {
            _originalChatParent = companionChatPanel.transform.parent;
            _originalSiblingIndex = companionChatPanel.transform.GetSiblingIndex();

            if (companionChatScroll != null)
            {
                RectTransform rt = companionChatScroll.GetComponent<RectTransform>();
                _originalScrollPosY = rt.anchoredPosition.y;
                _originalScrollHeight = rt.sizeDelta.y;
            }
        }

        companionChatPanel.transform.SetParent(anchor, false);

        // Áp dụng RectTransform mới cho companionChatScroll
        if (companionChatScroll != null)
        {
            RectTransform rt = companionChatScroll.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, productDetailScrollPosY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, productDetailScrollHeight);
        }
    }

    public void RestoreChatPanel()
    {
        if (_originalChatParent == null) return;

        companionChatPanel.transform.SetParent(_originalChatParent, false);
        companionChatPanel.transform.SetSiblingIndex(_originalSiblingIndex);
        _originalChatParent = null;

        // Khôi phục RectTransform gốc của companionChatScroll
        if (companionChatScroll != null)
        {
            RectTransform rt = companionChatScroll.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _originalScrollPosY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, _originalScrollHeight);
        }
    }

    public void ShowProductWelcome()
    {
        if (_productContext == null || activeParticipants.Count == 0) return;

        foreach (var participant in activeParticipants)
        {
            if (!participant.IsActive()) continue;

            string message = participant.GetParticipantType() switch
            {
                ChatParticipantType.VendorNPC =>
                    $"Bạn đang xem [{_productContext.title}] — {_productContext.price:N0} VND. Bạn cần tư vấn gì không?",
                ChatParticipantType.Companion =>
                    $"Ồ [{_productContext.title}] này trông ổn đó! Mình thấy hợp với bạn đấy~",
                _ => $"{participant.GetParticipantName()} chào bạn."
            };

            AddChatBubble(message, isPlayer: false,
                sender: participant.GetParticipantName(),
                icon: participant.GetParticipantIcon());
        }
    }
    #endregion

    #region Save and Load Chat History
    [System.Serializable]
    public class ChatContextEntry
    {
        public string senderName;
        public string message;
        public float timestamp;

        public ChatContextEntry(string sender, string msg)
        {
            senderName = sender;
            message = msg;
            timestamp = Time.time;
        }
    }

    // Thêm vào trong class MultiChatManager
    private Queue<ChatContextEntry> sharedContext = new Queue<ChatContextEntry>();
    private const int MAX_CONTEXT_SIZE = 5; // Giữ 5 tin nhắn gần nhất

   

    // Hàm lấy context để gửi cho NPC
    private string GetContextSummary()
    {
        if (sharedContext.Count == 0) return "";

        string summary = "[Lịch sử chat gần đây]\n";
        foreach (var entry in sharedContext)
        {
            summary += $"{entry.senderName}: {entry.message}\n";
        }
        return summary;
    }
    #endregion

    public void ShowShopWelcome(IChatParticipant participant)
    {
        if (participant == null || !participant.IsActive())
            return;

        // Choose message by participant type (clear and O(1))
        string message = participant.GetParticipantType() switch
        {        
            ChatParticipantType.VendorNPC => "Xin chào Luci, đây là cửa hàng nước hoa tại Thành Phố Golden West, và đây là 1 số sự lựa chọn",
            ChatParticipantType.Companion => "Mình tìm được vài mùi hương hợp với bạn — xem thử nhé!",
            _ => $"{participant.GetParticipantName()} chào bạn.", 
        };

        // Only activate the panel if it's not already active (avoid redundant SetActive calls)
        
        // 4, 1 , 3 , r 
        // Add a single bubble for the welcome message
        AddChatBubble(message, isPlayer: false, sender: participant.GetParticipantName(), icon: participant.GetParticipantIcon());
    }
}



