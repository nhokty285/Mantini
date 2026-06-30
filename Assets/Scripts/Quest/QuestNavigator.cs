using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Quest Navigator — gắn vào Canvas/GO_Quest. Khi player click button quest, script sẽ:
///   1. Xác định step quest hiện tại (= section đầu tiên CHƯA seen trong PlayerPrefs).
///   2. Auto-move player tới NPC target bằng NavMeshAgent (tránh vật cản tự động).
///   3. Mở UI panel tương ứng (Shop / Cart) khi tới nơi.
///
/// Design notes:
///   • Không sửa TutorialGamePlay.cs — đọc trực tiếp PlayerPrefs với cùng KEY_SEEN_PREFIX
///     để zero-coupling, vẫn share state.
///   • Dictionary&lt;TutorialSection, Action&gt; lookup O(1) thay vì switch-case dài.
///   • NavMeshAgent xử lý pathfinding A* + tránh vật cản — không cần lerp thủ công.
///   • Có timeout 30s phòng case agent không tìm được path → không treo coroutine vĩnh viễn.
///
/// Dependencies (giữ nguyên signature, không sửa file khác):
///   - PlayerController.Instance.SetCanMove(bool)
///   - GameplayPlayerSpawner.Instance.SpawnedPlayer
///   - VendorNPC.ProcessInteraction()
///   - NavMesh đã được bake trong scene (kiểm tra qua NavMeshBaker GameObject)
/// </summary>
[RequireComponent(typeof(Button))]
public class QuestNavigator : MonoBehaviour
{
    public static QuestNavigator Instance { get; private set; }

    // ── PlayerPrefs Keys ────────────────────────────────────────────────────
    // PHẢI KHỚP với KEY_SEEN_PREFIX trong TutorialGamePlay.cs
    private const string KEY_SEEN_PREFIX = "MapTest2_TutSeen_";

    // Cache mảng enum — tránh System.Enum.GetValues() alloc + reflection mỗi click
    private static readonly TutorialGamePlay.TutorialSection[] AllSteps =
        (TutorialGamePlay.TutorialSection[])System.Enum.GetValues(typeof(TutorialGamePlay.TutorialSection));

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — QUEST BUTTON
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Quest Button ══════")]
    [SerializeField] private Button questButton;
    [Tooltip("Image dùng cho hiệu ứng punch scale khi click. Có thể để trống.")]
    [SerializeField] private RectTransform feedbackTarget;

    [Tooltip("Text hiển thị trạng thái 'đang tìm đường' khi auto-move. Có thể để trống.")]
    [SerializeField] private GameObject statusTextObject;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — WORLD TARGET
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ World Target (Auto-Move) ══════")]
    [Tooltip("NPC để player auto-move tới (NPC_Eyewear_New).")]
    [SerializeField] private Transform npcTargetTransform;

    [Header("══════ Joystick Interrupt ══════")]
    [Tooltip("Joystick di chuyển (FloatingJoystick trên GO_MobileController). Khi player kéo joystick → ngắt auto-move.")]
    [SerializeField] private Joystick movementJoystick;
    [Tooltip("Ngưỡng joystick magnitude để coi là player chủ động điều khiển (0.1-0.3).")]
    [SerializeField] private float joystickInterruptThreshold = 0.2f;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — NAVMESH AGENT SETTINGS
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ NavMeshAgent Settings ══════")]
    [Tooltip("Tốc độ di chuyển (m/s) — sẽ set cho NavMeshAgent.speed.")]
    [SerializeField] private float autoMoveSpeed = 3f;
    [Tooltip("Gia tốc (m/s²).")]
    [SerializeField] private float acceleration = 50f; // cao = ramp tốc độ tức thì, bớt khựng đầu
    [Tooltip("Tốc độ xoay (deg/s) — cho NavMeshAgent internal (không dùng khi tự nội suy).")]
    [SerializeField] private float angularSpeed = 720f;
    [Tooltip("Tốc độ nội suy xoay người khi auto-move. Cao = xoay nhanh. 8-12 là mượt.")]
    [SerializeField] private float rotationLerpSpeed = 10f;
    [Tooltip("Khoảng cách dừng tới target (m). Cũng dùng làm NavMeshAgent.stoppingDistance.")]
    [SerializeField] private float arrivalDistance = 2.5f;
    [Tooltip("Radius của agent (m). Ảnh hưởng tới né vật cản.")]
    [SerializeField] private float agentRadius = 0.4f;
    [Tooltip("Chiều cao của agent (m).")]
    [SerializeField] private float agentHeight = 1.8f;
    [Tooltip("Timeout (s) phòng case agent không tìm được path.")]
    [SerializeField] private float autoMoveTimeout = 30f;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — UI PANEL REFS
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ UI Panels ══════")]
    [Tooltip("Nút mở Cart (quickCartButton trong shop). Dùng cho step Cart/Checkout.")]
    [SerializeField] private Button quickCartButton;

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR — DEBUG
    // ════════════════════════════════════════════════════════════════════════
    [Header("══════ Debug ══════")]
    [SerializeField] private bool enableDebugLog = true;

