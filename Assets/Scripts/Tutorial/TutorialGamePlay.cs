using System.Collections;
using System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Tutorial workflow cho scene MapTest2.
/// Chỉ chạy lần đầu tiên. Có thể bật/tắt và debug từ Inspector.
///
/// HOOKS CẦN THÊM VÀO CÁC FILE KHÁC:
/// VendorNPC.OnPlayerEnterRange()     → TutorialGamePlay.Instance?.OnPlayerEnterNPCRange()
/// VendorNPC.ProcessInteraction()     → TutorialGamePlay.Instance?.OnPlayerClickedNPC()
/// BaseNPC.HideDialogueAndOpenShop()  → TutorialGamePlay.Instance?.OnHideDialogueAndOpenShop()
/// MultiChatManager (send)            → TutorialGamePlay.Instance?.OnPlayerSentChat()
/// DifyChatService (response)         → TutorialGamePlay.Instance?.OnAIResponseReceived()
/// ShopController.OnProductLinkCallback() → TutorialGamePlay.Instance?.OnPlayerTappedItem()
/// ProductDetailUI (add to cart ok)   → TutorialGamePlay.Instance?.OnAddToCartSuccess()
/// ProductDetailUI (back button)      → TutorialGamePlay.Instance?.OnPlayerBackToShop()
/// ShopController (cartButton click)  → TutorialGamePlay.Instance?.OnCartOpened()
/// CartUI (addSelectedToCart ok)      → TutorialGamePlay.Instance?.OnAddSelectedToCartSuccess()
/// </summary>
public class TutorialGamePlay : MonoBehaviour
{
    public static TutorialGamePlay Instance { get; private set; }

    // ── PlayerPrefs Keys ─────────────────────────────────────────────────────
    private const string KEY_DONE = "MapTest2_TutorialDone";
    private const string KEY_STEP = "MapTest2_TutorialStep";
    private const string KEY_ENABLED = "MapTest2_TutorialEnabled";

    // ════════════════════════════════════════════════════════════════════════
    // ENUM
    // ════════════════════════════════════════════════════════════════════════
    public enum TutorialStep
    {
        None = -1,
        Step1_Intro = 0,   // Companion chào + arrow dẫn đường đến NPC
        Step1_WaitNPCClick = 1,   // Đã vào range → Spotlight NPC, chờ bấm
        Step2_Chat = 2,   // Shop mở → Spotlight chat input
        Step2_SelectItem = 3,   // Spotlight khu vực item
        Step2_InProductDetail = 4,   // ProductDetailUI mở → hướng dẫn chọn size
        Step2_WaitAddToCart = 5,   // Chờ player bấm "Thêm vào giỏ"
        Step2_WaitBackToShop = 6,   // Nhắc player bấm Back về shop
        Step2_WaitOpenBag = 7,   // Spotlight cartButton, chờ bấm
        Step2_OpenBag = 8,   // CartUI mở → spotlight từng phần
        Step3_Checkout = 9,   // Hướng dẫn checkout
        Step3_Reward = 10,  // Hiện badge thành công
        Step4_Contextual = 99,  // Tooltip tự khám phá
        Completed = 100
    }

