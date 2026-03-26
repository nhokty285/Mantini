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
        Instance = this;
        Application.targetFrameRate = 120;
    }
    void Start()
    {
        FindCameraByTag();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // ✅ Tự động tìm InputAxisController nếu chưa assign
        if (inputAxisController == null)
        {
            inputAxisController = FindFirstObjectByType<CinemachineInputAxisController>();

            if (inputAxisController != null)
            {
                Debug.Log($"[PlayerController] Found InputAxisController: {inputAxisController.gameObject.name}");
            }
        }
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

        if (inputAxisController != null)
        {
            inputAxisController.enabled = shouldMove;
            Debug.Log($"[PlayerController] InputAxisController.enabled = {shouldMove} (lockCount={_lockCount})");
        }

        if (shouldMove)
        {
            moveInput = Vector2.zero;
            anim.SetBool("isMoving", false);
        }

        OnMovementStateChanged?.Invoke(shouldMove);
    }

    /*  void FixedUpdate()
      {
          // Vẫn tìm camera (trường hợp camera động)
          if (cameraTransform == null)
              FindCameraByTag();

          // Kiểm tra canMove trước khi di chuyển
          Vector3 moveDir = Vector3.zero;

          if (canMove && cameraTransform != null)
          {
              moveDir = CalculateCameraRelativeMovement();
          }

          float currentSpeed = moveDir.magnitude;
          anim.SetFloat("moveSpeed", currentSpeed, 0.1f, Time.fixedDeltaTime); // DampTime 0.1f giúp blend mượt mà

          bool isMoving = currentSpeed > 0.01f;
          HandleIdleAnimations(isMoving);

          if (!isMoving) return;

          moveDir.Normalize();

          // Di chuyển chỉ khi canMove = true
          Quaternion targetRot = Quaternion.LookRotation(moveDir);
          rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));

          Vector3 newPosition = rb.position + moveDir * moveSpeed * Time.fixedDeltaTime;
          rb.MovePosition(newPosition);
      }*/

    void Update()
    {
        // Tính toán hướng nhưng CHƯA di chuyển nhân vật
        if (canMove && cameraTransform != null)
        {
            _cachedInputDirection = CalculateCameraRelativeMovement();
        }
        else
        {
            _cachedInputDirection = Vector3.zero;
        }

        // Xử lý Animation ở đây để hình ảnh phản hồi ngay lập tức với ngón tay
        // Người chơi thấy nhân vật bắt đầu chạy NGAY, dù vật lý thực tế chưa chạy
        float inputSpeed = _cachedInputDirection.magnitude;
        anim.SetFloat("moveSpeed", inputSpeed, 0.1f, Time.deltaTime);
        HandleIdleAnimations(inputSpeed > 0.01f);
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

        Vector3 newPosition = rb.position + moveDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);


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

