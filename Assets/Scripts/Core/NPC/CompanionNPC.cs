using UnityEngine;
using System.Collections.Generic;

public class CompanionNPC : BaseNPC
{
    [Header("Companion Movement")]
    [SerializeField] private float followDistance = 2f;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private bool shouldFollowPlayer = true;
    [SerializeField] private float teleportDistance = 10f; // Dịch chuyển về gần player nếu xa hơn khoảng này

    [Header("Idle Variations")]
    [SerializeField] private float minIdleTime = 3f;
    [SerializeField] private float maxIdleTime = 6f;
    [SerializeField] private int totalIdleEmotes = 3;   // số emote idle trong sub-machine (nếu có)

    [Header("Chat")]
    [SerializeField] private MultiChatManager _chatManager;

    [Header("Game Help Data")]
    [SerializeField] private List<GetHelpTopic> helpTopics = new();

    // Animator hashes — readonly để tránh allocation runtime
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int actionIdHash = Animator.StringToHash("ActionID");
    private readonly int doActionHash = Animator.StringToHash("DoAction");
    private readonly int emoteIdHash = Animator.StringToHash("EmoteID");
    private readonly int playEmoteHash = Animator.StringToHash("PlayEmote");

    // Movement
    private bool isFollowing;
    private Vector3 currentVelocity;

    // Idle random
    private float lastIdleTime;
    private float currentWaitTime;
    private int lastEmoteId = 0;
    private bool isPlayingIdleEmote = false;

    // Public actions (giữ public API cho code khác đang subscribe)
    public System.Action OnTypingStarted;
    public System.Action OnTypingStopped;

    protected override MultiChatManager GetChatManager() => _chatManager;

    void Start()
    {
        InitializeNPCData();
        AutoFindPlayer();
        ResetIdleTimer();

        if (NameplateManager.Instance != null)
            NameplateManager.Instance.Register(this.transform, npcName);

        // Refactor: chỉ Find khi Inspector chưa wire (tránh override SerializeField)
        if (_chatManager == null)
            _chatManager = FindFirstObjectByType<MultiChatManager>();
    }

    void FixedUpdate()
    {
        if (!shouldFollowPlayer || playerTransform == null)
        {
            UpdateAnimationSpeed(0f);
            HandleIdleEmotes(false);
            return;
        }

        Vector3 delta = transform.position - playerTransform.position;
        float sqrDist = delta.sqrMagnitude;

        if (sqrDist > teleportDistance * teleportDistance) { TeleportNearPlayer(); return; }

        float stopDist = followDistance + 0.05f;
        bool isCloseEnough = sqrDist <= stopDist * stopDist;

        if (!isCloseEnough)
        {
            FollowPlayer();
        }
        else
        {
            // Đã vào vùng đệm -> Ép vận tốc về 0 lập tức để cắt đứt di chuyển
            currentVelocity = Vector3.zero;
        }

        // Tính toán Speed truyền vào Animator
        float planarSpeed = currentVelocity.magnitude;

        // Ép tốc độ Animator về 0 tuyệt đối nếu đã tới gần hoặc vận tốc quá nhỏ
        if (isCloseEnough || planarSpeed < 0.05f)
            planarSpeed = 0f;

        UpdateAnimationSpeed(planarSpeed);
        HandleIdleEmotes(planarSpeed == 0f); // Tự tin check bằng 0f vì ta đã ép ở trên
    }

    #region Movement

    private void AutoFindPlayer()
    {
        if (playerTransform != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            isFollowing = true;
        }
    }

    private void TeleportNearPlayer()
    {
        // Dịch chuyển ngay về sau lưng player (offset ngược chiều mà player đang nhìn)
        Vector3 offset = -playerTransform.forward * followDistance;
        offset.y = 0f;
        transform.position = playerTransform.position + offset;

        // Reset velocity để tránh jitter sau khi teleport
        currentVelocity = Vector3.zero;

        // Quay mặt về phía player ngay lập tức
        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        // Đảm bảo animation không bị kẹt ở trạng thái chạy
        UpdateAnimationSpeed(0f);
#if UNITY_EDITOR
        GameLog.Info($"[CompanionNPC] {npcName} Teleported back to player (was too far).");
#endif
    }

    private void FollowPlayer()
    {
        if (!isFollowing) return;

        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        Vector3 targetPos = playerTransform.position - dir.normalized * followDistance;

        // Smooth move
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref currentVelocity,
            smoothTime,
            followSpeed
        );

