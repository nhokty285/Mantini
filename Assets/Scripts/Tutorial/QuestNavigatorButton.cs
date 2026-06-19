using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// QuestNavigatorButton — Gắn vào GO_Quest panel.
/// Khi player không biết quest đang ở bước nào, bấm nút này để:
///   1. Camera tween đến vị trí world-space của quest step hiện tại.
///   2. Spotlight highlight đúng UI element cần thao tác (nếu có).
///   3. Companion hiển thị gợi ý ngắn về bước đó.
///
/// HOOKS cần wire trong Inspector:
///   - questNavButton         : Button "Tìm Quest" trên GO_Quest
///   - questNavLabel          : TMP text label trên button (optional)
///   - tutorialGamePlay       : ref đến TutorialGamePlay instance
///   - questTargetTransform   : Transform world-space của NPC / location cần đến
///   - highlightTargetRect    : RectTransform UI cần spotlight (optional)
///   - companionHintMessage   : Nội dung gợi ý hiển thị khi bấm
///   - cameraFollowTarget     : Transform camera sẽ follow (thường là Player)
///   - spotlightOverlay       : ref đến spotlight overlay GameObject (từ TutorialGamePlay)
///   - spotlightHole          : ref đến RectTransform "lỗ" spotlight
///   - spotlightPadding       : padding bổ sung cho spotlight hole
///
/// Performance notes (theo mantini-performance-data-structures):
///   - Không dùng FindObjectOfType trong Update — cache tất cả ref trong Awake/Start.
///   - Coroutine chỉ chạy khi button được bấm (event-driven), không chạy mỗi frame.
///   - WaitForSeconds dùng cached WaitForSeconds để tránh GC (pattern from mantini skill).
/// </summary>
public class QuestNavigatorButton : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — BUTTON UI
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Button UI ══════")]
    [SerializeField] private Button questNavButton;
    [SerializeField] private TextMeshProUGUI questNavLabel;
    [SerializeField] private string defaultLabel = "📍 Tìm Quest";
    [SerializeField] private string navigatingLabel = "⏳ Đang dẫn...";

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — QUEST TARGET
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Quest Target (World) ══════")]
    [Tooltip("Transform world-space của NPC hoặc vị trí quest hiện tại cần đến.")]
    [SerializeField] private Transform questTargetTransform;

    [Tooltip("Camera sẽ di chuyển tới gần target này.")]
    [SerializeField] private Transform cameraFollowTarget;

    [Tooltip("Thời gian camera tween tới vị trí target.")]
    [SerializeField] private float cameraTweenDuration = 1.2f;

    [Tooltip("Giữ khoảng cách này từ target khi camera di chuyển tới.")]
    [SerializeField] private float cameraApproachOffset = 5f;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — SPOTLIGHT
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Spotlight (UI Highlight) ══════")]
    [Tooltip("UI element cần được spotlight khi bấm nút (optional).")]
    [SerializeField] private RectTransform highlightTargetRect;

    [SerializeField] private GameObject spotlightOverlay;
    [SerializeField] private RectTransform spotlightHole;
    [SerializeField] private Vector2 spotlightPadding = new Vector2(20f, 20f);

    [Tooltip("Spotlight tự tắt sau bao nhiêu giây.")]
    [SerializeField] private float spotlightAutoHide = 3f;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — COMPANION HINT
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Companion Hint ══════")]
    [SerializeField] private GameObject companionPanel;
    [SerializeField] private TextMeshProUGUI companionHintText;

    [TextArea(2, 4)]
    [SerializeField] private string companionHintMessage =
        "Bạn cần đến đây để tiếp tục quest!\nHãy bấm vào NPC để mở Shop.";

    [Tooltip("Companion hint tự tắt sau bao nhiêu giây.")]
    [SerializeField] private float hintAutoHide = 3f;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — REFERENCE
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ References ══════")]
    [Tooltip("Kéo TutorialGamePlay vào đây để lấy current step.")]
    [SerializeField] private TutorialGamePlay tutorialGamePlay;

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    // Cache WaitForSeconds để tránh GC mỗi lần coroutine chạy
    private WaitForSeconds _waitSpotlightHide;
    private WaitForSeconds _waitHintHide;

    private bool _isNavigating = false;
    private Coroutine _navigationCoroutine;

    // ════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Cache WaitForSeconds — avoid GC allocation mỗi lần coroutine start
        _waitSpotlightHide = new WaitForSeconds(spotlightAutoHide);
        _waitHintHide = new WaitForSeconds(hintAutoHide);
    }

    private void Start()
    {
        SetupButton();

        // Fallback: nếu chưa wire tutorialGamePlay, tự tìm (chỉ chạy 1 lần ở Start)
        if (tutorialGamePlay == null)
            tutorialGamePlay = TutorialGamePlay.Instance;
    }

    private void OnDestroy()
    {
        if (questNavButton != null)
            questNavButton.onClick.RemoveListener(OnQuestNavButtonClicked);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region SETUP
    // ════════════════════════════════════════════════════════════════════════

    private void SetupButton()
    {
        if (questNavButton == null)
        {
            GameLog.Warn("[QuestNavigatorButton] questNavButton chưa được wire trong Inspector!");
            return;
        }

        questNavButton.onClick.RemoveAllListeners();
        questNavButton.onClick.AddListener(OnQuestNavButtonClicked);

        UpdateButtonLabel(defaultLabel);
        GameLog.Info("[QuestNavigatorButton] Setup hoàn tất.");
    }

    private void UpdateButtonLabel(string label)
    {
        if (questNavLabel != null)
            questNavLabel.text = label;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region BUTTON HANDLER
    // ════════════════════════════════════════════════════════════════════════

    private void OnQuestNavButtonClicked()
    {
        if (_isNavigating)
        {
            GameLog.Info("[QuestNavigatorButton] Đang navigate, bỏ qua click.");
            return;
        }

        if (_navigationCoroutine != null)
            StopCoroutine(_navigationCoroutine);

        _navigationCoroutine = StartCoroutine(NavigateToQuestTarget());
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region NAVIGATION COROUTINE
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Flow khi player bấm nút:
    /// 1. Lock button (tránh spam click)
    /// 2. Tween camera đến vị trí quest target (nếu có)
    /// 3. Show spotlight lên UI element cần thao tác (nếu có)
    /// 4. Show companion hint
    /// 5. Auto-cleanup sau vài giây
    /// </summary>
    private IEnumerator NavigateToQuestTarget()
    {
        _isNavigating = true;
        questNavButton.interactable = false;
        UpdateButtonLabel(navigatingLabel);

        AudioManager.Instance?.PlaySFXOneShot("Whoosh");

        // ── STEP 1: Di chuyển camera đến quest target ──────────────────────
        if (questTargetTransform != null && cameraFollowTarget != null)
        {
            yield return TweenCameraToTarget();
        }
        else if (questTargetTransform != null)
        {
            GameLog.Warn("[QuestNavigatorButton] cameraFollowTarget chưa wire — bỏ qua camera tween.");
        }

        // ── STEP 2: Spotlight UI element (nếu có) ──────────────────────────
        if (highlightTargetRect != null && spotlightOverlay != null && spotlightHole != null)
        {
            ShowSpotlight(highlightTargetRect);
            // Tự tắt spotlight sau thời gian cấu hình
            StartCoroutine(AutoHideSpotlight());
        }

        // ── STEP 3: Companion hint ──────────────────────────────────────────
        ShowCompanionHint();
        StartCoroutine(AutoHideHint());

        // ── STEP 4: Restore button sau khi animation xong ──────────────────
        yield return _waitSpotlightHide;

        _isNavigating = false;
        questNavButton.interactable = true;
        UpdateButtonLabel(defaultLabel);

        GameLog.Info("[QuestNavigatorButton] Navigation hoàn tất.");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region CAMERA HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tween cameraFollowTarget đến gần questTargetTransform.
    /// Dùng DOTween theo chuẩn Mantini (project dùng DOTween).
    /// Không alloc object mới — DOTween reuse tween instance.
    /// </summary>
    private IEnumerator TweenCameraToTarget()
    {
        Vector3 direction = (cameraFollowTarget.position - questTargetTransform.position).normalized;
        Vector3 targetPos = questTargetTransform.position + direction * cameraApproachOffset;
        targetPos.y = cameraFollowTarget.position.y; // giữ nguyên chiều cao camera

        bool tweenDone = false;
        cameraFollowTarget.DOMove(targetPos, cameraTweenDuration)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => tweenDone = true);

        yield return new WaitUntil(() => tweenDone);
        GameLog.Info($"[QuestNavigatorButton] Camera đã đến gần quest target: {questTargetTransform.name}");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region SPOTLIGHT HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void ShowSpotlight(RectTransform target)
    {
        if (spotlightOverlay == null || spotlightHole == null || target == null) return;

        spotlightOverlay.SetActive(true);
        spotlightHole.gameObject.SetActive(true);
        spotlightHole.position = target.position;
        spotlightHole.sizeDelta = target.rect.size + spotlightPadding;

        GameLog.Info($"[QuestNavigatorButton] Spotlight → {target.name}");
    }

    private void HideSpotlight()
    {
        spotlightOverlay?.SetActive(false);
        spotlightHole?.gameObject.SetActive(false);
    }

    private IEnumerator AutoHideSpotlight()
    {
        yield return _waitSpotlightHide;
        HideSpotlight();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region COMPANION HINT HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private void ShowCompanionHint()
    {
        if (companionPanel == null || companionHintText == null) return;

        companionHintText.text = companionHintMessage;
        companionPanel.SetActive(true);
        GameLog.Info("[QuestNavigatorButton] Companion hint hiển thị.");
    }

    private IEnumerator AutoHideHint()
    {
        yield return _waitHintHide;
        companionPanel?.SetActive(false);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region PUBLIC API — Cập nhật target theo quest step
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cập nhật quest target và hint message từ code khác (ví dụ TutorialGamePlay).
    /// Gọi method này mỗi khi quest chuyển bước.
    /// </summary>
    public void SetQuestTarget(
        Transform targetTransform,
        RectTransform uiHighlight,
        string hintMessage)
    {
        questTargetTransform = targetTransform;
        highlightTargetRect = uiHighlight;
        companionHintMessage = hintMessage;
        GameLog.Info($"[QuestNavigatorButton] Quest target cập nhật: {targetTransform?.name ?? "null"}");
    }

    /// <summary>Cập nhật chỉ message hint (khi target không đổi nhưng hint text thay đổi).</summary>
    public void SetHintMessage(string message)
    {
        companionHintMessage = message;
    }

    /// <summary>Ẩn/hiện nút từ ngoài (ví dụ khi quest hoàn thành).</summary>
    public void SetButtonVisible(bool visible)
    {
        if (questNavButton != null)
            questNavButton.gameObject.SetActive(visible);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region DEBUG
    // ════════════════════════════════════════════════════════════════════════

    [ContextMenu("Test: Simulate Button Click")]
    public void DEBUG_SimulateClick() => OnQuestNavButtonClicked();

    [ContextMenu("Test: Show Spotlight Only")]
    public void DEBUG_ShowSpotlight()
    {
        if (highlightTargetRect != null)
            ShowSpotlight(highlightTargetRect);
        else
            GameLog.Warn("[QuestNavigatorButton] highlightTargetRect chưa wire.");
    }

    #endregion
}