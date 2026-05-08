/*using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Workflow: Load scene MapTest2 → check PlayerPrefs
/// → Nếu lần đầu chơi → chạy tutorial 4 bước
public class TutorialGamePlay : MonoBehaviour
{
    public static TutorialGamePlay Instance { get; private set; }

    // ─── PlayerPrefs Keys ───────────────────────────────────────────────────
    private const string TUTORIAL_DONE_KEY = "MapTest2_TutorialDone";
    private const string TUTORIAL_STEP_KEY = "MapTest2_TutorialStep";

    // ─── Tutorial Steps ─────────────────────────────────────────────────────
    public enum TutorialStep
    {
        None = -1,
        Step0_CompanionIntro = -2,  // Companion chat trước khi vào tutorial
        Step1_MoveToNPC = 0,        // Spotlight + arrow → bấm NPC
        Step2_Chat = 1,             // AI chat → dạy chat input
        Step2_SelectItem = 2,       // Arrow → chọn item trên quầy
        Step2_OpenBag = 3,          // Arrow → bấm Bag button
        Step3_Checkout = 4,         // Điền thông tin → thanh toán
        Step3_Reward = 5,           // Hiện badge reward
        Step4_Contextual = 6,       // Tooltip tự khám phá
        Completed = 99
    }

    private TutorialStep currentStep = TutorialStep.None;

    // ─── World References (assign trong Inspector) ────────────────────────
    [Header("=== WORLD REFERENCES ===")]
    [SerializeField] private Transform firstNPC;
    [SerializeField] private Camera mainCamera;

    // ─── UI Overlay ───────────────────────────────────────────────────────
    [Header("=== UI OVERLAY ===")]
    [SerializeField] private Canvas tutorialCanvas;
    [SerializeField] private GameObject darkOverlayPanel;
    [SerializeField] private RectTransform arrowUI;
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private Button skipTutorialButton;

    // ─── Step 0: Companion Chat Panel ────────────────────────────────────
    [Header("=== STEP 0: Companion Intro ===")]
    [SerializeField] private GameObject companionChatPanel;
    // Panel chứa toàn bộ UI chat companion (ẩn/hiện cả cụm)
    // Hierarchy gợi ý:
    //   CompanionChatPanel
    //   ├── CompanionImage      ← Image 2D của companion
    //   ├── ChatBubble          ← Panel bubble
    //   │   └── ChatText (TMP)  ← Text hiển thị lời thoại
    //   └── ContinueButton      ← Nút "Tiếp tục" / tap to continue

    [SerializeField] private Image companionImage;
    // ↑ Assign image 2D companion tại đây hoặc load từ PlayerPrefs key

    [SerializeField] private TextMeshProUGUI companionChatText;
    // ↑ TMP text trong bubble chat

    [SerializeField] private Button companionContinueButton;
    // ↑ Nút bấm để qua lời thoại tiếp theo / kết thúc intro

    [SerializeField] private Sprite[] companionSprites;
    // ↑ Mảng sprite cho từng companion. Index theo CompanionID đã chọn.
    // Nếu dùng Addressables/Resources thì thay bằng LoadAsync

    [TextArea(2, 4)]
    [SerializeField]
    private string companionIntroLine1 = "Chào mừng bạn đến với Mantini! Mình sẽ hướng dẫn bạn nhé 😊";
    [TextArea(2, 4)]
    [SerializeField]
    private string companionIntroLine2 = "Hãy bắt đầu bằng cách đến gặp NPC đầu tiên — họ sẽ mở Shop cho bạn!";

    // ─── Step 1 ───────────────────────────────────────────────────────────
    [Header("=== STEP 1: Spotlight NPC ===")]
    [SerializeField] private Image spotlightRing;

    // ─── Step 2 (Shop UI) ─────────────────────────────────────────────────
    [Header("=== STEP 2: Shop UI ===")]
    [SerializeField] private RectTransform chatInputArea;
    [SerializeField] private RectTransform itemGridArea;
    [SerializeField] private RectTransform bagButtonRect;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private Button[] itemButtons;
    [SerializeField] private Button bagOpenButton;
    [SerializeField] private TextMeshProUGUI npcAIChatText;
    // ↑ Text trong khung chat NPC-AI (Step 2 trở đi)
    // CompanionChatPanel cũng được bật khi TypeAIMessage chạy

    // ─── Step 3 (Reward) ─────────────────────────────────────────────────
    [Header("=== STEP 3: Reward ===")]
    [SerializeField] private GameObject rewardPopup;
    [SerializeField] private TextMeshProUGUI rewardTitleText;
    [SerializeField] private TextMeshProUGUI rewardDescText;
    [SerializeField] private GameObject badgeObject;
    [SerializeField] private Button rewardCloseButton;

    // ─── Step 4 (Contextual Tooltip) ─────────────────────────────────────
    [Header("=== STEP 4: Contextual Tooltips ===")]
    [SerializeField] private ContextualTooltipTrigger[] contextualTriggers;

    // ─── NPC-AI Script (Step 2+) ──────────────────────────────────────────
    [Header("=== NPC-AI Messages ===")]
    [TextArea(2, 4)]
    [SerializeField]
    private string aiGreetMessage = "Xin chào! Mình là trợ lý tư vấn 👋\nBạn hãy gõ câu hỏi vào ô chat bên dưới nhé!";
    [TextArea(2, 4)]
    [SerializeField]
    private string aiSelectMessage = "Tuyệt! Bây giờ hãy chọn một sản phẩm\nbạn thích từ quầy hàng phía trên! 👆";
    [TextArea(2, 4)]
    [SerializeField]
    private string aiBagMessage = "Đã chọn xong rồi! Bấm vào 🎒 TÚI\nđể tiến hành thanh toán nhé!";
    [TextArea(2, 4)]
    [SerializeField]
    private string aiCheckoutMsg = "Điền thông tin và nhấn THANH TOÁN\nđể hoàn thành đơn hàng đầu tiên! 🎉";

    private Coroutine arrowBounceCoroutine;

    // ─── Companion intro state ─────────────────────────────────────────────
    private int companionLineIndex = 0;   // Dòng thoại đang hiển thị
    private bool isTyping = false; // Đang gõ typewriter?
    private string[] companionLines;             // Cache mảng lời thoại

    // ─── DEBUG SETTINGS ──────────────────────────────────────────────────────
    [Header("=== DEBUG SETTINGS ===")]
    [SerializeField] private bool forceShowTutorial = false;
    [SerializeField] private bool skipAllTutorial = false;
    [SerializeField] private TutorialStep debugStartAtStep = TutorialStep.Step0_CompanionIntro;

    [Header("=== SPOTLIGHT OFFSET ===")]
    [SerializeField] private Vector3 spotlightWorldOffset = new Vector3(0f, 1.5f, 0f);

#if UNITY_EDITOR
    [SerializeField] private bool showDebugPanel = true;
#endif

    // ─────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (skipTutorialButton != null)
            skipTutorialButton.onClick.AddListener(SkipTutorial);
        if (rewardCloseButton != null)
            rewardCloseButton.onClick.AddListener(OnRewardClosed);

        // ContinueButton dùng chung cho cả intro companion lẫn các bước sau
        if (companionContinueButton != null)
            companionContinueButton.onClick.AddListener(OnCompanionContinuePressed);

        CheckAndStartTutorial();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Entry Point

    public void CheckAndStartTutorial()
    {
#if UNITY_EDITOR
        if (skipAllTutorial)
        {
            Debug.Log("[Tutorial] DEBUG: Skip toàn bộ tutorial");
            ActivateContextualTooltips();
            return;
        }
        if (forceShowTutorial)
        {
            Debug.Log($"[Tutorial] DEBUG: Force chạy từ bước {debugStartAtStep}");
            HideAllTutorialUI();
            SetStep(debugStartAtStep);
            return;
        }
#endif
        bool isDone = PlayerPrefs.GetInt(TUTORIAL_DONE_KEY, 0) == 1;
        if (isDone) { ActivateContextualTooltips(); return; }

        int savedStep = PlayerPrefs.GetInt(TUTORIAL_STEP_KEY, 0);
        // Step0 dùng giá trị -2, nếu chưa save thì luôn bắt đầu từ Step0
        bool hasAnySave = PlayerPrefs.HasKey(TUTORIAL_STEP_KEY);
        HideAllTutorialUI();
        if (!hasAnySave)
            SetStep(TutorialStep.Step0_CompanionIntro);
        else
            SetStep((TutorialStep)savedStep);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region State Machine

    private void SetStep(TutorialStep step)
    {
        currentStep = step;
        PlayerPrefs.SetInt(TUTORIAL_STEP_KEY, (int)step);
        PlayerPrefs.Save();

        switch (step)
        {
            case TutorialStep.Step0_CompanionIntro: StartCoroutine(RunStep0_CompanionIntro()); break;
            case TutorialStep.Step1_MoveToNPC: StartCoroutine(RunStep1()); break;
            case TutorialStep.Step2_Chat: StartCoroutine(RunStep2_Chat()); break;
            case TutorialStep.Step2_SelectItem: StartCoroutine(RunStep2_SelectItem()); break;
            case TutorialStep.Step2_OpenBag: StartCoroutine(RunStep2_OpenBag()); break;
            case TutorialStep.Step3_Checkout: StartCoroutine(RunStep3_Checkout()); break;
            case TutorialStep.Step3_Reward: StartCoroutine(RunStep3_Reward()); break;
            case TutorialStep.Step4_Contextual: ActivateContextualTooltips(); break;
            case TutorialStep.Completed: CompleteTutorial(); break;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region STEP 0 – Companion Intro Chat

    private IEnumerator RunStep0_CompanionIntro()
    {
        // Lấy sprite companion theo ID player đã chọn
        LoadCompanionSprite();

        // Cache mảng lời thoại
        companionLines = new string[] { companionIntroLine1, companionIntroLine2 };
        companionLineIndex = 0;

        // Hiện panel
        ShowCompanionPanel(true);
        SetOverlay(true);

        // Gõ dòng đầu tiên
        yield return StartCoroutine(TypeCompanionLine(companionLines[0]));

        // Đợi player bấm Continue để qua dòng tiếp / kết thúc
        // (xử lý trong OnCompanionContinuePressed)
    }

    /// Bấm Continue / tap màn hình trong Step0
    private void OnCompanionContinuePressed()
    {
        if (currentStep != TutorialStep.Step0_CompanionIntro) return;
        if (isTyping)
        {
            // Nếu đang gõ → skip ngay toàn bộ text
            StopAllCoroutines();
            isTyping = false;
            if (companionChatText != null && companionLineIndex < companionLines.Length)
                companionChatText.text = companionLines[companionLineIndex];
            return;
        }

        companionLineIndex++;
        if (companionLineIndex < companionLines.Length)
        {
            // Còn dòng tiếp → gõ tiếp
            StartCoroutine(TypeCompanionLine(companionLines[companionLineIndex]));
        }
        else
        {
            // Hết lời thoại → ẩn companion panel, sang Step1
            ShowCompanionPanel(false);
            SetOverlay(false);
            SetStep(TutorialStep.Step1_MoveToNPC);
        }
    }

    /// Gõ typewriter cho companion intro
    private IEnumerator TypeCompanionLine(string line)
    {
        if (companionChatText == null) yield break;
        isTyping = true;
        companionChatText.text = "";
        foreach (char c in line)
        {
            companionChatText.text += c;
            yield return new WaitForSeconds(0.03f);
        }
        isTyping = false;
    }

    /// Load sprite companion theo ID đã lưu
    private void LoadCompanionSprite()
    {
        if (companionImage == null) return;
        int companionID = PlayerPrefs.GetInt("SelectedCompanionID", 0);
        if (companionSprites != null && companionID < companionSprites.Length && companionSprites[companionID] != null)
            companionImage.sprite = companionSprites[companionID];
        // Nếu dùng Addressables:
        // Addressables.LoadAssetAsync<Sprite>($"Companion_{companionID}").Completed += h => companionImage.sprite = h.Result;
    }

    private void ShowCompanionPanel(bool show)
    {
        if (companionChatPanel != null) companionChatPanel.SetActive(show);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region STEP 1 – Spotlight + Arrow → NPC

    private IEnumerator RunStep1()
    {
        SetOverlay(true);
        if (spotlightRing != null && firstNPC != null)
        {
            spotlightRing.gameObject.SetActive(true);
            spotlightRing.transform.position = firstNPC.position;
            Debug.Log("[RunStep1] Đã đặt spotlightRing tại vị trí NPC: " + firstNPC.position);
        }
        ShowTooltip("👆 Di chuyển đến NPC và bấm vào để mở Shop!");
        ShowArrow(null);
        yield return null;
    }

    private void Update()
    {
        if(TutorialStep.Step1_MoveToNPC ==currentStep)
        {
            if (spotlightRing == null || firstNPC == null || mainCamera == null) return;

            Vector3 targetWorldPos = firstNPC.position + spotlightWorldOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(targetWorldPos);

            bool isVisible = screenPos.z > 0
                && screenPos.x >= 0 && screenPos.x <= Screen.width
                && screenPos.y >= 0 && screenPos.y <= Screen.height;

            spotlightRing.gameObject.SetActive(isVisible);
            if (!isVisible) return;

            Canvas canvas = spotlightRing.canvas;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvas.worldCamera, out localPoint);
            spotlightRing.rectTransform.localPosition = localPoint;
        }
    }

    public void OnPlayerClickedNPC()
    {
        if (currentStep != TutorialStep.Step1_MoveToNPC) return;
        SetOverlay(false);
        HideTooltip();
        StopArrowBounce();
        if (spotlightRing != null) spotlightRing.gameObject.SetActive(false);
        SetStep(TutorialStep.Step2_Chat);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region STEP 2 – NPC-AI Dẫn Dắt trong Shop

    private IEnumerator RunStep2_Chat()
    {
        SetShopInteractable(false);
        if (chatSendButton != null) chatSendButton.interactable = true;

        yield return StartCoroutine(TypeAIMessage(aiGreetMessage));
        ShowArrow(chatInputArea);
        ShowTooltip("Gõ câu hỏi vào đây rồi nhấn Gửi 💬");
    }

    public void OnPlayerSentChat()
    {
        if (currentStep != TutorialStep.Step2_Chat) return;
        HideTooltip(); HideArrow();
        SetStep(TutorialStep.Step2_SelectItem);
    }

    private IEnumerator RunStep2_SelectItem()
    {
        SetShopInteractable(false);
        if (itemButtons != null)
            foreach (var btn in itemButtons)
                if (btn != null) btn.interactable = true;

        yield return StartCoroutine(TypeAIMessage(aiSelectMessage));
        ShowArrow(itemGridArea);
        ShowTooltip("Chọn sản phẩm bạn muốn 👆");
    }

    public void OnPlayerSelectedItem()
    {
        if (currentStep != TutorialStep.Step2_SelectItem) return;
        HideTooltip(); HideArrow();
        SetStep(TutorialStep.Step2_OpenBag);
    }

    private IEnumerator RunStep2_OpenBag()
    {
        SetShopInteractable(false);
        if (bagOpenButton != null) bagOpenButton.interactable = true;

        yield return StartCoroutine(TypeAIMessage(aiBagMessage));
        ShowArrow(bagButtonRect);
        ShowTooltip("Bấm vào Túi 🎒 để thanh toán!");
    }

    public void OnPlayerOpenedBag()
    {
        if (currentStep != TutorialStep.Step2_OpenBag) return;
        HideTooltip(); HideArrow();
        SetShopInteractable(true);
        SetStep(TutorialStep.Step3_Checkout);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region STEP 3 – Checkout & Reward

    private IEnumerator RunStep3_Checkout()
    {
        yield return StartCoroutine(TypeAIMessage(aiCheckoutMsg));
        ShowTooltip("Điền thông tin và nhấn THANH TOÁN 🛒");
    }

    public void OnPlayerCompletedCheckout()
    {
        if (currentStep != TutorialStep.Step3_Checkout) return;
        HideTooltip();
        SetStep(TutorialStep.Step3_Reward);
    }

    private IEnumerator RunStep3_Reward()
    {
        // Ẩn companion panel khi hiện reward
        ShowCompanionPanel(false);
        yield return new WaitForSeconds(0.5f);
        if (badgeObject != null)
        {
            badgeObject.SetActive(true);
            StartCoroutine(BounceScale(badgeObject.transform));
        }
        if (rewardPopup != null)
        {
            rewardPopup.SetActive(true);
            if (rewardTitleText != null) rewardTitleText.text = "🎉 Đơn hàng đầu tiên!";
            if (rewardDescText != null) rewardDescText.text = "Bạn đã hoàn thành đơn hàng đầu tiên!\nTiếp tục khám phá thêm nhiều NPC khác nhé!";
        }
    }

    private void OnRewardClosed()
    {
        if (rewardPopup != null) rewardPopup.SetActive(false);
        SetStep(TutorialStep.Step4_Contextual);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region STEP 4 – Contextual Tooltips

    private void ActivateContextualTooltips()
    {
        if (contextualTriggers == null) return;
        foreach (var t in contextualTriggers)
            if (t != null) t.Enable();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Complete & Skip

    private void CompleteTutorial()
    {
        PlayerPrefs.SetInt(TUTORIAL_DONE_KEY, 1);
        PlayerPrefs.SetInt(TUTORIAL_STEP_KEY, (int)TutorialStep.Completed);
        PlayerPrefs.Save();
        HideAllTutorialUI();
        ShowCompanionPanel(false);
        ActivateContextualTooltips();
        Debug.Log("[TutorialGamePlay] Tutorial hoàn thành! 🎉");
    }

    public void SkipTutorial()
    {
        StopAllCoroutines();
        CompleteTutorial();
    }

    [ContextMenu("DEBUG: Reset Tutorial")]
    public void DebugResetTutorial()
    {
        PlayerPrefs.DeleteKey(TUTORIAL_DONE_KEY);
        PlayerPrefs.DeleteKey(TUTORIAL_STEP_KEY);
        PlayerPrefs.Save();
        Debug.Log("[TutorialGamePlay] Tutorial đã được reset!");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region UI Helpers

    private void SetOverlay(bool active)
        => darkOverlayPanel?.SetActive(active);

    private void ShowTooltip(string msg)
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(true);
        if (tooltipText != null) tooltipText.text = msg;
    }

    private void HideTooltip()
        => tooltipPanel?.SetActive(false);

    private void ShowArrow(RectTransform target)
    {
        if (arrowUI == null) return;
        arrowUI.gameObject.SetActive(true);
        if (target != null)
            arrowUI.position = target.position + new Vector3(-80f, 0f, 0f);
        StartArrowBounce();
    }

    private void HideArrow()
    {
        StopArrowBounce();
        arrowUI?.gameObject.SetActive(false);
    }

    private void HideAllTutorialUI()
    {
        SetOverlay(false);
        HideTooltip();
        HideArrow();
        ShowCompanionPanel(false);
        if (spotlightRing != null) spotlightRing.gameObject.SetActive(false);
        if (rewardPopup != null) rewardPopup.SetActive(false);
    }

    private void SetShopInteractable(bool state)
    {
        if (chatSendButton != null) chatSendButton.interactable = state;
        if (bagOpenButton != null) bagOpenButton.interactable = state;
        if (itemButtons != null)
            foreach (var btn in itemButtons)
                if (btn != null) btn.interactable = state;
    }

    private void StartArrowBounce()
    {
        StopArrowBounce();
        if (arrowUI != null)
            arrowBounceCoroutine = StartCoroutine(ArrowBounceLoop());
    }

    private void StopArrowBounce()
    {
        if (arrowBounceCoroutine != null)
        {
            StopCoroutine(arrowBounceCoroutine);
            arrowBounceCoroutine = null;
        }
    }

    private IEnumerator ArrowBounceLoop()
    {
        if (arrowUI == null) yield break;
        Vector3 origin = arrowUI.localPosition;
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed / 0.5f, 1f);
            arrowUI.localPosition = origin + new Vector3(-12f * t, 0f, 0f);
            yield return null;
        }
    }

    private IEnumerator BounceScale(Transform target)
    {
        if (target == null) yield break;
        target.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime * 3f; target.localScale = Vector3.one * Mathf.SmoothStep(0f, 1.15f, Mathf.Clamp01(t)); yield return null; }
        t = 0f;
        while (t < 1f) { t += Time.deltaTime * 4f; target.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, t); yield return null; }
        target.localScale = Vector3.one;
    }

    /// TypeAIMessage: dùng npcAIChatText + bật CompanionChatPanel (Step 2+)
    private IEnumerator TypeAIMessage(string message)
    {
        // Bật companion panel khi NPC-AI nói chuyện trong shop
        ShowCompanionPanel(true);
        LoadCompanionSprite();

        if (companionChatText != null)
        {
            // Ưu tiên dùng companionChatText nếu companionChatPanel đang bật
            companionChatText.text = "";
            isTyping = true;
            foreach (char c in message)
            {
                companionChatText.text += c;
                yield return new WaitForSeconds(0.03f);
            }
            isTyping = false;
        }
        else if (npcAIChatText != null)
        {
            // Fallback: dùng npcAIChatText riêng
            npcAIChatText.text = "";
            foreach (char c in message)
            {
                npcAIChatText.text += c;
                yield return new WaitForSeconds(0.03f);
            }
        }
        yield return new WaitForSeconds(0.5f);
    }

    #endregion

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showDebugPanel) return;

        GUILayout.BeginArea(new Rect(10, 10, 280, 240));
        GUI.Box(new Rect(0, 0, 280, 240), "");

        GUILayout.Label($"── TUTORIAL DEBUG ──");
        GUILayout.Label($"Step hiện tại : {currentStep}");
        GUILayout.Label($"TutorialDone  : {PlayerPrefs.GetInt(TUTORIAL_DONE_KEY, 0)}");
        GUILayout.Label($"SavedStep     : {PlayerPrefs.GetInt(TUTORIAL_STEP_KEY, 0)}");
        GUILayout.Label($"CompanionID   : {PlayerPrefs.GetInt("SelectedCompanionID", 0)}");

        GUILayout.Space(6);

        if (GUILayout.Button("▶ Reset & Chạy lại từ đầu"))
        {
            DebugResetTutorial();
            CheckAndStartTutorial();
        }

        if (GUILayout.Button($"⏭ Nhảy tới: {debugStartAtStep}"))
        {
            StopAllCoroutines();
            HideAllTutorialUI();
            SetStep(debugStartAtStep);
        }

        if (GUILayout.Button("✅ Mark hoàn thành"))
        {
            CompleteTutorial();
        }

        if (GUILayout.Button("🗑 Xóa toàn bộ PlayerPrefs"))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Tutorial] Đã xóa toàn bộ PlayerPrefs");
        }

        GUILayout.EndArea();
    }
#endif
}*/

