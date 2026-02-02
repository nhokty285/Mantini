/*using UnityEngine;
using System.Collections.Generic;

public class CompanionNPC : BaseNPC
{
    [Header("Companion Specific")]
    [SerializeField] private float followDistance = 2f;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float smoothTime = 0.3f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private bool shouldFollowPlayer = true;

    [Header("Game Help Data")]
    [SerializeField] private List<GetHelpTopic> helpTopics = new();

    private bool isFollowing = false;
    private Vector3 currentVelocity;

    void Start()
    {
        InitializeNPCData();
        AutoFindPlayer();
    }

    private void AutoFindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                Debug.Log($"[{npcName}] Auto-locked to player");
            }
        }

        if (shouldFollowPlayer && playerTransform != null)
        {
            StartFollowing();
        }
    }

    private void FixedUpdate()
    {
        if (!isFollowing || playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer >= followDistance)
        {
            FollowPlayer();
        }
        else
        {
            StayIdle();
        }
    }

    private void FollowPlayer()
    {
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        Vector3 targetPos = playerTransform.position - directionToPlayer * followDistance;

        // Smooth movement
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref currentVelocity,
            smoothTime,
            followSpeed
        );

        // Smooth rotation
        Vector3 lookPos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        Quaternion targetRotation = Quaternion.LookRotation(lookPos - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        UpdateAnimation();
    }

    private void StayIdle()
    {
        currentVelocity = Vector3.zero;

        if (npcAnimator != null)
        {
            //npcAnimator.SetFloat("moveSpeed", 0f);
            npcAnimator.SetBool("isMoving", false);
        }
    }

    private void UpdateAnimation()
    {
        if (npcAnimator == null) return;

        float speed = currentVelocity.magnitude;
        //npcAnimator.SetFloat("moveSpeed", speed);
        npcAnimator.SetBool("isMoving", speed > 0.1f);
    }

    public void StartFollowing()
    {
        isFollowing = true;
    }

    public void StopFollowing()
    {
        isFollowing = false;
        currentVelocity = Vector3.zero;

        if (npcAnimator != null)
        {
            //npcAnimator.SetFloat("moveSpeed", 0f);
            npcAnimator.SetBool("isMoving", false);
        }
    }

    public override void OnPlayerEnterRange()
    {
        Debug.Log($"[{npcName}] Player in interaction range");
        // Chỉ dùng cho UI interaction, không control follow
    }

    public override void OnPlayerExitRange()
    {
        Debug.Log($"[{npcName}] Player out of interaction range");
        // Follow vẫn tiếp tục
    }

    public override void InitializeNPCData()
    {
        npcType = NPCType.Companion;
        aiPersonality = "You are a helpful game companion.";
        LoadGameHelpData();
    }

    public override void ProcessInteraction()
    {
        Debug.Log($"Talking to {npcName}");
    }

    protected override string GetDefaultResponse()
    {
        string[] responses = {
            "Xin chào! Tôi có thể giúp gì?",
            "Bạn cần hướng dẫn không?",
            "Tôi ở đây để giúp bạn!"
        };
        return responses[Random.Range(0, responses.Length)];
    }

    private void LoadGameHelpData()
    {
        helpTopics = new List<GetHelpTopic>
        {
            new GetHelpTopic("movement", "WASD để di chuyển"),
            new GetHelpTopic("shop", "Talk với vendor để mở shop")
        };
    }

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
}
*/

using UnityEngine;
using System.Collections.Generic;

public class CompanionNPC : BaseNPC, IChatParticipant
{
    [Header("Companion Movement")]
    [SerializeField] private float followDistance = 2f;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private bool shouldFollowPlayer = true;

    [Header("Idle Variations")]
    [SerializeField] private float minIdleTime = 3f;
    [SerializeField] private float maxIdleTime = 6f;
    [SerializeField] private int totalIdleEmotes = 3;   // số emote idle trong sub-machine (nếu có)

    // Animator hashes
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
    void Start()
    {
        InitializeNPCData();
        AutoFindPlayer();
        ResetIdleTimer();
        NameplateManager.Instance.Register(this.transform, npcName);

    }

