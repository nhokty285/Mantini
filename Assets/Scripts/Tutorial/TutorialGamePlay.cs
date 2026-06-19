using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Tutorial workflow cho scene MapTest2 — refactored UX:
///  • Approach + NPCClick: dẫn dắt 1 lần đầu (có spotlight + lock button shop).
///  • Các section còn lại (Chat, SelectItem, ProductDetail, BackToShop,
///    OpenBag, Cart, Checkout, Reward): chỉ hiện companion dialogue + spotlight
///    LẦN ĐẦU khi player tự mở; KHÔNG khóa button, KHÔNG chặn thao tác.
///  • Đã thấy section → không hiện lại (lưu PlayerPrefs).
///
/// HOOKS Ở CÁC FILE KHÁC — GIỮ NGUYÊN TÊN, KHÔNG CẦN SỬA:
///   VendorNPC.OnPlayerEnterRange()      → TutorialGamePlay.Instance?.OnPlayerEnterNPCRange()
///   VendorNPC.ProcessInteraction()      → TutorialGamePlay.Instance?.OnPlayerClickedNPC()
///   BaseNPC.HideDialogueAndOpenShop()   → TutorialGamePlay.Instance?.OnHideDialogueAndOpenShop()
///   MultiChatController (SendChatButton click) → TutorialGamePlay.Instance?.OnPlayerSentChat()
///   DifyChatService (response)          → TutorialGamePlay.Instance?.OnAIResponseReceived()
///   ShopController.OnProductLinkCallback()  → TutorialGamePlay.Instance?.OnPlayerTappedItem()
///   ProductDetailUI (add to cart ok)    → TutorialGamePlay.Instance?.OnAddToCartSuccess()
///   ProductDetailUI (back button)       → TutorialGamePlay.Instance?.OnPlayerBackToShop()
///   ShopController (cartButton click)   → TutorialGamePlay.Instance?.OnCartOpened()
///   CartUI (addSelectedToCart ok)       → TutorialGamePlay.Instance?.OnAddSelectedToCartSuccess()
///   CheckoutController                  → TutorialGamePlay.Instance?.OnCheckoutCompleted()
/// </summary>
public class TutorialGamePlay : MonoBehaviour
{
    public static TutorialGamePlay Instance { get; private set; }

    /// <summary>
    /// Section hiện tại player đang ở (= section đầu tiên CHƯA seen).
    /// Dùng cho các script ngoài (như QuestNavigator) — nguồn truth duy nhất
    /// để xác định quest step, không cần đọc PlayerPrefs trực tiếp.
    /// </summary>
    public TutorialSection CurrentSection
    {
        get
        {
            foreach (var s in AllSections)
                if (!_seenSections.Contains(s)) return s;
            return TutorialSection.Reward;
        }
    }

    // ── PlayerPrefs Keys ────────────────────────────────────────────────────
    private const string KEY_ENABLED      = "MapTest2_TutorialEnabled";
    private const string KEY_SEEN_PREFIX  = "MapTest2_TutSeen_";
    // Legacy keys (giữ để clean khi reset)
    private const string KEY_LEGACY_DONE  = "MapTest2_TutorialDone";
    private const string KEY_LEGACY_STEP  = "MapTest2_TutorialStep";

    // ════════════════════════════════════════════════════════════════════════
    // ENUM — mỗi section là 1 đơn vị độc lập, có thể trigger riêng
    // ════════════════════════════════════════════════════════════════════════
    public enum TutorialSection
    {
        Approach,        // [Critical] Companion chào + arrow đến NPC
        NPCClick,        // [Critical] Hiện hint "bấm vào NPC"
        Chat,            // [Hint]     Shop vừa mở → giới thiệu ô chat
        SelectItem,      // [Hint]     Player nhấn Send → giới thiệu khu vực item
        ProductDetail,   // [Hint]     Mở ProductDetailUI → giới thiệu chọn size
        BackToShop,      // [Hint]     Add to cart xong → gợi ý quay lại shop
        OpenBag,         // [Hint]     Quay lại shop → gợi ý nút giỏ
        Cart,            // [Hint]     Cart panel mở → giới thiệu cart
        Checkout,        // [Hint]     Đã add to cart → giới thiệu checkout
        Reward           // [Hint]     Checkout xong → chúc mừng
    }