using System.Collections;
using System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Vector2 spotlightPadding = new Vector2(20f, 20f);

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
        _step = TutorialStep.Step1_WaitNPCClick;
        yield return new WaitForSeconds(1.8f);
        HideAllTutorialUI();
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
        HideCompanionPanel();
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
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region STEP 2A — Chat với AI
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator RunStep2_Chat()
    {
        ShowSpotlight(chatInputArea);
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

/*        if (productDetailPanel != null)
            ShowSpotlightWorldObject(productDetailPanel);*/

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

    private IEnumerator ShowCompanionMessage(string msg, bool wait)
    {
        if (companionPanel == null) yield break;
        companionPanel.SetActive(true);

        if (companionImage != null && tutorialCompanionSprite != null)
            companionImage.sprite = tutorialCompanionSprite;

        if (companionChatText != null)
        {
            companionChatText.text = "";
            foreach (char c in msg)
            {
                companionChatText.text += c;
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
        }
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
        StopSpotlightTracking(); // Dừng tracking cũ nếu có
        spotlightOverlay.SetActive(true);
        spotlightHole.position = target.position;
        spotlightHole.sizeDelta = target.rect.size + spotlightPadding;
    }
    private void ShowSpotlightWorldObject(GameObject go)
    {
        if (go == null) return;
        var rect = go.GetComponent<RectTransform>();
        if (rect != null) ShowSpotlight(rect);
    }
    private void ShowSpotlightOnWorld(Transform worldTarget, Vector2 size)
    {
        if (spotlightOverlay == null || spotlightHole == null || worldTarget == null) return;
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