    void FixedUpdate()
    {
        if (!shouldFollowPlayer || playerTransform == null)
        {
            UpdateAnimationSpeed(0f);
            HandleIdleEmotes(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool shouldMove = distance > followDistance;

        if (shouldMove)
        {
            FollowPlayer();
        }
        else
        {
            // đứng yên
            currentVelocity = Vector3.zero;
        }

        float planarSpeed = currentVelocity.magnitude;
        UpdateAnimationSpeed(planarSpeed);
        HandleIdleEmotes(planarSpeed <= 0.05f);
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
        Vector3 newPos = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref currentVelocity,
            smoothTime,
            followSpeed
        );
        transform.position = newPos;

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
        npcAnimator.SetFloat(speedHash, speed, 0.1f, Time.deltaTime);
    }

    private void ResetIdleTimer()
    {
        lastIdleTime = Time.time;
        currentWaitTime = Random.Range(minIdleTime, maxIdleTime);
    }

    private void HandleIdleEmotes(bool isIdle)
    {
        if (npcAnimator == null || totalIdleEmotes <= 0) return;

        // 1. KHI ĐANG DI CHUYỂN (Yêu cầu: Tắt trigger, ID=0, Dừng sub-machine)
        if (!isIdle)
        {
            // Nếu đang có cờ hiệu diễn hoặc trigger đang chờ -> Dọn dẹp ngay
            if (isPlayingIdleEmote || npcAnimator.GetInteger(emoteIdHash) != 0)
            {
                // Tắt Trigger ngay lập tức
                npcAnimator.ResetTrigger(playEmoteHash);

                // Đưa ID về 0 để Animator không còn lý do ở lại Sub-machine
                npcAnimator.SetInteger(emoteIdHash, 0);

                // Xóa cờ trạng thái
                isPlayingIdleEmote = false;
            }

            ResetIdleTimer(); // Reset bộ đếm để lần sau đứng lại phải chờ từ đầu
            return;
        }

        // 2. KHI ĐANG ĐỨNG YÊN
        // ... (Phần logic random và check animation giữ nguyên như cũ) ...
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
        {
            emoteId = Random.Range(1, totalIdleEmotes + 1);
        }
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

        // tạm dừng follow nếu cần
        // isFollowing = false;

        npcAnimator.SetInteger(actionIdHash, actionId);
        npcAnimator.SetTrigger(doActionHash);

        // sau khi animation trong sub-machine Actions chơi xong
        // Animator (transition Has Exit Time) sẽ tự trả về Locomotion
    }

    public void PlayEmote(int emoteId)
    {
        if (npcAnimator == null) return;

        // Set ID & Trigger
        npcAnimator.SetInteger(emoteIdHash, emoteId);
        npcAnimator.SetTrigger(playEmoteHash);

        // Optional: Reset ID về 0 ngay sau 1 frame để nó không bị "dính"
        StartCoroutine(ResetEmoteIDAfterFrame());
    }

    private System.Collections.IEnumerator ResetEmoteIDAfterFrame()
    {
        yield return null; // Chờ 1 frame
        npcAnimator.SetInteger(emoteIdHash, 0);
    }
    #endregion

    #region BaseNPC overrides

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
        return ChatParticipantType.Companion;
    }

    public bool IsActive()
    {
        return gameObject.activeInHierarchy; // Hoặc return isPlayerNearby;
    }

    public void OnJoinChat()
    {
        Debug.Log($"{npcName} joined chat");
    }

    public void OnLeaveChat()
    {
        Debug.Log($"{npcName} left chat");
    }


    public override void OnPlayerEnterRange()
    {
        // ví dụ: vẫy tay khi player lại gần
        PlayAction(1); // ActionID 1 = Wave (tùy bạn setup trong Animator)
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

    protected override string GetDefaultResponse()
    {
        return "Tôi sẽ đi cùng bạn!";
    }
    [Header("Game Help Data")]
    [SerializeField] private List<GetHelpTopic> helpTopics = new();
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

    // ✅ IMPLEMENT METHOD BẮT BUỘC
    public override string ProcessMessage(string message, string sender)
    {
        Debug.Log($"[{GetParticipantName()}] Processing message from {sender}: '{message}'");

        // TODO: Gọi API OpenAI ở đây (code bạn đã có trong GetAIResponse)
        // Tạm thời return mock response để test
        return $"Xin chào! Tôi là {GetParticipantName()}. Bạn vừa nói: {message}";
    }
}