    // ── Internal State ────────────────────────────────────────────────────
    private TutorialStep _step = TutorialStep.None;
    private bool _waitingForAI = false;
    private bool _continuePressed = false;
    private bool _isTutorialEnabled = true;
    private Coroutine _arrowBounce;
    private Coroutine _arrowDirectionRoutine;

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

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — SPOTLIGHT
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Spotlight Overlay ══════")]
    [SerializeField] private GameObject spotlightOverlay;
    [SerializeField] private RectTransform spotlightHole;
    [SerializeField] private Vector2 spotlightPadding = new Vector2(20f, 220f);
    [SerializeField] private float autoHideDelay = 2f;
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — STEP 1
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Step 1: Di chuyển đến NPC ══════")]
    [SerializeField] private Transform firstNPCWorldTransform;
    [SerializeField] private RectTransform firstNPCScreenRect;
    [SerializeField] private GameObject arrowNPC;
    [SerializeField] private float arrowUpdateInterval = 0.08f;
    [SerializeField] private Vector3 npcSpotlightOffset = new Vector3(0f, 1f, 0f); // ★ Chỉnh từ Inspector
    [SerializeField] private Vector2 npcSpotlightSize = new Vector2(120f, 120f);
    private Coroutine _spotlightTrackRoutine;
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — SHOP UI
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Shop UI References ══════")]
    [SerializeField] private RectTransform chatInputArea;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private RectTransform shopItemsContainerRect;
    [SerializeField] private RectTransform shopScrollViewRect;
    [SerializeField] private Button cartButton;
    [SerializeField] private GameObject cartPanel;
    [SerializeField] private GameObject productDetailPanel;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — CART UI
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ CartUI References ══════")]
    [SerializeField] private RectTransform cartItemListRect;
    [SerializeField] private Button addSelectedToCartBtn;
    [SerializeField] private RectTransform checkoutButtonRect;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — MESSAGES (chỉnh từ Inspector, không cần vào code)
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Messages ══════")]
    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step1_Intro =
        "👋 Xin chào! Mình là trợ lý của bạn!\nHãy di chuyển đến NPC phía trước\nđể bắt đầu mua sắm nhé! 🏃";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step1_Click =
        "✅ Bạn đã đến nơi rồi!\nHãy bấm vào NPC để mở Shop! 👆";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Chat =
        "💬 Đây là ô Chat với AI!\nBạn có thể hỏi về sản phẩm, size, hay phong cách.\nHãy thử gõ một câu hỏi và nhấn Gửi nhé!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Item =
        "🛍️ Tuyệt! Bây giờ hãy chọn một sản phẩm\nbạn thích từ khu vực hàng hóa phía dưới!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Size =
        "👟 Đây là chi tiết sản phẩm!\n• Chọn SIZE phù hợp với bạn\n• Sau đó nhấn \"Thêm vào giỏ\" để tiếp tục!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Back =
        "✅ Đã thêm vào giỏ thành công!\nHãy nhấn nút ← Trở Lại để quay về Shop.";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Bag =
        "🛒 Ngon lắm! Bây giờ hãy bấm vào nút\nTÚI HÀNG (🛒) để xem giỏ hàng của bạn!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step2_Cart =
        "📦 Đây là Giỏ Hàng của bạn!\n• Tick chọn sản phẩm muốn mua\n• Nhấn \"Thêm vào đơn\" để tiến hành!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Step3 =
        "🎉 Tuyệt vời! Điền thông tin giao hàng\nvà nhấn THANH TOÁN để hoàn tất đơn đầu tiên!";

    [TextArea(2, 4)]
    [SerializeField]
    private string msg_Reward =
        "🏆 ĐƠN HÀNG ĐẦU TIÊN HOÀN THÀNH! ✅\nBạn đã thành thạo cơ bản.\nTiếp tục khám phá thêm nhé!";

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — DEBUG PANEL (chỉ hiện trong Editor)
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Debug Panel ══════")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private GameObject debugPanelUI;           // Panel UI debug (optional, có thể null)
    [SerializeField] private TextMeshProUGUI debugStepText;     // Text hiển thị step hiện tại
    [SerializeField] private TextMeshProUGUI debugStatusText;   // Text hiển thị trạng thái

    // ════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Đọc setting bật/tắt từ PlayerPrefs (ưu tiên PlayerPrefs, fallback Inspector)
        _isTutorialEnabled = PlayerPrefs.GetInt(KEY_ENABLED, tutorialEnabled ? 1 : 0) == 1;

        companionContinueButton?.onClick.AddListener(()=> 
        { 
            OnContinuePressed();
            HideAllTutorialUI();
        });

        HideAllTutorialUI();
        UpdateDebugUI();

        if (!_isTutorialEnabled)
        {
            DebugLog("Tutorial đang TẮT — skip.");
            return;
        }