    // Refactor: cache mảng enum để tránh Enum.GetValues() (alloc + reflection) mỗi lần dùng.
    // Số section nhỏ nhưng được duyệt nhiều chỗ (load/save/reset/debug UI).
    private static readonly TutorialSection[] AllSections =
        (TutorialSection[])System.Enum.GetValues(typeof(TutorialSection));

    // ── Internal State ──────────────────────────────────────────────────────
    private readonly HashSet<TutorialSection> _seenSections = new HashSet<TutorialSection>();
    private bool _isTutorialEnabled = true;
    private bool _continuePressed = false;
    private bool _introFlowDone = false;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — SETTINGS
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ SETTINGS ══════")]
    [Tooltip("Bật/tắt toàn bộ tutorial. Khi tắt, game chạy bình thường.")]
    [SerializeField] private bool tutorialEnabled = true;

    [Tooltip("Bật để bỏ qua check PlayerPrefs — luôn chạy tutorial khi load scene (dùng khi test).")]
    [SerializeField] private bool forceRunOnStart = false;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — COMPANION PANEL
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Companion Panel ══════")]
    [SerializeField] private GameObject companionPanel;
    [SerializeField] private Image companionImage;
    [SerializeField] private TextMeshProUGUI companionChatText;
    [SerializeField] private Button companionContinueButton;
    [SerializeField] private Sprite tutorialCompanionSprite;
    [SerializeField] private float typewriterSpeed = 0.025f;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private DialogueAudioSync audioSync;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — SPOTLIGHT
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Spotlight Overlay ══════")]
    [SerializeField] private GameObject spotlightOverlay;
    [SerializeField] private RectTransform spotlightHole;
    [SerializeField] private Vector2 spotlightPadding = new Vector2(20f, 220f);
    [SerializeField] private float autoHideDelay = 2f;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — STEP APPROACH (Critical)
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Approach NPC (Critical Section) ══════")]
    // ⚠️ Các field dưới có thể đã được wire trong Inspector — giữ lại để không phá reference.
    // Nếu confirmed chưa wire, có thể xoá sau:
    [SerializeField] private Transform firstNPCWorldTransform;
    [SerializeField] private RectTransform firstNPCScreenRect;
    [SerializeField] private float arrowUpdateInterval = 0.08f;
    [SerializeField] private GameObject spotlightEffectPrefab;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — SHOP UI
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Shop UI References ══════")]
    [SerializeField] private RectTransform chatInputArea;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private RectTransform shopItemsContainerRect;
    [SerializeField] private RectTransform shopScrollViewRect;
    [SerializeField] private Button quickCartButton;
    [SerializeField] private RectTransform cartPanel;
    [SerializeField] private GameObject productDetailPanel;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — CART UI
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ CartUI References ══════")]
    [SerializeField] private RectTransform cartItemListRect;
    [SerializeField] private Button addSelectedToCartBtn;
    [SerializeField] private RectTransform checkoutButtonRect;
    [SerializeField] private Button closeCart;

    [Header("══════ Blockable Buttons (CHỈ khóa ở Intro/Approach) ══════")]
    [SerializeField] private Button[] allBlockableButtons;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — MESSAGES
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Messages ══════")]
    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step1_Intro =
        "Hãy bắt đầu làm quen với thao tác nhé!\nHãy di chuyển đến NPC phía trước\nđể bắt đầu mua sắm nhé!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step1_Click =
        "Bạn đã đến nơi rồi!\nHãy bấm vào NPC để mở Shop!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Chat =
        "Đây là ô Chat với AI!\nBạn có thể hỏi về sản phẩm, size, hay phong cách.\nHãy thử gõ một câu hỏi và nhấn Gửi nhé!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Item =
        "Tuyệt! Bây giờ hãy chọn một sản phẩm\nbạn thích từ khu vực hàng hóa phía dưới!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Size =
        "Đây là chi tiết sản phẩm!\n• Chọn SIZE phù hợp với bạn\n• Sau đó nhấn \"Thêm vào giỏ\" để tiếp tục!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Back =
        "Đã thêm vào giỏ thành công!\nHãy nhấn nút <- Trở Lại để quay về Shop.";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Bag =
        "Ngon lắm! Bây giờ hãy bấm vào nút\nTUI (gio hang) để xem giỏ hàng của bạn!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Cart =
        "Đây là Giỏ Hàng của bạn!\n• Tick chọn sản phẩm muốn mua\n• Nhấn \"Thêm vào đơn\" để tiến hành!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step3 =
        "Tuyệt vời! Điền thông tin giao hàng\nvà nhấn THANH TOÁN để hoàn tất đơn đầu tiên!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Reward =
        "DON HANG DAU TIEN HOAN THANH!\nBạn đã thành thạo cơ bản.\nTiếp tục khám phá thêm nhé!";

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — DEBUG PANEL
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Debug Panel ══════")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private GameObject debugPanelUI;
    [SerializeField] private TextMeshProUGUI debugStepText;
    [SerializeField] private TextMeshProUGUI debugStatusText;