    // ── Internal State ──────────────────────────────────────────────────────
    private Dictionary<TutorialGamePlay.TutorialSection, System.Action> _stepActions;
    private Coroutine _autoMoveCoroutine;
    private bool _isAutoMoving = false;

    // ════════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Auto-bind button trên cùng GameObject
        if (questButton == null) questButton = GetComponent<Button>();
        statusTextObject.gameObject.SetActive(false);
        BuildStepActions();
    }

    private void Start()
    {
        if (questButton != null)
        {
            // Xoá listener cũ trước khi gán mới — tránh subscribe nhiều lần
            questButton.onClick.RemoveAllListeners();
            questButton.onClick.AddListener(OnQuestButtonClicked);
        }
        else
        {
            Debug.LogError("[QuestNavigator] questButton là null! Quest button không hoạt động.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Step Actions — Dictionary O(1) lookup
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build mapping step → action. Dictionary lookup O(1) thay vì switch-case dài,
    /// và dễ bảo trì khi thêm step mới (chỉ cần add 1 entry).
    /// </summary>
    private void BuildStepActions()
    {
        _stepActions = new Dictionary<TutorialGamePlay.TutorialSection, System.Action>(AllSteps.Length)
        {
            { TutorialGamePlay.TutorialSection.Approach,      () => MoveToNPC(openShopOnArrival: false) },
            { TutorialGamePlay.TutorialSection.NPCClick,      () => MoveToNPC(openShopOnArrival: false) },
            { TutorialGamePlay.TutorialSection.Chat,          () => MoveToNPC(openShopOnArrival: true)  },
            { TutorialGamePlay.TutorialSection.SelectItem,    () => MoveToNPC(openShopOnArrival: true)  },
            { TutorialGamePlay.TutorialSection.ProductDetail, () => MoveToNPC(openShopOnArrival: true)  },
            { TutorialGamePlay.TutorialSection.BackToShop,    () => MoveToNPC(openShopOnArrival: true)  },
            { TutorialGamePlay.TutorialSection.OpenBag,       () => MoveToNPC(openShopOnArrival: true)  },
            { TutorialGamePlay.TutorialSection.Cart,          OpenCartPanel },
            { TutorialGamePlay.TutorialSection.Checkout,      OpenCartPanel },
            { TutorialGamePlay.TutorialSection.Reward,        () => DebugLog("Quest đã hoàn thành — không cần navigate.") },
        };
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Click Handler & Step Detection
    // ════════════════════════════════════════════════════════════════════════

    private void OnQuestButtonClicked()
    {
        if (_isAutoMoving)
        {
            DebugLog("Đang auto-move, bỏ qua click.");
            return;
        }

        PlayClickFeedback();

        TutorialGamePlay.TutorialSection currentStep = GetCurrentStep();
        DebugLog($"Click quest button — step hiện tại: {currentStep}");

        if (_stepActions.TryGetValue(currentStep, out var action))
        {
            action?.Invoke();
        }
        else
        {
            GameLog.Warn($"[QuestNavigator] Không có action cho step {currentStep}");
        }
    }

    /// <summary>
    /// Trả về step quest hiện tại — đọc từ TutorialGamePlay.Instance.CurrentSection.
    /// Single source of truth: không đọc PlayerPrefs raw nữa.
    /// Fallback về Approach nếu TutorialGamePlay chưa init (defensive).
    /// </summary>
    private TutorialGamePlay.TutorialSection GetCurrentStep()
    {
        if (TutorialGamePlay.Instance == null)
        {
            GameLog.Warn("[QuestNavigator] TutorialGamePlay.Instance null — fallback Approach.");
            return TutorialGamePlay.TutorialSection.Approach;
        }
        return TutorialGamePlay.Instance.CurrentSection;
    }

    private void PlayClickFeedback()
    {
        if (feedbackTarget == null) return;
        feedbackTarget.DOKill();
        feedbackTarget.localScale = Vector3.one;
        feedbackTarget.DOPunchScale(Vector3.one * 0.15f, 0.3f, 5, 1f);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Auto-Move (NavMeshAgent)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bắt đầu auto-move tới NPC bằng NavMeshAgent. Nếu đang move, stop coroutine cũ.
    /// </summary>
    private void MoveToNPC(bool openShopOnArrival)
    {
        if (npcTargetTransform == null)
        {
            Debug.LogError("[QuestNavigator] npcTargetTransform chưa wire!");
            return;
        }

        if (_autoMoveCoroutine != null) StopCoroutine(_autoMoveCoroutine);
        _autoMoveCoroutine = StartCoroutine(AutoMoveRoutine(npcTargetTransform.position, openShopOnArrival));
    }

    /// <summary>
    /// Coroutine sử dụng NavMeshAgent để pathfind tới target.
    ///
    /// Flow:
    ///   1. Lấy/tạo NavMeshAgent trên player.
    ///   2. Disable Rigidbody physics (kinematic) để agent control transform.
    ///   3. Warp agent về vị trí player (snap to NavMesh).
    ///   4. SetDestination → agent tự pathfind.
    ///   5. Poll mỗi FixedUpdate: pathPending? remainingDistance? velocity?
    ///   6. Khi tới nơi → restore physics, unlock input, mở shop nếu cần.
    /// </summary>
    private IEnumerator AutoMoveRoutine(Vector3 targetPos, bool openShopOnArrival)
    {
        var player = GameplayPlayerSpawner.Instance != null
            ? GameplayPlayerSpawner.Instance.SpawnedPlayer
            : null;

        if (player == null)
        {
            Debug.LogError("[QuestNavigator] Player chưa spawn!");
            yield break;
        }

        // Get hoặc add NavMeshAgent component
        var agent = player.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = player.AddComponent<NavMeshAgent>();
            DebugLog("Đã add NavMeshAgent vào player.");
        }

        // Configure agent — luôn re-config phòng case Inspector edit lúc playing
        agent.speed = autoMoveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = arrivalDistance;
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.autoBraking = true;
        agent.updateRotation = false; // Tự nội suy rotation mượt thay vì agent snap
        // Tắt avoidance internal — path NavMesh đã clean, avoidance gây jitter
        agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;

        var rb = player.GetComponent<Rigidbody>();
        var anim = player.GetComponent<Animator>();
        var col = player.GetComponent<Collider>();
        var playerCtrl = PlayerController.Instance;

        _isAutoMoving = true;

        // Task 1: Hiện status text 'đang tìm đường'
        if (statusTextObject != null) statusTextObject.SetActive(true);

        // Lock player input (Cinemachine + PlayerController)
        PlayerController.Instance?.SetCanMove(false);

        // Disable PlayerController hoàn toàn để LateUpdate không ghi đè
        // animator params (isMoving, moveSpeed) mỗi frame trong khi auto-move.
        bool wasPlayerCtrlEnabled = playerCtrl != null && playerCtrl.enabled;
        if (playerCtrl != null) playerCtrl.enabled = false;

        // Tắt physics để agent toàn quyền điều khiển transform
        bool wasKinematic = rb != null && rb.isKinematic;
        var prevInterp = rb != null ? rb.interpolation : RigidbodyInterpolation.None;
        if (rb != null)
        {
            rb.isKinematic = true;
            // KHÔNG set Interpolate: khi agent điều khiển transform, Interpolate khiến
            // rb.position và transform.position đá nhau → giật. Tắt hẳn khi auto-move.
            rb.interpolation = RigidbodyInterpolation.None;
        }

        // Collider thành trigger → không va chạm vật lý với obstacle (hết jitter),
        // nhưng vẫn fire OnTriggerEnter của NPC trigger zone.
        bool wasTrigger = col != null && col.isTrigger;
        if (col != null) col.isTrigger = true;

        // Enable agent
        agent.enabled = true;

        // Warp agent về vị trí player (snap to NavMesh). Quan trọng vì player có thể
        // đang đứng ngay sát mép NavMesh hoặc lệch nhẹ — Warp tìm điểm gần nhất.
        if (!agent.Warp(player.transform.position))
        {
            GameLog.Warn("[QuestNavigator] agent.Warp failed — player không gần NavMesh!");
        }

        // Đặt destination — agent sẽ tự pathfind tránh vật cản
        if (!agent.SetDestination(targetPos))
        {
            GameLog.Warn("[QuestNavigator] SetDestination failed — target có thể không trên NavMesh!");
        }

        // Set BOTH params: isMoving (Bool) là gate trigger transition, moveSpeed (Float) là blend value.
        // Phải set sau SetCanMove(false) vì PlayerController reset isMoving=false khi lock input.
        if (anim != null)
        {
            anim.SetBool("isMoving", true);
            anim.SetFloat("moveSpeed", 1f);
        }

        var fixedWait = new WaitForFixedUpdate();
        float elapsed = 0f;
        bool interruptedByJoystick = false; // Task 2: track lý do thoát loop

        while (true)
        {
            // Safety timeout
            if (elapsed >= autoMoveTimeout)
            {
                GameLog.Warn("[QuestNavigator] Auto-move timeout — dừng.");
                break;
            }

            // Task 2: Player kéo joystick → ngắt auto-move, trả quyền điều khiển
            if (movementJoystick != null &&
                movementJoystick.Direction.magnitude >= joystickInterruptThreshold)
            {
                DebugLog("Player dùng joystick — ngắt auto-move.");
                interruptedByJoystick = true;
                break;
            }

            // Đợi agent compute path xong
            if (agent.pathPending)
            {
                elapsed += Time.fixedDeltaTime;
                yield return fixedWait;
                continue;
            }

            // Tự nội suy rotation mượt theo hướng di chuyển (agent.velocity).
            // Slerp thay vì snap → player xoay người tự nhiên cả lúc start lẫn bẻ cua.
            Vector3 vel = agent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(vel.normalized);
                player.transform.rotation = Quaternion.Slerp(
                    player.transform.rotation, targetRot, rotationLerpSpeed * Time.fixedDeltaTime);
            }

            // ── Kiểm tra đã tới nơi (logic 2 tầng để tránh false-arrival do partial path) ──
            //
            // VẤN ĐỀ CŨ: chỉ check agent.remainingDistance → khi NavMesh path bị PARTIAL
            // (không nối thẳng tới NPC vì obstacle/khe chặn), agent dừng ở điểm cuối path
            // → remainingDistance=0 → code coi 'đã đến NPC' dù còn cách rất xa.
            //
            // FIX: phân biệt 2 trường hợp
            //  (a) Path COMPLETE + thực sự gần NPC → coi như đã đến, mở shop bình thường
            //  (b) Path PARTIAL + agent đã dừng → đánh dấu interrupt, KHÔNG mở shop sai

            float realDistToNPC = UnityEngine.Vector3.Distance(player.transform.position, targetPos);
            bool agentStopped = agent.remainingDistance <= agent.stoppingDistance
                             && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.1f);

            if (agentStopped)
            {
                // (a) Đã thật sự tới gần NPC → arrival hợp lệ
                if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete
                    && realDistToNPC <= arrivalDistance + 1.5f)
                {
                    break; // success, sẽ mở shop trong cleanup
                }

                // (b) Agent đã dừng nhưng KHÔNG tới được NPC (partial path hoặc kẹt)
                // → log warning + đánh dấu interrupt để KHÔNG mở shop sai chỗ
                GameLog.Warn($"[QuestNavigator] Agent dừng nhưng chưa tới NPC! " +
                    $"pathStatus={agent.pathStatus}, realDist={realDistToNPC:F1}m, " +
                    $"remainingDist={agent.remainingDistance:F1}m. Không mở shop.");
                interruptedByJoystick = true; // tái dùng flag để skip shop
                break;
            }

            elapsed += Time.fixedDeltaTime;
            yield return fixedWait;
        }

        // Stop agent
        if (agent.isOnNavMesh) agent.ResetPath();
        agent.enabled = false;

        // Restore physics + animator + collider
        if (col != null) col.isTrigger = wasTrigger;
        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.interpolation = prevInterp;
        }
        if (anim != null)
        {
            anim.SetBool("isMoving", false);
            anim.SetFloat("moveSpeed", 0f);
        }

        _isAutoMoving = false;

        // Task 1: Ẩn status text
        if (statusTextObject != null) statusTextObject.SetActive(false);

        // Restore PlayerController trước — nó cần enabled để SetCanMove hoạt động
        if (playerCtrl != null) playerCtrl.enabled = wasPlayerCtrlEnabled;

        // Unlock input
        PlayerController.Instance?.SetCanMove(true);

        if (interruptedByJoystick)
        {
            DebugLog("Auto-move bị ngắt bởi joystick — KHÔNG mở shop.");
        }
        else
        {
            DebugLog("Đã đến NPC.");
            if (openShopOnArrival) TriggerNPCInteraction();
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region UI Panel Openers
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mở shop hoàn chỉnh sau khi tới NPC. Quy trình 2-tầng:
    ///   1. ProcessInteraction() — show BT_Talk button + load shop data.
    ///   2. Đợi 1 frame để UI cập nhật, rồi:
    ///      - Nếu BT_Talk visible (lần đầu chat) → invoke onClick → dialogue → shop tự mở.
    ///      - Nếu BT_Talk ẩn (đã chat trước đó) → gọi AutoOpenShop() trực tiếp.
    /// </summary>
    private void TriggerNPCInteraction()
    {
        if (npcTargetTransform == null) return;

        var vendorNPC = npcTargetTransform.GetComponent<VendorNPC>();
        if (vendorNPC == null)
        {
            GameLog.Warn("[QuestNavigator] Target NPC không có VendorNPC component!");
            return;
        }

        vendorNPC.ProcessInteraction(); // Show talk button + load shop data
        DebugLog("Triggered ProcessInteraction → đang đợi UI update.");

        // Đợi 1 frame để UI active state cập nhật, rồi auto-trigger flow tiếp
        StartCoroutine(AutoOpenShopFlow());
    }

    /// <summary>
    /// Sau ProcessInteraction, đợi 1 frame rồi auto-click BT_Talk (nếu hiện)
    /// hoặc gọi AutoOpenShop trực tiếp (nếu đã chat rồi).
    /// </summary>
    private IEnumerator AutoOpenShopFlow()
    {
        // Đợi UI ToolKit/Canvas resolve active state — 1 frame là đủ
        yield return null;

        var mainMenu = MainMenuView.Instance;
        if (mainMenu == null)
        {
            GameLog.Warn("[QuestNavigator] MainMenuView.Instance null — không mở shop được!");
            yield break;
        }

        bool talkVisible = mainMenu.talkButton != null
                        && mainMenu.talkButton.gameObject.activeInHierarchy
                        && mainMenu.talkButton.interactable;

        if (talkVisible)
        {
            // Lần đầu: BT_Talk đang hiện → invoke onClick → dialogue → shop tự mở sau dialogue
            mainMenu.talkButton.onClick.Invoke();
            DebugLog("Auto-clicked BT_Talk → dialogue sẽ chạy → shop mở sau khi xong.");
        }
        else
        {
            // Đã chat rồi: BT_Talk ẩn → mở shop trực tiếp qua AutoOpenShop (skip dialogue)
            mainMenu.AutoOpenShop();
            DebugLog("BT_Talk ẩn → AutoOpenShop() trực tiếp (skip dialogue).");
        }
    }

    /// <summary>
    /// Mở cart panel — invoke quickCartButton.onClick để tận dụng flow có sẵn.
    /// </summary>
    private void OpenCartPanel()
    {
        if (quickCartButton != null)
        {
            quickCartButton.onClick.Invoke();
            DebugLog("Triggered quickCartButton.onClick — cart sẽ mở.");
        }
        else
        {
            GameLog.Warn("[QuestNavigator] quickCartButton chưa wire!");
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Debug
    // ════════════════════════════════════════════════════════════════════════

    private void DebugLog(string msg)
    {
        if (enableDebugLog) GameLog.Info($"[QuestNavigator] {msg}");
    }

    [ContextMenu("Test: Show Current Step")]
    public void DEBUG_ShowCurrentStep()
    {
        GameLog.Info($"[QuestNavigator] Current step: {GetCurrentStep()}");
    }

    [ContextMenu("Test: Trigger Quest Click")]
    public void DEBUG_TriggerClick()
    {
        OnQuestButtonClicked();
    }

    [ContextMenu("Test: Auto-Move Only (no shop open)")]
    public void DEBUG_AutoMoveOnly()
    {
        MoveToNPC(openShopOnArrival: false);
    }

    #endregion
}