        CheckAndStartTutorial();
    }

    private void Update()
    {
        // Cập nhật debug text liên tục khi panel bật
#if UNITY_EDITOR
        if (showDebugPanel) UpdateDebugUI();
#endif
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region PUBLIC — BẬT / TẮT TUTORIAL
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Bật tutorial (lưu vào PlayerPrefs).</summary>
    public void EnableTutorial()
    {
        _isTutorialEnabled = true;
        PlayerPrefs.SetInt(KEY_ENABLED, 1);
        PlayerPrefs.Save();
        DebugLog("Tutorial đã BẬT.");
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
        _step = TutorialStep.None;
        DebugLog("Tutorial đã TẮT.");
        UpdateDebugUI();
    }

    /// <summary>Toggle bật/tắt — dùng cho UI Button.</summary>
    public void ToggleTutorial()
    {
        if (_isTutorialEnabled) DisableTutorial();
        else EnableTutorial();
    }

    /// <summary>Trả về trạng thái hiện tại.</summary>
    public bool IsTutorialEnabled() => _isTutorialEnabled;

    /// <summary>Trả về bước hiện tại.</summary>
    public TutorialStep CurrentStep() => _step;

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Entry Point
    // ════════════════════════════════════════════════════════════════════════

    public void CheckAndStartTutorial()
    {
        if (!_isTutorialEnabled) return;

        if (!forceRunOnStart && PlayerPrefs.GetInt(KEY_DONE, 0) == 1)
        {
            DebugLog("Tutorial đã hoàn thành trước đó — skip.");
            ActivateContextualTooltips();
            return;
        }

        int saved = forceRunOnStart ? 0 : PlayerPrefs.GetInt(KEY_STEP, 0);
        DebugLog($"Bắt đầu từ bước {(TutorialStep)saved}");
        SetStep((TutorialStep)saved);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STATE MACHINE
    // ════════════════════════════════════════════════════════════════════════

    private void SetStep(TutorialStep step)
    {
        if (!_isTutorialEnabled) return;

        _step = step;
        PlayerPrefs.SetInt(KEY_STEP, (int)step);
        PlayerPrefs.Save();

        DebugLog($"SetStep → {step}");
        UpdateDebugUI();


        SetSpotlightForStep(step);

        switch (step)
        {
            case TutorialStep.Step1_Intro:          StartCoroutine(RunStep1());                 break;
            case TutorialStep.Step1_WaitNPCClick:    /* đợi OnPlayerClickedNPC() */             break;
            case TutorialStep.Step2_Chat:           StartCoroutine(RunStep2_Chat());            break;
            case TutorialStep.Step2_SelectItem:     StartCoroutine(RunStep2_SelectItem());      break;
            case TutorialStep.Step2_InProductDetail:StartCoroutine(RunStep2_InProductDetail()); break;
            case TutorialStep.Step2_WaitAddToCart:   /* đợi OnAddToCartSuccess() */             break;
            case TutorialStep.Step2_WaitBackToShop: StartCoroutine(RunStep2_WaitBackToShop());  break;
            case TutorialStep.Step2_WaitOpenBag:    StartCoroutine(RunStep2_WaitOpenBag());     break;
            case TutorialStep.Step2_OpenBag:        StartCoroutine(RunStep2_OpenBag());         break;
            case TutorialStep.Step3_Checkout:       StartCoroutine(RunStep3_Checkout());        break;
            case TutorialStep.Step3_Reward:         StartCoroutine(RunStep3_Reward());          break;
            case TutorialStep.Step4_Contextual:     ActivateContextualTooltips();               break;
            case TutorialStep.Completed:            CompleteTutorial();                         break;
        }
    }

    private void SetSpotlightForStep(TutorialStep step)
    {
        switch (step)
        {
            // ── Các step KHÔNG dùng spotlight ────────────────────────────────
            case TutorialStep.Step1_WaitNPCClick:   // ← fix bug của bạn
            case TutorialStep.Step2_InProductDetail:
            case TutorialStep.Step2_WaitAddToCart:
            case TutorialStep.Step2_WaitBackToShop:
            case TutorialStep.Step3_Reward:
            case TutorialStep.Step4_Contextual:
            case TutorialStep.Completed:
            case TutorialStep.None:
                HideSpotlight();
                break;

            // ── Các step CÓ spotlight → để RunStepX() tự gọi ShowSpotlight ──
            // (không làm gì ở đây, Coroutine sẽ xử lý)
            default:
                break;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 1 — Chào → Di chuyển → Bấm NPC
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep1()
    {
        /*  HideAllTutorialUI();

          // Phase A: Companion chào, không cần bấm Continue
          yield return ShowCompanionMessage(msg_Step1_Intro, waitForContinue: false);
          yield return new WaitForSeconds(3f);
          HideCompanionPanel();

          // Phase B: Arrow xoay về phía NPC liên tục
          if (arrowNPC != null)
          {
              arrowNPC.SetActive(true);
              _arrowDirectionRoutine = StartCoroutine(UpdateArrowDirectionToNPC());
          }*/

        HideAllTutorialUI();

        // ── Phase A: Companion chào (3 giây, không bấm Continue) ──────────────
        yield return ShowCompanionMessage(msg_Step1_Intro, wait: false);
        yield return new WaitForSeconds(2f);
        HideCompanionPanel();

        // ── Phase B: Dark overlay + Spotlight bám NPC trong world space ───────
        // Arrow xoay về NPC + spotlight chiếu vào NPC ngay sau khi companion tắt
        if (firstNPCWorldTransform != null)
        {
            // Size spotlight: ước lượng theo NPC — chỉnh trong Inspector nếu cần
            ShowSpotlightOnWorld(firstNPCWorldTransform, npcSpotlightSize);
        }

        if (arrowNPC != null)
        {
            arrowNPC.SetActive(true);
            _arrowDirectionRoutine = StartCoroutine(UpdateArrowDirectionToNPC());
        }

        // Đợi player di chuyển → OnPlayerEnterNPCRange() callback
    }

    private IEnumerator UpdateArrowDirectionToNPC()
    {
        if (arrowNPC == null || firstNPCWorldTransform == null) yield break;
        Camera cam = Camera.main;
        RectTransform arrowRect = arrowNPC.GetComponent<RectTransform>();

        while (_step == TutorialStep.Step1_Intro || _step == TutorialStep.Step1_WaitNPCClick)
        {
            if (cam != null && arrowRect != null)
            {
                Vector3 npcScreen = cam.WorldToScreenPoint(firstNPCWorldTransform.position);
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 dir = ((Vector2)npcScreen - center).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                arrowRect.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
            yield return new WaitForSeconds(arrowUpdateInterval);
        }
    }

    /// <summary>Hook → VendorNPC.OnPlayerEnterRange()</summary>
    public void OnPlayerEnterNPCRange()
    {
        if (_step != TutorialStep.Step1_Intro) return;
        StartCoroutine(ShowCompanionThenWaitClick());
    }

    private IEnumerator ShowCompanionThenWaitClick()
    {
        yield return ShowCompanionMessage(msg_Step1_Click, wait: false);
/*        _step = TutorialStep.Step1_WaitNPCClick;
*/        yield return new WaitForSeconds(1.8f);
        HideAllTutorialUI();
        SetStep(TutorialStep.Step1_WaitNPCClick);
        HideSpotlight();
        PlayerPrefs.SetInt(KEY_STEP, (int)_step);
        PlayerPrefs.Save();
        UpdateDebugUI();

    }

    /// <summary>Hook → VendorNPC.ProcessInteraction()</summary>
    public void OnPlayerClickedNPC()
    {
        if (_step != TutorialStep.Step1_WaitNPCClick) return;
        StopArrowBounce();
        HideSpotlight();
        HideAllTutorialUI();
        arrowNPC?.SetActive(false);
        if (_arrowDirectionRoutine != null)
        {
            StopCoroutine(_arrowDirectionRoutine);
            _arrowDirectionRoutine = null;
        }
        DebugLog("Player bấm NPC — chờ HideDialogueAndOpenShop...");
    }

    /// <summary>Hook → BaseNPC.HideDialogueAndOpenShop()</summary>
    public void OnHideDialogueAndOpenShop()
    {
        if (_step != TutorialStep.Step1_WaitNPCClick) return;
        SetStep(TutorialStep.Step2_Chat);
        ShowSpotlight(chatInputArea);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2A — Chat với AI
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_Chat()
    {
        yield return ShowCompanionMessage(msg_Step2_Chat, wait: true);
        SetShopUIInteractableExcept(chatInputArea, chatSendButton);
        
    }

    /// <summary>Hook → MultiChatManager (khi player nhấn Send)</summary>
    public void OnPlayerSentChat()
    {
        if (_step != TutorialStep.Step2_Chat) return;
        _waitingForAI = true;
        DebugLog("Đang đợi AI response...");
    }

    /// <summary>Hook → DifyChatService (khi AI trả về kết quả)</summary>
    public void OnAIResponseReceived()
    {
        if (_step != TutorialStep.Step2_Chat || !_waitingForAI) return;
        _waitingForAI = false;
        HideSpotlight();
        RestoreAllShopUI();
        SetStep(TutorialStep.Step2_SelectItem);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2B — Chọn Item
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_SelectItem()
    {
        HideAllTutorialUI();
        ShowSpotlight(shopItemsContainerRect);
        yield return ShowCompanionMessage(msg_Step2_Item, wait: true);
        yield return new WaitForSeconds(1f);
        chatSendButton?.gameObject.SetActive(false);
        if (cartButton != null) cartButton.interactable = false;
    }

    /// <summary>Hook → ShopController.OnProductLinkCallback()</summary>
    public void OnPlayerTappedItem()
    {
        if (_step != TutorialStep.Step2_SelectItem) return;
        HideSpotlight();
        RestoreAllShopUI();
        SetStep(TutorialStep.Step2_InProductDetail);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2C — ProductDetailUI
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_InProductDetail()
    {
        HideAllTutorialUI();
        yield return ShowCompanionMessage(msg_Step2_Size, wait: true);
        // Chuyển state thủ công vì không gọi SetStep (tránh loop)
        _step = TutorialStep.Step2_WaitAddToCart;
        PlayerPrefs.SetInt(KEY_STEP, (int)_step);
        PlayerPrefs.Save();
        UpdateDebugUI();
    }

    /// <summary>Hook → ProductDetailUI (add to cart thành công)</summary>
    public void OnAddToCartSuccess()
    {
        if (_step != TutorialStep.Step2_WaitAddToCart) return;
        HideSpotlight();
        SetStep(TutorialStep.Step2_WaitBackToShop);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2D — Back về Shop
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_WaitBackToShop()
    {
        yield return ShowCompanionMessage(msg_Step2_Back, wait: false);
        yield return new WaitForSeconds(2f);
        HideCompanionPanel();
        // Companion hiện, chờ player tự bấm Back
    }

    /// <summary>Hook → ProductDetailUI (back button)</summary>
    public void OnPlayerBackToShop()
    {
        if (_step != TutorialStep.Step2_WaitBackToShop) return;
        HideCompanionPanel();
        SetStep(TutorialStep.Step2_WaitOpenBag);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2E — Mở Bag
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_WaitOpenBag()
    {
        //HideAllTutorialUI();
        yield return ShowCompanionMessage(msg_Step2_Bag, wait: true);
        yield return new WaitForSeconds(2f);
        HideCompanionPanel();   
        if (cartButton != null)
            ShowSpotlight(cartButton.GetComponent<RectTransform>());
    }

    /// <summary>Hook → ShopController (cartButton onClick → cartPanel active)</summary>
    public void OnCartOpened()
    {
        if (_step != TutorialStep.Step2_WaitOpenBag) return;
        HideSpotlight();
        SetStep(TutorialStep.Step2_OpenBag);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2F — CartUI
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_OpenBag()
    {
        yield return null; // Đợi CartUI render

        yield return ShowCompanionMessage(msg_Step2_Cart, wait: true);

        // Spotlight 1: danh sách item
        if (cartItemListRect != null)
        {
            ShowSpotlight(cartItemListRect);
            yield return new WaitForSeconds(2f);
            HideSpotlight();
        }

        yield return new WaitForSeconds(0.3f);

        // Spotlight 2: nút thêm đơn
        if (addSelectedToCartBtn != null)
        {
            ShowSpotlight(addSelectedToCartBtn.GetComponent<RectTransform>());
            SetCartUIInteractableExcept(addSelectedToCartBtn);
        }
        // Đợi OnAddSelectedToCartSuccess()
    }

    /// <summary>Hook → CartUI.addSelectedToCartButton.onClick</summary>
    public void OnAddSelectedToCartSuccess()
    {
        if (_step != TutorialStep.Step2_OpenBag) return;
        HideSpotlight();
        RestoreAllShopUI();
        SetStep(TutorialStep.Step3_Checkout);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 3 — Checkout & Reward
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep3_Checkout()
    {
        yield return ShowCompanionMessage(msg_Step3, wait: false);
        yield return new WaitForSeconds(2f);
        HideAllTutorialUI();
        // Đợi OnCheckoutCompleted()
    }

    /// <summary>Hook → CheckoutController (thanh toán xong)</summary>
    public void OnCheckoutCompleted()
    {
        if (_step != TutorialStep.Step3_Checkout) return;
        HideCompanionPanel();
        SetStep(TutorialStep.Step3_Reward);
    }

    private IEnumerator RunStep3_Reward()
    {
        yield return new WaitForSeconds(0.3f);
        yield return ShowCompanionMessage(msg_Reward, wait: true);

        SetStep(TutorialStep.Step4_Contextual);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 4 — Contextual Tooltips
    // ════════════════════════════════════════════════════════════════════════

    private void ActivateContextualTooltips()
    {
        CompleteTutorial();
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

        Vector3 originalScale = companionImage.transform.localScale;

        companionImage.transform.localScale = Vector3.zero;
        companionImage.transform.DOScale(originalScale, 0.5f).SetEase(Ease.OutBack);


        if (companionImage != null && tutorialCompanionSprite != null)
            companionImage.sprite = tutorialCompanionSprite;

        if (companionChatText != null)
        {
            companionChatText.text = msg;           // set full text 1 lần duy nhất
            companionChatText.maxVisibleCharacters = 0;
            for (int i = 0; i <= msg.Length; i++)
            {
                companionChatText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(typewriterSpeed);
            }
        }

        if (wait)
        {
            companionContinueButton.gameObject.SetActive(true);
            _continuePressed = true;
            yield return new WaitUntil(() => _continuePressed);
            HideCompanionPanel();
            HideAllTutorialUI();
        }
        else
        {
            companionContinueButton.gameObject.SetActive(false);
            yield return new WaitForSeconds(autoHideDelay);
        }
        /*        if (wait)
                {
                    _continuePressed = false;                              // ← fix bug: reset về false TRƯỚC
                    companionContinueButton?.gameObject.SetActive(true);
                    yield return new WaitUntil(() => _continuePressed);   // ← giờ mới chờ thật sự
                    HideCompanionPanel();

                }
                else
                {
                    companionContinueButton?.gameObject.SetActive(false);
                  //  yield return new WaitForSeconds(autoHideDelay);       // ← đợi 1s rồi tự tắt

                }*/
    }

    private void OnContinuePressed() => _continuePressed = true;

    private void HideCompanionPanel()
    {
        companionPanel?.SetActive(false);
        companionContinueButton?.gameObject.SetActive(false);
        _continuePressed = false;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region SPOTLIGHT HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void ShowSpotlight(RectTransform target)
    {
        if (spotlightOverlay == null || spotlightHole == null || target == null) return;
        spotlightHole.gameObject.SetActive(true);
        StopSpotlightTracking(); // Dừng tracking cũ nếu có
        spotlightOverlay.SetActive(true);
        spotlightHole.position = target.position;
        spotlightHole.sizeDelta = target.rect.size + spotlightPadding;
    }

    private void ShowSpotlightOnWorld(Transform worldTarget, Vector2 size)
    {
        if (spotlightOverlay == null || spotlightHole == null || worldTarget == null) return;
        spotlightHole.gameObject.SetActive(true);
        StopSpotlightTracking();
        spotlightOverlay.SetActive(true);
        spotlightHole.sizeDelta = size + spotlightPadding;
        _spotlightTrackRoutine = StartCoroutine(TrackSpotlightToWorldPos(worldTarget));
    }
    [SerializeField] private Camera _camera;
    private IEnumerator TrackSpotlightToWorldPos(Transform worldTarget)
    {
        Camera cam = _camera;
        Canvas canvas = spotlightOverlay.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;

        while (worldTarget != null)
        {
            if (cam != null)
            {
                Vector3 worldPos = worldTarget.position + npcSpotlightOffset;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                // Nếu NPC nằm sau camera → ẩn spotlight
                if (screenPos.z < 0f)
                {
                    spotlightHole.anchoredPosition = new Vector2(-9999f, -9999f);
                }
                else
                {
                    // Convert Screen → Canvas local position
                    if (canvasRect != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect, screenPos, canvas.worldCamera, out Vector2 localPoint);
                        spotlightHole.localPosition = localPoint;
                    }
                    else
                    {
                        // ScreenSpaceOverlay: screenPos = anchoredPosition trực tiếp
                        spotlightHole.position = screenPos;
                    }
                }
            }
            yield return null; // Update mỗi frame
        }
    }
    private void StopSpotlightTracking()
    {
        if (_spotlightTrackRoutine != null)
        {
            StopCoroutine(_spotlightTrackRoutine);
            _spotlightTrackRoutine = null;
        }
    }
    private void HideSpotlight()
    {
        StopSpotlightTracking(); // NEW: dừng tracking khi ẩn
        spotlightOverlay?.SetActive(false);
        spotlightHole?.gameObject.SetActive(false); 
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region INTERACTABLE HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void SetShopUIInteractableExcept(params Object[] _)
    {
        if (cartButton != null) cartButton.interactable = false;
        var sr = shopScrollViewRect?.GetComponent<ScrollRect>();
        if (sr != null) sr.enabled = false;
        productDetailPanel?.SetActive(false);
    }

    private void RestoreAllShopUI()
    {
        if (cartButton != null) cartButton.interactable = true;
        chatSendButton?.gameObject.SetActive(true);
        var sr = shopScrollViewRect?.GetComponent<ScrollRect>();
        if (sr != null) sr.enabled = true;
    }

    private void SetCartUIInteractableExcept(Button allowed)
    {
        // Mở rộng tùy CartUI — hiện tại chỉ giữ addSelectedToCartBtn
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region ARROW BOUNCE
    // ════════════════════════════════════════════════════════════════════════

    private void StartArrowBounce(RectTransform arrow, Vector3 direction)
    {
        StopArrowBounce();
        if (arrow != null) _arrowBounce = StartCoroutine(ArrowLoop(arrow, direction));
    }

    private void StopArrowBounce()
    {
        if (_arrowBounce != null) { StopCoroutine(_arrowBounce); _arrowBounce = null; }
    }

    private IEnumerator ArrowLoop(RectTransform arrow, Vector3 dir)
    {
        Vector3 origin = arrow.localPosition;
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            arrow.localPosition = origin + dir * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 0.5f;
            yield return null;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region COMPLETE / SKIP / RESET
    // ════════════════════════════════════════════════════════════════════════

    private void HideAllTutorialUI()
    {
        HideCompanionPanel();
        HideSpotlight();
        StopArrowBounce();
        arrowNPC?.SetActive(false);
    }

    private void CompleteTutorial()
    {
        PlayerPrefs.SetInt(KEY_DONE, 1);
        PlayerPrefs.SetInt(KEY_STEP, (int)TutorialStep.Completed);
        PlayerPrefs.Save();
        HideAllTutorialUI();
        _step = TutorialStep.Completed;
        DebugLog("Tutorial HOÀN THÀNH!");
        UpdateDebugUI();
    }

    public void SkipTutorial()
    {
        StopAllCoroutines();
        RestoreAllShopUI();
        CompleteTutorial();
        ActivateContextualTooltips();
        DebugLog("Tutorial bị SKIP.");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region DEBUG — ContextMenu + Runtime Panel
    // ════════════════════════════════════════════════════════════════════════

    /// Hiển thị trạng thái trên debugPanel UI nếu có assign
    private void UpdateDebugUI()
    {
        if (!showDebugPanel) return;
        debugPanelUI?.SetActive(showDebugPanel);

        if (debugStepText != null)
            debugStepText.text = $"Step: {_step} ({(int)_step})";

        if (debugStatusText != null)
            debugStatusText.text = $"Enabled: {_isTutorialEnabled} | Done: {PlayerPrefs.GetInt(KEY_DONE, 0) == 1} | WaitAI: {_waitingForAI}";
    }

    private void DebugLog(string msg)
        => Debug.Log($"[TutorialGamePlay] {msg}");

    // ── ContextMenu — Right-click component trong Inspector ────────────────

    [ContextMenu("🔁 Reset Tutorial (xóa PlayerPrefs)")]
    public void DEBUG_ResetTutorial()
    {
        PlayerPrefs.DeleteKey(KEY_DONE);
        PlayerPrefs.DeleteKey(KEY_STEP);
        PlayerPrefs.Save();
        _step = TutorialStep.None;
        UpdateDebugUI();
        Debug.Log("[Tutorial] ✅ Reset xong! Reload scene để chạy lại.");
    }

    [ContextMenu("⏭️ Skip đến Step2_Chat")]
    public void DEBUG_JumpToStep2Chat()
    {
        StopAllCoroutines(); HideAllTutorialUI(); RestoreAllShopUI();
        SetStep(TutorialStep.Step2_Chat);
    }

    [ContextMenu("⏭️ Skip đến Step2_SelectItem")]
    public void DEBUG_JumpToSelectItem()
    {
        StopAllCoroutines(); HideAllTutorialUI(); RestoreAllShopUI();
        SetStep(TutorialStep.Step2_SelectItem);
    }

    [ContextMenu("⏭️ Skip đến Step2_OpenBag")]
    public void DEBUG_JumpToOpenBag()
    {
        StopAllCoroutines(); HideAllTutorialUI(); RestoreAllShopUI();
        SetStep(TutorialStep.Step2_OpenBag);
    }

    [ContextMenu("⏭️ Skip đến Step3_Checkout")]
    public void DEBUG_JumpToCheckout()
    {
        StopAllCoroutines(); HideAllTutorialUI(); RestoreAllShopUI();
        SetStep(TutorialStep.Step3_Checkout);
    }

    [ContextMenu("⏭️ Skip đến Reward")]
    public void DEBUG_JumpToReward()
    {
        StopAllCoroutines(); HideAllTutorialUI(); RestoreAllShopUI();
        SetStep(TutorialStep.Step3_Reward);
    }

    [ContextMenu("✅ Force Complete Tutorial")]
    public void DEBUG_ForceComplete()
    {
        StopAllCoroutines(); RestoreAllShopUI();
        CompleteTutorial();
    }

    [ContextMenu("🔔 Simulate: OnPlayerEnterNPCRange")]
    public void DEBUG_SimulateEnterRange() => OnPlayerEnterNPCRange();

    [ContextMenu("🔔 Simulate: OnPlayerClickedNPC")]
    public void DEBUG_SimulateClickNPC() => OnPlayerClickedNPC();

    [ContextMenu("🔔 Simulate: OnHideDialogueAndOpenShop")]
    public void DEBUG_SimulateOpenShop() => OnHideDialogueAndOpenShop();

    [ContextMenu("🔔 Simulate: OnPlayerSentChat")]
    public void DEBUG_SimulateSentChat() => OnPlayerSentChat();

    [ContextMenu("🔔 Simulate: OnAIResponseReceived")]
    public void DEBUG_SimulateAIResponse() => OnAIResponseReceived();

    [ContextMenu("🔔 Simulate: OnPlayerTappedItem")]
    public void DEBUG_SimulateTapItem() => OnPlayerTappedItem();

    [ContextMenu("🔔 Simulate: OnAddToCartSuccess")]
    public void DEBUG_SimulateAddToCart() => OnAddToCartSuccess();

    [ContextMenu("🔔 Simulate: OnPlayerBackToShop")]
    public void DEBUG_SimulateBackToShop() => OnPlayerBackToShop();

    [ContextMenu("🔔 Simulate: OnCartOpened")]
    public void DEBUG_SimulateCartOpened() => OnCartOpened();

    [ContextMenu("🔔 Simulate: OnAddSelectedToCartSuccess")]
    public void DEBUG_SimulateAddSelected() => OnAddSelectedToCartSuccess();

    [ContextMenu("🔔 Simulate: OnCheckoutCompleted")]
    public void DEBUG_SimulateCheckout() => OnCheckoutCompleted();

    #endregion
}