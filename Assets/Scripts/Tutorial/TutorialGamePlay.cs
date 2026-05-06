using System.Collections;
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
        Step1_MoveToNPC = 0,  // Spotlight + arrow → bấm NPC
        Step2_Chat = 1,  // AI chat → dạy chat input
        Step2_SelectItem = 2,  // Arrow → chọn item trên quầy
        Step2_OpenBag = 3,  // Arrow → bấm Bag button
        Step3_Checkout = 4,  // Điền thông tin → thanh toán
        Step3_Reward = 5,  // Hiện badge reward
        Step4_Contextual = 6,  // Tooltip tự khám phá
        Completed = 99
    }

    private TutorialStep currentStep = TutorialStep.None;

    // ─── World References (assign trong Inspector) ────────────────────────
    [Header("=== WORLD REFERENCES ===")]
    [SerializeField] private Transform firstNPC;
    [SerializeField] private Camera mainCamera;

    // ─── UI Overlay ───────────────────────────────────────────────────────
    [Header("=== UI OVERLAY ===")]
    [SerializeField] private Canvas tutorialCanvas;       // Sort Order cao nhất
    [SerializeField] private GameObject darkOverlayPanel; // Panel tối (Image alpha ~0.75)
    [SerializeField] private RectTransform arrowUI;       // Mũi tên animated
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private Button skipTutorialButton;

    // ─── Step 1 ───────────────────────────────────────────────────────────
    [Header("=== STEP 1: Spotlight NPC ===")]
    [SerializeField] private Image spotlightRing;    // Ring glow quanh NPC

    // ─── Step 2 (Shop UI) ─────────────────────────────────────────────────
    [Header("=== STEP 2: Shop UI ===")]
    [SerializeField] private RectTransform chatInputArea;
    [SerializeField] private RectTransform itemGridArea;
    [SerializeField] private RectTransform bagButtonRect;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private Button[] itemButtons;
    [SerializeField] private Button bagOpenButton;
    [SerializeField] private TextMeshProUGUI npcAIChatText; // Text hiển thị lời NPC-AI

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

    // ─── NPC-AI Script (Step 2) ───────────────────────────────────────────
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

    // ─── DEBUG SETTINGS ──────────────────────────────────────────────────────
    [Header("=== DEBUG SETTINGS ===")]
    [SerializeField] private bool forceShowTutorial = false;
    // ↑ Tick vào Inspector → luôn chạy tutorial dù đã làm rồi

    [SerializeField] private bool skipAllTutorial = false;
    // ↑ Tick vào Inspector → luôn skip tutorial

    [SerializeField] private TutorialStep debugStartAtStep = TutorialStep.Step1_MoveToNPC;
    // ↑ Chọn muốn debug từ bước nào

    [Header("=== SPOTLIGHT OFFSET ===")]
    [SerializeField] private Vector3 spotlightWorldOffset = new Vector3(0f, 1.5f, 0f);

#if UNITY_EDITOR
    [SerializeField] private bool showDebugPanel = true;
    // ↑ Chỉ hiện trong Editor, không ảnh hưởng build
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

        CheckAndStartTutorial();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Entry Point

    /// Gọi khi load scene MapTest2 xong.
    public void CheckAndStartTutorial()
    {
#if UNITY_EDITOR
        // Debug: Force skip tất cả
        if (skipAllTutorial)
        {
            Debug.Log("[Tutorial] DEBUG: Skip toàn bộ tutorial");
            ActivateContextualTooltips();
            return;
        }

        // Debug: Force chạy tutorial dù đã xong
        if (forceShowTutorial)
        {
            Debug.Log($"[Tutorial] DEBUG: Force chạy từ bước {debugStartAtStep}");
            HideAllTutorialUI();
            SetStep(debugStartAtStep);
            return;
        }
#endif

        // Logic thật — build production
        bool isDone = PlayerPrefs.GetInt(TUTORIAL_DONE_KEY, 0) == 1;
        if (isDone) { ActivateContextualTooltips(); return; }

        int savedStep = PlayerPrefs.GetInt(TUTORIAL_STEP_KEY, 0);
        HideAllTutorialUI();
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
    #region STEP 1 – Spotlight + Arrow → NPC

    private IEnumerator RunStep1()
    {
        SetOverlay(true);
        if (spotlightRing != null && firstNPC != null)
        {
            spotlightRing.gameObject.SetActive(true);
            spotlightRing.transform.position = firstNPC.position; // ← DÒNG NÀY
            Debug.Log("[RunStep1] Đã đặt spotlightRing tại vị trí NPC: " + firstNPC.position);
        }
        ShowTooltip("👆 Di chuyển đến NPC và bấm vào để mở Shop!");
        ShowArrow(null); // Arrow tự update vị trí trong Update nếu cần
        yield return null;
    }
    private void Update()
    {
        if (currentStep != TutorialStep.Step1_MoveToNPC) return;
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
            canvasRect,
            screenPos,
            canvas.worldCamera, // ← truyền camera của Canvas vào đây
            out localPoint
        );

        spotlightRing.rectTransform.localPosition = localPoint;
    }
    /// Gọi từ NPCClickHandler khi player bấm NPC đầu tiên trong tutorial
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
        // Đợi OnPlayerSentChat() được gọi từ ChatController
    }

    /// Gọi từ ChatController sau khi player gửi tin nhắn đầu tiên
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

    /// Gọi từ ItemButton khi player chọn item
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

    /// Gọi từ BagButton khi player mở túi
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

    /// Gọi từ CheckoutController sau khi player hoàn thành thanh toán
    public void OnPlayerCompletedCheckout()
    {
        if (currentStep != TutorialStep.Step3_Checkout) return;
        HideTooltip();
        SetStep(TutorialStep.Step3_Reward);
    }

    private IEnumerator RunStep3_Reward()
    {
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
        SetOverlay(false); HideTooltip(); HideArrow();
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

    private IEnumerator TypeAIMessage(string message)
    {
        if (npcAIChatText == null) yield break;
        npcAIChatText.text = "";
        foreach (char c in message)
        {
            npcAIChatText.text += c;
            yield return new WaitForSeconds(0.03f);
        }
        yield return new WaitForSeconds(0.5f);
    }

    #endregion

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showDebugPanel) return;

        // Vẽ panel debug góc trên trái
        GUILayout.BeginArea(new Rect(10, 10, 280, 220));
        GUI.Box(new Rect(0, 0, 280, 220), "");

        GUILayout.Label($"── TUTORIAL DEBUG ──");
        GUILayout.Label($"Step hiện tại : {currentStep}");
        GUILayout.Label($"TutorialDone  : {PlayerPrefs.GetInt(TUTORIAL_DONE_KEY, 0)}");
        GUILayout.Label($"SavedStep     : {PlayerPrefs.GetInt(TUTORIAL_STEP_KEY, 0)}");

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
}