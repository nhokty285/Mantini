using Unity.Cinemachine; // ✅ THÊM  
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField][Range(0, 20)] float rotationSpeed = 10f;

    [Header("Camera Reference")]
    [SerializeField] Transform cameraTransform;
    [SerializeField] string cameraTag = "CameraTag";

    [Header("Animation")]
    [SerializeField] float minIdleTime = 3f;
    [SerializeField] float maxIdleTime = 6f;
    [SerializeField] int totalIdleAnimations = 6;
    private float currentWaitTime = 0f;
    private int lastPlayedIdle = 0; // Biến lưu idle vừa diễn xong

    [Header("Control State")]
    [SerializeField] private bool canMove = true;
    private int _lockCount = 0;

    // ✅ THÊM: Reference đến Cinemachine Input Controller
    [Header("Cinemachine Control")]
    [SerializeField] private CinemachineInputAxisController inputAxisController;
    // ✅ FIX: Cache TẤT CẢ instances trong scene để disable đồng loạt.
    // Lần đầu mở shop bị bug vì có thể chỉ disable 1 instance, instance khác vẫn active,
    // hoặc reference null do timing player spawn trước camera.
    private CinemachineInputAxisController[] _allInputControllers;

    // Input & cache
    Vector2 moveInput;
    [SerializeField] Animator anim;
    Rigidbody rb;
    float lastIdleTime;
    bool wasMoving = false;

    // Biến lưu trữ Input (Cầu nối giữa Update và FixedUpdate)
    private Vector3 _cachedInputDirection;

    private void Awake()
    {
        // ✅ FIX: Đảm bảo Instance được set ngay khi player spawn (qua GameplayPlayerSpawner).
        // Nếu không có dòng này, PlayerController.Instance == null và mọi câu lệnh
        // PlayerController.Instance?.SetCanMove(...) ở các script khác sẽ bị bỏ qua im lặng.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerController] Duplicate instance found, destroying new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        // ✅ Cleanup Instance nếu instance hiện tại bị destroy (đổi scene, reload, ...)
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        FindCameraByTag();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // ✅ Refresh cache tất cả Cinemachine input controllers
        RefreshInputControllersCache();
    }

    /// <summary>
    /// Tìm và cache TẤT CẢ CinemachineInputAxisController instances trong scene.
    /// Gọi từ Start() và mỗi lần SetCanMove khi cache rỗng, để bảo vệ trường hợp:
    /// - Camera spawn sau player (player được spawn động qua GameplayPlayerSpawner).
    /// - Có nhiều virtual camera với CinemachineInputAxisController khác nhau.
    /// - Một instance bị disable/destroy giữa các lần gọi.
    /// 
    /// Complexity: O(n) theo số GameObject — gọi rất ít, KHÔNG trong Update.
    /// </summary>
    private void RefreshInputControllersCache()
    {
        // FindObjectsInactive.Include để tìm cả những controller đang trên GO bị disable
        _allInputControllers = FindObjectsByType<CinemachineInputAxisController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        // Giữ inputAxisController legacy cho tương thích (lấy item đầu nếu có)
        if (inputAxisController == null && _allInputControllers.Length > 0)
        {
            inputAxisController = _allInputControllers[0];
        }

        Debug.Log($"[PlayerController] Refreshed input controllers cache. Count = {_allInputControllers.Length}");
    }

    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public static event System.Action<bool> OnMovementStateChanged;

    // ✅ Counter-based: nhiều panel có thể lock đồng thời, chỉ unlock khi tất cả đều close
    public void SetCanMove(bool canMove)
    {
        if (!canMove)
            _lockCount++;
        else
            _lockCount = Mathf.Max(0, _lockCount - 1);

        bool shouldMove = _lockCount == 0;
        this.canMove = shouldMove;

        // ✅ FIX BUG LẦN 1: Refresh cache mỗi lần gọi SetCanMove nếu cache trống.
        // Lần đầu Play Mode có thể player spawn trước camera → cache rỗng → camera không lock được.
        if (_allInputControllers == null || _allInputControllers.Length == 0)
        {
            RefreshInputControllersCache();
        }

        ApplyInputAxisState(shouldMove);

        if (shouldMove)
        {
            moveInput = Vector2.zero;
            if (anim != null) anim.SetBool("isMoving", false);
        }

        OnMovementStateChanged?.Invoke(shouldMove);
    }

    /// <summary>
    /// Bật/tắt TẤT CẢ CinemachineInputAxisController instances.
    /// Khi component disabled, Cinemachine sẽ ngừng đọc input → camera không xoay.
    /// </summary>
    private void ApplyInputAxisState(bool enabled)
    {
        if (_allInputControllers == null) return;

        int validCount = 0;
        foreach (var controller in _allInputControllers)
        {
            if (controller == null) continue; // skip destroyed instances
            controller.enabled = enabled;
            validCount++;
        }

        Debug.Log($"[PlayerController] CinemachineInputAxisController.enabled = {enabled} on {validCount} controllers (lockCount={_lockCount})");
    }

    void LateUpdate()
    {
        if (canMove && cameraTransform != null)
            _cachedInputDirection = CalculateCameraRelativeMovement();
        else
            _cachedInputDirection = Vector3.zero;

        float rawMagnitude = _cachedInputDirection.magnitude;
        bool isMoving = rawMagnitude > 0.01f;

        // ✅ Dùng giá trị binary: 0 (đứng) hoặc 1 (chạy full speed)
        // Thay vì dùng rawMagnitude vốn phụ thuộc joystick depth
        float animSpeed = isMoving ? 1f : 0f;
        anim.SetFloat("moveSpeed", animSpeed, 0.1f, Time.deltaTime);

        HandleIdleAnimations(isMoving);
    }

    void FixedUpdate()
    {
        // Lấy giá trị đã lưu từ Update ra dùng
        Vector3 moveDir = _cachedInputDirection;

        if (moveDir.sqrMagnitude <= 0.001f) return;

        moveDir.Normalize();

        // Logic di chuyển giữ nguyên như cũ (An toàn tuyệt đối)
        Quaternion targetRot = Quaternion.LookRotation(moveDir);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));

        Vector3 targetVelocity = new Vector3(moveDir.x * moveSpeed, rb.linearVelocity.y, moveDir.z * moveSpeed);

        // Tính lực cần thêm để đạt targetVelocity (ForceMode.VelocityChange bỏ qua mass)
        rb.AddForce(targetVelocity - rb.linearVelocity, ForceMode.VelocityChange);


    }

    // Tìm camera theo tag
    void FindCameraByTag()
    {
        GameObject cameraObject = GameObject.FindGameObjectWithTag(cameraTag);
        if (cameraObject != null)
        {
            cameraTransform = cameraObject.transform;
            Debug.Log($"[PlayerController] Found camera by tag '{cameraTag}': {cameraTransform.name}");
        }
    }

    // Tính toán hướng di chuyển dựa trên hướng camera
    Vector3 CalculateCameraRelativeMovement()
    {
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDir = (cameraForward * moveInput.y) + (cameraRight * moveInput.x);
        return moveDir;
    }

    // Quản lý animation idle với biến thể
    void HandleIdleAnimations(bool isMoving)
    {
        /*  anim.SetBool("isMoving", isMoving);

          if (!isMoving)
          {
              if (wasMoving)
              {
                  lastIdleTime = Time.time;
                  anim.SetInteger("idleState", 0);
              }
              else if (Time.time - lastIdleTime >= idleVariationTime)
              {
                  int currentIdle = anim.GetInteger("idleState");
                  int nextIdle = (currentIdle == 0) ? 1 : 0;
                  anim.SetInteger("idleState", nextIdle);
                  lastIdleTime = Time.time;
              }
          }

          wasMoving = isMoving;*/

        if (!isMoving)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool isPlayingSpecialIdle = anim.GetInteger("idleState") != 0;

            if (wasMoving)
            {
                ResetIdleTimer();
            }
            else if (isPlayingSpecialIdle)
            {
                // Logic Cách 1: Đang diễn thì check xem xong chưa
                if (stateInfo.normalizedTime >= 0.95f && !anim.IsInTransition(0))
                {
                    anim.SetInteger("idleState", 0);
                    ResetIdleTimer();
                }
            }
            else
            {
                // ĐANG CHỜ (IdleState == 0)
                if (Time.time - lastIdleTime >= currentWaitTime)
                {
                    // LOGIC RANDOM MỚI: Không trùng lặp
                    int randomIdle;

                    // Nếu chỉ có 1 animation thì không cần check trùng (tránh treo vòng lặp)
                    if (totalIdleAnimations <= 1)
                    {
                        randomIdle = 1;
                    }
                    else
                    {
                        // Random cho đến khi ra số KHÁC số vừa diễn
                        do
                        {
                            randomIdle = Random.Range(1, totalIdleAnimations + 1);
                        }
                        while (randomIdle == lastPlayedIdle);
                    }

                    // Cập nhật lại biến lưu trữ
                    lastPlayedIdle = randomIdle;

                    // Set Animator
                    anim.SetInteger("idleState", randomIdle);
                }
            }
        }
        else
        {
            anim.SetInteger("idleState", 0);
            ResetIdleTimer();
        }

        wasMoving = isMoving;

    }

    // Hàm phụ để reset thời gian chờ ngẫu nhiên
    void ResetIdleTimer()
    {
        lastIdleTime = Time.time;
        // anim.SetInteger("idleState", 0); -> Không cần set ở đây nữa vì logic trên đã set rồi
        currentWaitTime = Random.Range(minIdleTime, maxIdleTime);
    }

    
}