        // Smooth rotation
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.fixedDeltaTime
        );
    }

    #endregion

    #region Animator – locomotion & idle emote

    private void UpdateAnimationSpeed(float speed)
    {
        if (npcAnimator == null) return;

        // Xóa bỏ quán tính (damp time) của Animator khi cần dừng hẳn
        if (speed == 0f)
            npcAnimator.SetFloat(speedHash, 0f);
        else
            npcAnimator.SetFloat(speedHash, speed, 0.1f, Time.deltaTime); // giữ độ mượt Walk->Run
    }

    private void ResetIdleTimer()
    {
        lastIdleTime = Time.time;
        currentWaitTime = Random.Range(minIdleTime, maxIdleTime);
    }

    private void HandleIdleEmotes(bool isIdle)
    {
        if (npcAnimator == null || totalIdleEmotes <= 0) return;

        // 1. KHI ĐANG DI CHUYỂN: Tắt trigger, ID=0, Dừng sub-machine
        if (!isIdle)
        {
            // Nếu đang có cờ hiệu diễn hoặc trigger đang chờ -> Dọn dẹp ngay
            if (isPlayingIdleEmote || npcAnimator.GetInteger(emoteIdHash) != 0)
            {
                npcAnimator.ResetTrigger(playEmoteHash);
                npcAnimator.SetInteger(emoteIdHash, 0);
                isPlayingIdleEmote = false;
            }

            ResetIdleTimer();
            return;
        }

        // 2. KHI ĐANG ĐỨNG YÊN
        if (isPlayingIdleEmote)
        {
            AnimatorStateInfo stateInfo = npcAnimator.GetCurrentAnimatorStateInfo(0);
            // Lưu ý: Nếu sub-machine ở layer khác thì thay số 0 bằng index layer đó
            if (stateInfo.normalizedTime >= 0.95f && !npcAnimator.IsInTransition(0))
            {
                isPlayingIdleEmote = false;
                npcAnimator.SetInteger(emoteIdHash, 0); // Diễn xong cũng về 0 luôn cho sạch
                ResetIdleTimer();
            }
            return;
        }

        if (Time.time - lastIdleTime < currentWaitTime) return;

        // Bắn Emote mới
        int emoteId = (totalIdleEmotes == 1) ? 1 : Random.Range(1, totalIdleEmotes + 1);
        while (totalIdleEmotes > 1 && emoteId == lastEmoteId)
            emoteId = Random.Range(1, totalIdleEmotes + 1);
        lastEmoteId = emoteId;

        npcAnimator.SetInteger(emoteIdHash, emoteId);
        npcAnimator.SetTrigger(playEmoteHash);
        isPlayingIdleEmote = true;
    }

    #endregion

    #region Public API – Actions sub-state machine

    // Gọi từ code khác khi muốn companion làm hành động (mở rương, chỉ trỏ, vẫy tay...)
    public void PlayAction(int actionId)
    {
        if (npcAnimator == null) return;

        npcAnimator.SetInteger(actionIdHash, actionId);
        npcAnimator.SetTrigger(doActionHash);

        // Animator (transition Has Exit Time) sẽ tự trả về Locomotion
    }

    public void PlayEmote(int emoteId)
    {
        if (npcAnimator == null) return;

        npcAnimator.SetInteger(emoteIdHash, emoteId);
        npcAnimator.SetTrigger(playEmoteHash);

        // Reset ID về 0 ngay sau 1 frame để nó không bị "dính"
        StartCoroutine(ResetEmoteIDAfterFrame());
    }

    private System.Collections.IEnumerator ResetEmoteIDAfterFrame()
    {
        yield return null; // Chờ 1 frame
        if (npcAnimator != null) npcAnimator.SetInteger(emoteIdHash, 0);
    }
    #endregion

    #region BaseNPC overrides

    // ─── IChatParticipant ─────────────────────────────────────────────
    // Refactor: bỏ duplicate (GetParticipantName/ID/Type, OnJoin/Leave) — base đã cover.
    // IsActive() vẫn cần override vì semantic khác base (không check `enabled`).
    public override bool IsActive() => gameObject.activeInHierarchy;

    public override void OnPlayerEnterRange()
    {
        // ví dụ: vẫy tay khi player lại gần
        PlayAction(1); // ActionID 1 = Wave
    }

    public override void OnPlayerExitRange()
    {
        // có thể thêm hành động khác nếu muốn, hoặc để trống
    }

    public override void InitializeNPCData()
    {
        npcType = NPCType.Companion;
        aiPersonality = "You are a helpful companion.";
    }

    public override void ProcessInteraction()
    {
        // ví dụ: làm động tác chỉ tay khi player bấm talk
        PlayAction(2); // ActionID 2 = Point / Talk gesture
    }

    protected override string GetDefaultResponse() => "Tôi sẽ đi cùng bạn!";

    public string GetHelpForTopic(string topic)
    {
        var helpTopic = helpTopics.Find(h => h.topicName.ToLower().Contains(topic.ToLower()));
        return helpTopic?.helpText ?? "Xin lỗi, tôi không có thông tin về chủ đề đó.";
    }

    [System.Serializable]
    public class GetHelpTopic
    {
        public string topicName;
        public string helpText;

        public GetHelpTopic(string topic, string text)
        {
            topicName = topic;
            helpText = text;
        }
    }

    #endregion

    public override string ProcessMessage(string message, string sender)
    {
        if (enableAIChat && !string.IsNullOrEmpty(aiPersonality))
        {
            GetAIResponse(message);
            return null; // câu trả lời sẽ được gửi qua event khi Dify trả về
        }
        return null;
    }
}