    // ════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (audioSync == null) audioSync = FindAnyObjectByType<DialogueAudioSync>();
        HideAllTutorialUI();
        if (spotlightEffectPrefab != null) spotlightEffectPrefab.SetActive(false);
    }

    private void Start()
    {
        _isTutorialEnabled = PlayerPrefs.GetInt(KEY_ENABLED, tutorialEnabled ? 1 : 0) == 1;
        LoadSeenSections();

        if (companionContinueButton != null)
        {
            companionContinueButton.onClick.RemoveAllListeners();
            // Refactor: tách lambda thành method có tên (tránh alloc closure khó debug stack)
            companionContinueButton.onClick.AddListener(OnCompanionContinueClicked);
        }

        UpdateDebugUI();

        if (!_isTutorialEnabled)
        {
            DebugLog("Tutorial đang TAT — bỏ qua, không khóa gì hết.");
            RestoreAllShopUI();
            return;
        }

        // Chỉ chạy intro guided flow nếu chưa thấy
        if (!HasSeen(TutorialSection.Approach))
        {
            StartCoroutine(IntroGuidedFlow());
        }
        else
        {
            DebugLog("Intro đã thấy — bỏ qua. Hint sections sẽ tự trigger qua hooks.");
            _introFlowDone = true;
            RestoreAllShopUI();
            ActivateContextualTooltips();
        }
    }

    // Refactor: bỏ Update() poll mỗi frame (Unity vẫn call empty Update qua reflection).
    // Debug UI đã được refresh từ MarkSeen / Enable / Disable / Skip — không cần poll.

    private void OnCompanionContinueClicked()
    {
        OnContinuePressed();
        HideAllTutorialUI();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region PUBLIC — BAT / TAT TUTORIAL
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Bật tutorial (lưu vào PlayerPrefs).</summary>
    public void EnableTutorial()
    {
        _isTutorialEnabled = true;
        PlayerPrefs.SetInt(KEY_ENABLED, 1);
        PlayerPrefs.Save();
        DebugLog("Tutorial đã BAT.");
        UpdateDebugUI();
    }

    /// <summary>Tắt tutorial — ẩn toàn bộ UI và restore shop.</summary>
    public void DisableTutorial()
    {
        _isTutorialEnabled = false;
        PlayerPrefs.SetInt(KEY_ENABLED, 0);
        PlayerPrefs.Save();
        StopAllCoroutines();
        RestoreAllShopUI();
        HideAllTutorialUI();
        DebugLog("Tutorial đã TAT.");
        UpdateDebugUI();
    }

    /// <summary>Toggle bật/tắt — dùng cho UI Button.</summary>
    public void ToggleTutorial()
    {
        if (_isTutorialEnabled) DisableTutorial();
        else EnableTutorial();
    }

    public bool IsTutorialEnabled() => _isTutorialEnabled;

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region SEEN-TRACKING (HashSet O(1) + PlayerPrefs)
    // ════════════════════════════════════════════════════════════════════════

    private void LoadSeenSections()
    {
        _seenSections.Clear();
        if (forceRunOnStart)
        {
            DebugLog("forceRunOnStart = true -> reset _seenSections.");
            return;
        }

        // Refactor: dùng AllSections cache, tránh Enum.GetValues() mỗi lần
        foreach (var s in AllSections)
        {
            if (PlayerPrefs.GetInt(KEY_SEEN_PREFIX + s.ToString(), 0) == 1)
                _seenSections.Add(s);
        }
        DebugLog($"Loaded {_seenSections.Count} section(s) đã thấy từ PlayerPrefs.");
    }

    private bool HasSeen(TutorialSection s) => _seenSections.Contains(s);

    private void MarkSeen(TutorialSection s)
    {
        if (!_seenSections.Add(s)) return; // đã có trong set, bỏ qua
        PlayerPrefs.SetInt(KEY_SEEN_PREFIX + s.ToString(), 1);
        PlayerPrefs.Save();
        DebugLog($"Marked seen: {s}");
        UpdateDebugUI();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region HINT API — Mỗi section chỉ hiện 1 lần, không block
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gọi từ hook khi player tự mở 1 section. Nếu chưa thấy -> hiện dialogue +
    /// spotlight (optional) rồi tự tắt. Đã thấy -> return ngay, không làm gì.
    /// KHÔNG block button, KHÔNG chặn thao tác player.
    /// </summary>
    private void TryShowHint(
        TutorialSection section,
        string msg,
        RectTransform spotlightTarget = null,
        bool waitContinue = true)
    {
        if (!_isTutorialEnabled) return;
        if (HasSeen(section))
        {
            DebugLog($"Section {section} đã thấy — bỏ qua.");
            return;
        }

        MarkSeen(section);
        StartCoroutine(HintRoutine(section, msg, spotlightTarget, waitContinue));
    }

    private IEnumerator HintRoutine(
        TutorialSection section,
        string msg,
        RectTransform spotlightTarget,
        bool waitContinue)
    {
        UpdateProgressText(section);

        if (spotlightTarget != null)
            ShowSpotlight(spotlightTarget);

        // ShowCompanionMessage tự xử lý:
        //   waitContinue = true  -> hiện Continue button, đợi player bấm
        //   waitContinue = false -> auto-hide sau autoHideDelay giây
        yield return ShowCompanionMessage(msg, wait: waitContinue);

        // Clean up — companion message đã tự hide khi wait=true; cần hide spotlight
        HideSpotlight();
        if (!waitContinue) HideCompanionPanel();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region INTRO GUIDED FLOW (Critical — Approach + NPCClick)
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator IntroGuidedFlow()
    {
        MarkSeen(TutorialSection.Approach);
        UpdateProgressText(TutorialSection.Approach);

        // Phase A: Companion chào
        if (spotlightOverlay != null) spotlightOverlay.SetActive(true);
        yield return ShowCompanionMessage(msg_Step1_Intro, wait: false);
        yield return new WaitForSeconds(2f);
        HideCompanionPanel();
        HideSpotlight();

        // Phase B: Bật spotlight effect prefab + sfx dẫn đường
        if (spotlightEffectPrefab != null) spotlightEffectPrefab.SetActive(true);
        AudioManager.Instance?.PlaySFXOneShot("Whoosh");

        // Lock button shop trong giai đoạn intro để player tập trung đi tới NPC
        LockAllBlockableButtons();

        DebugLog("Intro flow: chờ OnPlayerEnterNPCRange()...");
    }

    /// <summary>Hook -> VendorNPC.OnPlayerEnterRange()</summary>
    public void OnPlayerEnterNPCRange()
    {
        if (!_isTutorialEnabled) return;

        // Đã qua intro rồi -> không cần làm gì khi player đi ngang NPC
        if (HasSeen(TutorialSection.NPCClick)) return;

        MarkSeen(TutorialSection.NPCClick);
        UpdateProgressText(TutorialSection.NPCClick);
        StartCoroutine(ShowNPCClickHint());
    }

    private IEnumerator ShowNPCClickHint()
    {
        yield return ShowCompanionMessage(msg_Step1_Click, wait: false);
        yield return new WaitForSeconds(1.8f);
        HideAllTutorialUI();
    }

    /// <summary>Hook -> VendorNPC.ProcessInteraction()</summary>
    public void OnPlayerClickedNPC()
    {
        if (!_isTutorialEnabled) return;

        HideSpotlight();
        HideAllTutorialUI();
        if (spotlightEffectPrefab != null) spotlightEffectPrefab.SetActive(false);

        // Mở khóa toàn bộ shop UI — từ đây trở đi player tự do
        RestoreAllShopUI();
        _introFlowDone = true;
        DebugLog("Player bấm NPC — intro DONE, mở khóa toàn bộ UI.");
    }

    /// <summary>Hook -> BaseNPC.HideDialogueAndOpenShop()</summary>
    public void OnHideDialogueAndOpenShop()
    {
        TryShowHint(TutorialSection.Chat, msg_Step2_Chat, chatInputArea, waitContinue: true);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region HINT HOOKS — Passive, không block
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hook -> MultiChatController.SendChatButton.onClick
    /// Đánh dấu bước Chat hoàn thành và trigger SelectItem hint NGAY KHI player nhấn Send,
    /// KHÔNG chờ API trả về.
    /// </summary>
    public void OnPlayerSentChat()
    {
        // Đánh dấu bước Chat đã hoàn thành (nếu chưa)
        MarkSeen(TutorialSection.Chat);

        // Trigger SelectItem hint ngay lập tức, không chờ AI response
        TryShowHint(TutorialSection.SelectItem, msg_Step2_Item, shopItemsContainerRect, waitContinue: true);
    }

    /// <summary>Hook -> DifyChatService (AI trả về). No-op — SelectItem đã trigger từ OnPlayerSentChat.</summary>
    public void OnAIResponseReceived()
    {
        // no-op: SelectItem hint đã được trigger ngay khi player nhấn Send (OnPlayerSentChat),
        // không cần chờ AI response nữa.
    }

    /// <summary>Hook -> ShopController.OnProductLinkCallback() (player tap item).</summary>
    public void OnPlayerTappedItem()
        => TryShowHint(TutorialSection.ProductDetail, msg_Step2_Size, null, waitContinue: true);

    /// <summary>Hook -> ProductDetailUI (add to cart thành công).</summary>
    public void OnAddToCartSuccess()
        => TryShowHint(TutorialSection.BackToShop, msg_Step2_Back, null, waitContinue: false);

    /// <summary>Hook -> ProductDetailUI (back button).</summary>
    public void OnPlayerBackToShop()
    {
        RectTransform cartBtnRect = quickCartButton != null
            ? quickCartButton.GetComponent<RectTransform>()
            : null;
        TryShowHint(TutorialSection.OpenBag, msg_Step2_Bag, cartBtnRect, waitContinue: true);
    }

    /// <summary>Hook -> ShopController (cartButton onClick -> cartPanel active).</summary>
    public void OnCartOpened()
        => TryShowHint(TutorialSection.Cart, msg_Step2_Cart, cartItemListRect, waitContinue: true);

    /// <summary>Hook -> CartUI.addSelectedToCartButton.onClick</summary>
    public void OnAddSelectedToCartSuccess()
        => TryShowHint(TutorialSection.Checkout, msg_Step3, null, waitContinue: false);

    /// <summary>Hook -> CheckoutController (thanh toán xong).</summary>
    public void OnCheckoutCompleted()
    {
        TryShowHint(TutorialSection.Reward, msg_Reward, null, waitContinue: true);
        ActivateContextualTooltips();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region CONTEXTUAL TOOLTIPS
    // ════════════════════════════════════════════════════════════════════════

    private void ActivateContextualTooltips()
    {
        var triggers = FindObjectsByType<ContextualTooltipTrigger>(FindObjectsSortMode.None);
        foreach (var t in triggers) t.Enable();
        DebugLog($"Đã bật {triggers.Length} contextual tooltip(s).");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region COMPANION HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator ShowCompanionMessage(string msg, bool wait, bool showPanel = true)
    {
        if (companionPanel == null) yield break;

        companionPanel.SetActive(showPanel);

        if (companionImage != null)
        {
            Vector3 originalScale = companionImage.transform.localScale;
            companionImage.transform.localScale = Vector3.zero;
            companionImage.transform.DOScale(originalScale, 0.5f).SetEase(Ease.OutBack);

            if (tutorialCompanionSprite != null)
                companionImage.sprite = tutorialCompanionSprite;
        }

        if (companionChatText != null)
        {
            companionChatText.text = msg;
            companionChatText.maxVisibleCharacters = 0;

            // Refactor: cache audio check 1 lần ngoài loop (tránh check null + property mỗi char)
            bool canPlayTypeSound = audioSync != null && audioSync.typewriterSound != null;
            AudioClip typeClip = canPlayTypeSound ? audioSync.typewriterSound : null;
            int msgLen = msg.Length;

            for (int i = 0; i <= msgLen; i++)
            {
                companionChatText.maxVisibleCharacters = i;

                if (i < msgLen)
                {
                    char currentChar = msg[i];
                    bool shouldPlayTypewriter =
                        !char.IsWhiteSpace(currentChar) &&
                        !char.IsPunctuation(currentChar);

                    if (shouldPlayTypewriter && canPlayTypeSound)
                        AudioManager.Instance?.PlayTypewriter(typeClip, 0.4f);

                    yield return new WaitForSeconds(typewriterSpeed);
                }
            }

            AudioManager.Instance?.StopTypewriter();
        }

        if (wait)
        {
            if (companionContinueButton != null)
                companionContinueButton.gameObject.SetActive(true);

            _continuePressed = false;
            yield return new WaitUntil(() => _continuePressed);
            // Refactor: HideAllTutorialUI đã gọi HideCompanionPanel, không cần call riêng
            HideAllTutorialUI();
        }
        else
        {
            if (companionContinueButton != null)
                companionContinueButton.gameObject.SetActive(false);
            yield return new WaitForSeconds(autoHideDelay);
        }
    }

    private void OnContinuePressed() => _continuePressed = true;

    private void HideCompanionPanel()
    {
        AudioManager.Instance?.StopTypewriter();
        companionPanel?.SetActive(false);
        companionContinueButton?.gameObject.SetActive(false);
        _continuePressed = false;
    }

    /// <summary>
    /// Khóa toàn bộ allBlockableButtons.
    /// CHỈ dùng trong IntroGuidedFlow. Sau khi OnPlayerClickedNPC -> RestoreAllShopUI.
    /// Refactor: trước đây nhận params Button[] allowed nhưng caller luôn truyền 0 args
    /// → bỏ HashSet allocation, đơn giản hoá thành "lock tất cả".
    /// </summary>
    private void LockAllBlockableButtons()
    {
        if (allBlockableButtons == null) return;
        foreach (var btn in allBlockableButtons)
        {
            if (btn != null) btn.interactable = false;
        }
    }

    private void UpdateProgressText(TutorialSection section)
    {
        if (progressText == null) return;

        string text = section switch
        {
            TutorialSection.Approach       => "Di chuyen den NPC",
            TutorialSection.NPCClick       => "Bam vao NPC de mo Shop",
            TutorialSection.Chat           => "Kham pha Chat AI",
            TutorialSection.SelectItem     => "Chon san pham",
            TutorialSection.ProductDetail  => "Chi tiet san pham",
            TutorialSection.BackToShop     => "Quay lai Shop",
            TutorialSection.OpenBag        => "Mo gio hang",
            TutorialSection.Cart           => "Trong gio hang",
            TutorialSection.Checkout       => "Thanh toan",
            TutorialSection.Reward         => "Hoan thanh!",
            _                              => ""
        };

        progressText.text = text;
        progressText.gameObject.SetActive(text.Length > 0);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region SPOTLIGHT HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void ShowSpotlight(RectTransform target)
    {
        if (spotlightOverlay == null || spotlightHole == null || target == null) return;
        spotlightHole.gameObject.SetActive(true);
        spotlightOverlay.SetActive(true);
        spotlightHole.position = target.position;
        spotlightHole.sizeDelta = target.rect.size + spotlightPadding;
    }

    private void HideSpotlight()
    {
        spotlightOverlay?.SetActive(false);
        spotlightHole?.gameObject.SetActive(false);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region INTERACTABLE HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void RestoreAllShopUI()
    {
        if (quickCartButton != null) quickCartButton.interactable = true;
        chatSendButton?.gameObject.SetActive(true);

        var sr = shopScrollViewRect != null ? shopScrollViewRect.GetComponent<ScrollRect>() : null;
        if (sr != null) sr.enabled = true;

        if (allBlockableButtons != null)
        {
            foreach (var btn in allBlockableButtons)
                if (btn != null) btn.interactable = true;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region UI HIDE / RESET
    // ════════════════════════════════════════════════════════════════════════

    private void HideAllTutorialUI()
    {
        HideCompanionPanel();
        HideSpotlight();
        // Refactor: bỏ StopArrowBounce — _arrowBounce field + StartArrowBounce/ArrowLoop
        // không bao giờ được start từ bất kỳ đâu trong file, là dead code.
    }

    public void SkipTutorial()
    {
        StopAllCoroutines();
        RestoreAllShopUI();
        HideAllTutorialUI();

        // Đánh dấu toàn bộ section đã thấy
        foreach (var s in AllSections)
            MarkSeen(s);

        ActivateContextualTooltips();
        DebugLog("Tutorial bị SKIP — đánh dấu toàn bộ section đã thấy.");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region DEBUG
    // ════════════════════════════════════════════════════════════════════════

    private void UpdateDebugUI()
    {
        if (!showDebugPanel) return;
        debugPanelUI?.SetActive(showDebugPanel);

        if (debugStepText != null)
            debugStepText.text = $"Seen: {_seenSections.Count}/{AllSections.Length}";

        if (debugStatusText != null)
            debugStatusText.text = $"Enabled: {_isTutorialEnabled} | IntroDone: {_introFlowDone}";
    }

    private void DebugLog(string msg) => GameLog.Info($"[TutorialGamePlay] {msg}");

    // ── ContextMenu — Debug tools ────────────────────────────────────────

    [ContextMenu("Reset Tutorial (xoa tat ca PlayerPrefs)")]
    public void DEBUG_ResetTutorial()
    {
        foreach (var s in AllSections)
            PlayerPrefs.DeleteKey(KEY_SEEN_PREFIX + s.ToString());

        // Xóa cả legacy keys
        PlayerPrefs.DeleteKey(KEY_LEGACY_DONE);
        PlayerPrefs.DeleteKey(KEY_LEGACY_STEP);
        PlayerPrefs.Save();

        _seenSections.Clear();
        _introFlowDone = false;
        UpdateDebugUI();
        GameLog.Info("[TutorialGamePlay] Reset xong! Reload scene để chạy lại từ đầu.");
    }

    [ContextMenu("Mark ALL sections as Seen")]
    public void DEBUG_MarkAllSeen()
    {
        foreach (var s in AllSections)
            MarkSeen(s);
        _introFlowDone = true;
        HideAllTutorialUI();
        RestoreAllShopUI();
        GameLog.Info("[TutorialGamePlay] Tat ca section đã được đánh dấu seen.");
    }

    [ContextMenu("Simulate: OnPlayerEnterNPCRange")]
    public void DEBUG_SimulateEnterRange() => OnPlayerEnterNPCRange();

    [ContextMenu("Simulate: OnPlayerClickedNPC")]
    public void DEBUG_SimulateClickNPC() => OnPlayerClickedNPC();

    [ContextMenu("Simulate: OnHideDialogueAndOpenShop")]
    public void DEBUG_SimulateOpenShop() => OnHideDialogueAndOpenShop();

    [ContextMenu("Simulate: OnAIResponseReceived")]
    public void DEBUG_SimulateAIResponse() => OnAIResponseReceived();

    [ContextMenu("Simulate: OnPlayerTappedItem")]
    public void DEBUG_SimulateTapItem() => OnPlayerTappedItem();

    [ContextMenu("Simulate: OnAddToCartSuccess")]
    public void DEBUG_SimulateAddToCart() => OnAddToCartSuccess();

    [ContextMenu("Simulate: OnPlayerBackToShop")]
    public void DEBUG_SimulateBackToShop() => OnPlayerBackToShop();

    [ContextMenu("Simulate: OnCartOpened")]
    public void DEBUG_SimulateCartOpened() => OnCartOpened();

    [ContextMenu("Simulate: OnAddSelectedToCartSuccess")]
    public void DEBUG_SimulateAddSelected() => OnAddSelectedToCartSuccess();

    [ContextMenu("Simulate: OnCheckoutCompleted")]
    public void DEBUG_SimulateCheckout() => OnCheckoutCompleted();

    #endregion
}