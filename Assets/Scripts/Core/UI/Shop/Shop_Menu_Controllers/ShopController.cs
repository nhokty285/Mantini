using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel;

[System.Serializable]
public struct CarouselPosition
{
    public Vector3 position;
    public float scale;
    public float alpha;
    public bool isCenter;
    public int slotIndex; // 0=left, 1=center, 2=right

    public CarouselPosition(Vector3 pos, float scl, float alph, bool center, int slot)
    {
        position = pos;
        scale = scl;
        alpha = alph;
        isCenter = center;
        slotIndex = slot;
    }
}

public class ShopController : MonoBehaviour
{
    [Header("Multi Chat Integration")]
    [SerializeField] private MultiChatManager multiChatController;
    [SerializeField] private bool enableChatDuringShop = true;

    [Header("Shop System")]
    [SerializeField] public Button shopButton;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Transform shopItemsContainer;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private ShopData shopData;
    [SerializeField] private Button closeShopButton;
    [SerializeField] private TextMeshProUGUI shopHeaderText;
    [SerializeField] private ScrollRect shopScrollRect;
    [SerializeField] private RawImage playerImage;

    [Header("Product Detail System")]
    [SerializeField] private GameObject productDetailPanel;
    [SerializeField] private ProductDetailUI productDetailUI;

    [Header("Shopping Cart System")]
    [SerializeField] private Button cartButton;
    [SerializeField] private GameObject cartPanel;
    [SerializeField] private Sprite globalDefaultSprite;

    [Header("Fixed Position System")]
    [SerializeField] private bool useFixedPositions = true;

    [Header("Canvas Absolute Positions")]
    [SerializeField] private float leftPosition_3 = 251f;
    [SerializeField] private float centerPosition_5 = 421F;
    [SerializeField] private float rightPosition_4 = 590f;
    [SerializeField] private float leftPosition_1 = 151f;
    [SerializeField] private float rightPosition_2 = 690f;
    [SerializeField] private float positionY_first = 23f;
    [SerializeField] private float positionY_between = 94f;
    [SerializeField] private float positionY_Center = -130f;

    [SerializeField] private bool useCarouselMode = true;
    [SerializeField] private float carouselOffsetX = 300f;
    [SerializeField] private float centerScale = 1.2f;
    [SerializeField] private float sideScale = 0.8f;
    [SerializeField] private float sideAlpha = 0.6f;

    [Header("Center Item DOTween Animation")]
    [SerializeField] private bool enableCenterPunchAnimation = true;
    [SerializeField] private float centerPunchDuration = 0.45f;
    [SerializeField] private float centerPunchStrength = 0.18f;
    [SerializeField] private int centerPunchVibrato = 3;
    [SerializeField] private float centerPunchElasticity = 0.5f;

    [Header("External UI References (outside prefab)")]
    [SerializeField] private TextMeshProUGUI externalPriceText;

    [SerializeField] private RectTransform ParticipantsContainer;
    [SerializeField] private bool debugTouchArea = false;

    [Header("Swipe Control Settings")]
    [SerializeField] private bool enableSwipeControl = true;
    [SerializeField] private float minSwipeDistance = 40f;
    [SerializeField] private float maxSwipeTime = 0.5f;
    [SerializeField] private bool debugSwipe = false;

    [Header("Arrow Navigation Buttons")]
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;

    [Header("Dynamic Multi-Step Swipe")]
    [SerializeField] private float velocityStep1 = 800f;
    [SerializeField] private float velocityStep2 = 1600f;
    [SerializeField] private float velocityStep3 = 2400f;
    [SerializeField] private int maxSwipeSteps = 3;
    [SerializeField] private float stepAnimDuration = 0.28f;
    [SerializeField] private bool enableMomentumSwipe = true;

    [SerializeField] private CarouselIndicator carouselIndicator;

    [Header("Sound")]
    [SerializeField] private AudioClip openBGM;
    [SerializeField] private AudioClip openaAmbient;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private readonly Dictionary<string, CarouselPosition> _fixedPositions = new();
    private bool _positionsInitialized = false;

    private Vector3 _containerOriginalPos;
    private bool _isAnimating = false;
    private float _accumulatedDelta = 0f;

    private MainMenuViewModel MainMenuViewModel;
    private BaseNPC _currentInteractingNPC;
    private int _carouselCenterIndex = 0;
    private readonly List<ShopItem> _currentCarouselItems = new();

    // Swipe detection state
    private Vector2 _swipeStartPos;
    private Vector2 _swipeEndPos;
    private float _swipeStartTime;
    private bool _isSwipeActive = false;
    private bool _swipeProcessed = false;

    private int _lastCarouselIndex = -1;
    private readonly List<GameObject> _spawnedItems = new();

    // ═════════════════════════════════════════════════════════════════════════
    // INIT
    // ═════════════════════════════════════════════════════════════════════════

    public void Initialize(MainMenuViewModel viewModel)
    {
        this.MainMenuViewModel = viewModel;
        SetupEventListeners();
        SetupInitialState();
        _containerOriginalPos = shopItemsContainer.localPosition;
    }

    private void SetupEventListeners()
    {
        // Mantini convention: RemoveAllListeners trước AddListener
        if (shopButton != null)
        {
            shopButton.onClick.RemoveAllListeners();
            shopButton.onClick.AddListener(OnShopButtonClicked);
        }

        if (closeShopButton != null)
        {
            closeShopButton.onClick.RemoveAllListeners();
            closeShopButton.onClick.AddListener(OnCloseShopButtonClicked);
        }

        // Arrow navigation: di chuyển carousel 1 bước/lần nhấn (giống swipe nhưng cho người không muốn vuốt)
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.RemoveAllListeners();
            leftArrowButton.onClick.AddListener(OnLeftArrowClicked);
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.RemoveAllListeners();
            rightArrowButton.onClick.AddListener(OnRightArrowClicked);
        }
    }

    // Refactor: tách lambda thành method có tên — đỡ alloc closure, dễ debug
    private void OnShopButtonClicked()
    {
        MainMenuViewModel.IsDialogueVisible = false;
        MainMenuViewModel.OnShopClicked();

        if (multiChatController != null && enableChatDuringShop)
            multiChatController.OpenDialogWithShop();
    }

    private void OnCloseShopButtonClicked()
    {
        if (multiChatController != null)
            multiChatController.CloseCompanionChat();

        MainMenuViewModel.OnCloseShopClicked();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(openBGM);
            AudioManager.Instance.PlayAmbient(openaAmbient);
        }
    }

    // Nhấn mũi tên TRÁI → xem vật phẩm bên trái (về item trước đó)
    private void OnLeftArrowClicked()
    {
        if (!useCarouselMode) return;
        if (_currentCarouselItems == null || _currentCarouselItems.Count <= 1) return;
        if (_isAnimating) return;

        if (_carouselCenterIndex > 0)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXOneShot("Swipe");
            PreviousCarouselItem();
        }
        else
        {
            StartCoroutine(ShowBounceEffect(SwipeDirection.Right));
        }
    }

    // Nhấn mũi tên PHẢI → xem vật phẩm bên phải (sang item kế tiếp)
    private void OnRightArrowClicked()
    {
        if (!useCarouselMode) return;
        if (_currentCarouselItems == null || _currentCarouselItems.Count <= 1) return;
        if (_isAnimating) return;

        if (_carouselCenterIndex < _currentCarouselItems.Count - 1)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXOneShot("Swipe");
            NextCarouselItem();
        }
        else
        {
            StartCoroutine(ShowBounceEffect(SwipeDirection.Left));
        }
    }

    private void SetupInitialState()
    {
        if (shopButton != null) shopButton.gameObject.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (playerImage != null) playerImage.gameObject.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPDATE — swipe detection
    // ═════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (!useCarouselMode || !enableSwipeControl) return;
        if (_currentCarouselItems == null || _currentCarouselItems.Count <= 1) return;
        if (shopPanel == null || !shopPanel.activeInHierarchy) return;

        Vector2 inputPos = Vector2.zero;
        bool hasValidInput = false;

#if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0)
        {
            inputPos = Input.GetTouch(0).position;
            hasValidInput = true;
        }
#endif

        // CHỈ PROCESS SWIPE KHI TRONG VÙNG CAROUSEL
        bool cartActive = cartPanel != null && cartPanel.activeInHierarchy;
        bool detailActive = productDetailPanel != null && productDetailPanel.activeInHierarchy;

        if (hasValidInput && IsPointerInCarouselArea(inputPos) && !cartActive && !detailActive)
        {
            DetectSwipeInput();
        }
        else if (_isSwipeActive && !IsPointerInCarouselArea(inputPos))
        {
            // Cancel swipe nếu drag ra ngoài vùng
            CancelCurrentSwipe();
        }
    }

    private void CancelCurrentSwipe()
    {
#if UNITY_EDITOR
        if (debugSwipe) GameLog.Info("[ShopController] Swipe cancelled - moved outside carousel area");
#endif
        _isSwipeActive = false;
        _swipeProcessed = false;
    }

    private void DetectSwipeInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        DetectTouchSwipe();
#endif
    }

    private void DetectTouchSwipe()
    {
        if (Input.touchCount <= 0) return;

        Touch touch = Input.GetTouch(0);
        switch (touch.phase)
        {
            case TouchPhase.Began:
                StartSwipeDetection(touch.position);
                break;
            case TouchPhase.Moved:
                if (_isSwipeActive && !_swipeProcessed) ProcessSwipeMovement(touch.position);
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                EndSwipeDetection(touch.position);
                break;
        }
    }

    private void StartSwipeDetection(Vector2 position)
    {
        if (!IsPointerInCarouselArea(position)) return;

        _swipeStartPos = position;
        _swipeStartTime = Time.time;
        _isSwipeActive = true;
        _swipeProcessed = false;

#if UNITY_EDITOR
        if (debugSwipe) GameLog.Info($"[ShopController] Swipe started at: {position}");
#endif
    }

    private int CalculateSwipeSteps(float rawDeltaX)
    {
        float absDeltaX = Mathf.Abs(rawDeltaX);
        if (absDeltaX < minSwipeDistance) return 0;

        float swipeTime = Mathf.Max(Time.time - _swipeStartTime, 0.05f);
        float absVelocityX = absDeltaX / swipeTime;

        int steps;
        if (absVelocityX >= velocityStep3) steps = 3;
        else if (absVelocityX >= velocityStep2) steps = 2;
        else if (absVelocityX >= velocityStep1) steps = 1;
        else return 0;

        return Mathf.Clamp(steps, 1, maxSwipeSteps);
    }

    private void ProcessSwipeMovement(Vector2 currentPosition)
    {
        if (_swipeProcessed) return;
        if (_isAnimating && !enableMomentumSwipe) return;

        Vector2 swipeDelta = currentPosition - _swipeStartPos;
        float rawDeltaX = swipeDelta.x;
        _accumulatedDelta = rawDeltaX;

        int steps = CalculateSwipeSteps(rawDeltaX);
        if (steps <= 0) return;

        bool goLeft = rawDeltaX < 0;

#if UNITY_EDITOR
        if (debugSwipe)
        {
            float swipeTime = Mathf.Max(Time.time - _swipeStartTime, 0.05f);
            float absVelocityX = Mathf.Abs(rawDeltaX) / swipeTime;
            GameLog.Info($"[ShopController] Swipe commit | deltaX={rawDeltaX:F1} | time={swipeTime:F3} | velocityX={absVelocityX:F1} | steps={steps}");
        }
#endif

        _swipeProcessed = true;
        _accumulatedDelta = 0f;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFXOneShot("Swipe");

        StartCoroutine(ExecuteMultiStepSwipe(goLeft ? -1 : 1, steps));
    }

    private IEnumerator ExecuteMultiStepSwipe(int direction, int steps)
    {
        _isAnimating = true;

        for (int i = 0; i < steps; i++)
        {
            bool canContinue = direction < 0
                ? _carouselCenterIndex < _currentCarouselItems.Count - 1
                : _carouselCenterIndex > 0;

            if (!canContinue)
            {
                if (i == 0)
                    StartCoroutine(ShowBounceEffect(
                        direction < 0 ? SwipeDirection.Left : SwipeDirection.Right));
                break;
            }

            _carouselCenterIndex = direction < 0
                ? _carouselCenterIndex + 1
                : _carouselCenterIndex - 1;

            yield return StartCoroutine(AnimateCarouselTransition(direction, stepAnimDuration));

#if UNITY_ANDROID || UNITY_IOS
            if (SystemInfo.supportsVibration && i == 0)
                Handheld.Vibrate();
#endif
        }

        _isAnimating = false;
    }

    private void EndSwipeDetection(Vector2 position)
    {
        _swipeEndPos = position;
        float swipeTime = Time.time - _swipeStartTime;

#if UNITY_EDITOR
        if (debugSwipe)
        {
            Vector2 swipeDelta = _swipeEndPos - _swipeStartPos;
            float velocityX = Mathf.Abs(swipeDelta.x) / Mathf.Max(swipeTime, 0.05f);
            GameLog.Info($"[ShopController] Swipe ended | Delta={swipeDelta} | Distance={swipeDelta.magnitude:F1} | Time={swipeTime:F3} | VelocityX={velocityX:F1}");
        }
#endif

        _isSwipeActive = false;
        _swipeProcessed = false;
    }

    private bool IsPointerInCarouselArea(Vector2 screenPos)
    {
        if (ParticipantsContainer == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(ParticipantsContainer, screenPos, null);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // NPC INTERACTION
    // ═════════════════════════════════════════════════════════════════════════

    public void SetNPCInteraction(bool isNear, string npcName, ShopData npcShopData = null, BaseNPC npc = null)
    {
        if (isNear && npc == null)
        {
            Debug.LogError($"[ShopController] CRITICAL: NPC is NULL but isNear=true for {npcName}");
            return;
        }

        MainMenuViewModel.PendingDialogue = isNear;
        _currentInteractingNPC = npc;

        if (isNear)
        {
            MainMenuViewModel.CurrentNPCName = npcName;
            if (npcShopData != null)
            {
                MainMenuViewModel.CurrentShopData = npcShopData;
                UpdateShopHeaderPreview();
#if UNITY_EDITOR
                GameLog.Info($"[ShopController] Loaded shop data: {npcShopData.shopName} for NPC: {npcName}");
#endif
            }
            else
            {
                GameLog.Warn($"[ShopController] NPC {npcName} không có ShopData!");
            }
        }
        else
        {
            MainMenuViewModel.CurrentShopData = null;
            MainMenuViewModel.CurrentNPCName = null;
            _currentInteractingNPC = null;
#if UNITY_EDITOR
            GameLog.Info("[ShopController] Cleared shop data when leaving NPC");
#endif
        }

        var multiChatManager = MainMenuView.Instance?.GetComponentInChildren<MultiChatManager>();

        if (multiChatManager != null && npc != null)
        {
            if (isNear)
            {
                if (npc is IChatParticipant chatParticipant)
                {
                    multiChatManager.AddParticipant(chatParticipant);
#if UNITY_EDITOR
                    GameLog.Info($"[ShopController] Added {npc.name} as IChatParticipant");
#endif
                }
                else
                {
                    Debug.LogError($"[ShopController] {npc.name} does not implement IChatParticipant!");
                }
            }
            else
            {
                if (npc is IChatParticipant chatParticipant)
                    multiChatManager.RemoveParticipant(chatParticipant);
            }
        }
        else if (multiChatManager == null)
        {
            Debug.LogError("[ShopController] MultiChatManager NOT FOUND!");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // POPULATE / CAROUSEL DISPLAY
    // ═════════════════════════════════════════════════════════════════════════

    private void PopulateShopItems()
    {
        ClearShopItems();
        if (MainMenuViewModel.CurrentShopData == null)
        {
            Debug.LogError("[ShopController] CurrentShopData is null!");
            return;
        }

        _currentCarouselItems.Clear();
        _currentCarouselItems.AddRange(MainMenuViewModel.CurrentShopData.ItemsDictionary.Values);
        _carouselCenterIndex = 0;

#if UNITY_EDITOR
        GameLog.Info($"[ShopController] Displaying shop: {MainMenuViewModel.CurrentShopData.shopName} with {_currentCarouselItems.Count} items");
#endif
        UpdateShopHeader();

        if (useCarouselMode)
            UpdateCarouselDisplay();
        else
            StartCoroutine(LoadItemsBatched());

        UpdateArrowButtonsState();
    }

    private void PreviousCarouselItem()
    {
        if (_carouselCenterIndex > 0)
        {
            _carouselCenterIndex--;
            StartCoroutine(AnimateCarouselTransition(-1, 0.3f));
        }
    }

    private void NextCarouselItem()
    {
        if (_carouselCenterIndex < _currentCarouselItems.Count - 1)
        {
            _carouselCenterIndex++;
            StartCoroutine(AnimateCarouselTransition(1, 0.3f));
        }
    }

    // Bật/tắt interactable của 2 nút mũi tên tùy theo vị trí hiện tại của carousel.
    // O(1) — chỉ chạy khi index đổi hoặc khi populate, không nằm trong Update.
    private void UpdateArrowButtonsState()
    {
        int total = _currentCarouselItems != null ? _currentCarouselItems.Count : 0;
        bool hasMultiple = useCarouselMode && total > 1;

        if (leftArrowButton != null)
            leftArrowButton.interactable = hasMultiple && _carouselCenterIndex > 0;

        if (rightArrowButton != null)
            rightArrowButton.interactable = hasMultiple && _carouselCenterIndex < total - 1;
    }

    private void InitializeFixedPositions()
    {
        if (_positionsInitialized) return;

        _fixedPositions.Clear();

        // Single Center (Slot 4 - Topmost)
        _fixedPositions["single_center"] = new CarouselPosition(
            new Vector3(centerPosition_5, positionY_Center, 0f),
            centerScale, 1f, true, 4
        );

        // PENTA LAYOUT — Slot order theo lớp render
        // Slot 0: Far Left (Lớp dưới cùng)
        _fixedPositions["penta_left_far"] = new CarouselPosition(
            new Vector3(leftPosition_1, positionY_first, 0f),
            sideScale * 0.8f, 1f, false, 0
        );

        // Slot 1: Far Right (Lớp dưới cùng)
        _fixedPositions["penta_right_far"] = new CarouselPosition(
            new Vector3(rightPosition_2, positionY_first, 0f),
            sideScale * 0.8f, 1f, false, 1
        );

        // Slot 2: Near Left (Lớp giữa)
        _fixedPositions["penta_left_near"] = new CarouselPosition(
            new Vector3(leftPosition_3, positionY_between, 0f),
            sideScale, 1f, false, 2
        );

        // Slot 3: Near Right (Lớp giữa)
        _fixedPositions["penta_right_near"] = new CarouselPosition(
            new Vector3(rightPosition_4, positionY_between, 0f),
            sideScale, 1f, false, 3
        );

        // Slot 4: Center (Lớp trên cùng - Render cuối cùng)
        _fixedPositions["penta_center"] = new CarouselPosition(
            new Vector3(centerPosition_5, positionY_Center, 0f),
            centerScale, 1f, true, 4
        );

        _positionsInitialized = true;
#if UNITY_EDITOR
        GameLog.Info("[ShopController] Fixed absolute positions initialized (Center on TOP)");
#endif
    }

    private static string GetPositionKey(int totalItems, int itemIndex, int centerIndex)
    {
        if (totalItems == 1) return "single_center";

        int diff = itemIndex - centerIndex;
        return diff switch
        {
            0  => "penta_center",
            -1 => "penta_left_near",
            1  => "penta_right_near",
            -2 => "penta_left_far",
            2  => "penta_right_far",
            _  => ""
        };
    }

    private void EnsureItemCount(int count)
    {
        const int maxSlots = 5;
        while (_spawnedItems.Count < Mathf.Min(count, maxSlots))
        {
            var go = Instantiate(shopItemPrefab, shopItemsContainer);
            _spawnedItems.Add(go);
        }

        for (int i = 0; i < _spawnedItems.Count; i++)
            _spawnedItems[i].SetActive(i < Mathf.Min(count, maxSlots));
    }

    // Cập nhật dữ liệu + vị trí thay vì Clear/Spawn
    private void UpdateCarouselDisplay()
    {
        if (_currentCarouselItems == null || _currentCarouselItems.Count == 0) return;

        _carouselCenterIndex = Mathf.Clamp(_carouselCenterIndex, 0, _currentCarouselItems.Count - 1);
        int totalItems = _currentCarouselItems.Count;

        // Đảm bảo có đủ 5 slot vật lý
        int requiredSlots = (totalItems >= 1) ? 5 : 0;
        EnsureItemCount(requiredSlots);

        // Tắt hết trước
        for (int i = 0; i < _spawnedItems.Count; i++)
            _spawnedItems[i].SetActive(false);

        // Tính toán index dữ liệu
        int farLeftIndex = _carouselCenterIndex - 2;
        int nearLeftIndex = _carouselCenterIndex - 1;
        int nearRightIndex = _carouselCenterIndex + 1;
        int farRightIndex = _carouselCenterIndex + 2;

        // CẤP PHÁT SLOT VẬT LÝ THEO THỨ TỰ RENDER (Dưới lên Trên)

        // 1. Lớp xa nhất (Far) -> Slot 0 & 1
        if (farLeftIndex >= 0 && totalItems >= 5) SetupSlot(0, farLeftIndex);
        if (farRightIndex < totalItems && totalItems >= 5) SetupSlot(1, farRightIndex);

        // 2. Lớp gần (Near) -> Slot 2 & 3
        if (nearLeftIndex >= 0 && totalItems >= 2) SetupSlot(2, nearLeftIndex);
        if (nearRightIndex < totalItems && totalItems >= 2) SetupSlot(3, nearRightIndex);

        // 3. Lớp Center (Topmost) -> Slot 4 — luôn cuối cùng để đè lên item khác
        if (totalItems >= 1) SetupSlot(4, _carouselCenterIndex);

        // ✅ FIX: Chỉ update dots khi index THAY ĐỔI (trước đây gọi 2 lần — 1 unconditional + 1 conditional)
        if (_carouselCenterIndex != _lastCarouselIndex)
        {
            _lastCarouselIndex = _carouselCenterIndex;
            carouselIndicator?.UpdateDots(_carouselCenterIndex, _currentCarouselItems.Count);
            UpdateArrowButtonsState();
        }
    }

    private void SetupSlot(int slot, int itemIndex)
    {
        if (slot < 0 || slot >= _spawnedItems.Count) return;

        var go = _spawnedItems[slot];
        var ui = go.GetComponent<ShopItemUI>();
        var item = _currentCarouselItems[itemIndex];

        go.SetActive(true);

        if (globalDefaultSprite != null)
            ui.SetDefaultSprite(globalDefaultSprite);

        // Inject TRƯỚC ui.Setup() — chỉ inject cho center slot (slot 4)
        if (slot == 4 && externalPriceText != null)
            ui.SetExternalPriceText(externalPriceText);

        ui.Setup(item, () => OnCarouselItemClicked(item, itemIndex));

        if (item.GetAPIData() != null)
            ui.SetAPIData(item.GetAPIData());

        ApplyFixedPosition(go, _currentCarouselItems.Count, itemIndex, _carouselCenterIndex);
    }

    private void ApplyFixedPosition(GameObject go, int totalItems, int itemIndex, int centerIndex)
    {
        InitializeFixedPositions();
        string key = GetPositionKey(totalItems, itemIndex, centerIndex);
        if (!_fixedPositions.TryGetValue(key, out var fp)) return;

        go.transform.DOKill(complete: false);

        var rt = go.transform as RectTransform;
        if (rt != null)
            rt.anchoredPosition = new Vector2(fp.position.x, fp.position.y);

        if (!fp.isCenter)
            go.transform.localScale = Vector3.one * fp.scale;

        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.alpha = 1f; // không dùng alpha để dim nữa

        var ui = go.GetComponent<ShopItemUI>();
        if (ui != null)
        {
            ui.SetCarouselMode(fp.isCenter);
            ui.ApplyCarouselBackgroundState(fp.isCenter);
        }

        SetRaycastTarget(go, fp.isCenter);

        if (fp.isCenter)
        {
            AddCenterItemEffects(go);
            PlayCenterPunchAnimation(go);
        }
    }

    private void SetRaycastTarget(GameObject itemGameObject, bool isCenter)
    {
        Button button = itemGameObject.GetComponentInChildren<Button>();
        if (button == null)
        {
#if UNITY_EDITOR
            GameLog.Warn($"[ShopController] No Button component found in children of {itemGameObject.name}");
#endif
            return;
        }

        // raycastTarget là property của Graphic, không phải Button
        Graphic graphic = button.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = isCenter;

        CanvasGroup canvasGroup = itemGameObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = isCenter;

#if UNITY_EDITOR
        if (debugSwipe)
            GameLog.Info($"[ShopController] Raycast target set to {isCenter} for {itemGameObject.name}");
#endif
    }

    private void AddCenterItemEffects(GameObject centerItem)
    {
        // Cache outline trên center item
        var outline = centerItem.GetComponent<Outline>();
        if (outline == null)
        {
            outline = centerItem.AddComponent<Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(2f, 2f);
        }
        outline.enabled = true;
    }

    private void PlayCenterPunchAnimation(GameObject centerItem)
    {
        if (!enableCenterPunchAnimation || centerItem == null) return;

        Transform t = centerItem.transform;
        t.DOKill(complete: false);

        // Force set lại đúng scale của penta_center TRƯỚC KHI punch
        float targetScale = _fixedPositions.TryGetValue("penta_center", out var cp)
            ? cp.scale
            : centerScale;
        t.localScale = Vector3.one * targetScale;

        t.DOPunchScale(
            punch: Vector3.one * centerPunchStrength,
            duration: centerPunchDuration,
            vibrato: centerPunchVibrato,
            elasticity: centerPunchElasticity
        )
        .SetId("center_punch_" + centerItem.GetInstanceID())
        .SetUpdate(true);  // chạy kể cả khi Time.timeScale = 0
    }

    private void OnCarouselItemClicked(ShopItem shopItem, int itemIndex)
    {
        if (itemIndex == _carouselCenterIndex)
        {
            // Center item clicked - show detail
            MainMenuViewModel.OnBuyItemClicked(shopItem.itemID);
        }
        else
        {
            // Side item clicked - move to center
            _carouselCenterIndex = itemIndex;
            StartCoroutine(AnimateCarouselTransition(1, 0.3f));
        }
    }

    private IEnumerator AnimateCarouselTransition(int direction, float duration)
    {
        // 1. Reset container về origin TRƯỚC (tránh drift tích lũy)
        shopItemsContainer.DOKill(complete: false);
        shopItemsContainer.localPosition = _containerOriginalPos;

        // 2. Snap items về vị trí đúng
        UpdateCarouselDisplay();

        // 3. Slide nhẹ: di chuyển từ offset → 0
        float offsetX = direction * -30f;
        shopItemsContainer.localPosition = new Vector3(offsetX, 0f, 0f);
        shopItemsContainer
            .DOLocalMoveX(0f, duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        yield return new WaitForSeconds(duration);

        shopItemsContainer.localPosition = Vector3.zero;
    }

    private enum SwipeDirection { Left, Right }

    private IEnumerator ShowBounceEffect(SwipeDirection direction)
    {
        const float bounceDistance = 20f;
        const float duration = 0.3f;

        Vector3 originalPos = shopItemsContainer.localPosition;
        Vector3 bouncePos = originalPos + (direction == SwipeDirection.Left
            ? Vector3.left * bounceDistance
            : Vector3.right * bounceDistance);

        // Bounce out
        yield return StartCoroutine(AnimatePosition(originalPos, bouncePos, duration * 0.4f));
        // Bounce back
        yield return StartCoroutine(AnimatePosition(bouncePos, originalPos, duration * 0.6f));
    }

    private IEnumerator AnimatePosition(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            shopItemsContainer.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }
        shopItemsContainer.localPosition = to;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GRID MODE (non-carousel fallback)
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator LoadItemsBatched()
    {
        var items = MainMenuViewModel.CurrentShopData.ItemsDictionary.Values;
        int count = 0;

        foreach (var shopItem in items)
        {
            var itemUI = Instantiate(shopItemPrefab, shopItemsContainer);
            var shopItemUI = itemUI.GetComponent<ShopItemUI>();

            if (globalDefaultSprite != null)
                shopItemUI.SetDefaultSprite(globalDefaultSprite);

            shopItemUI.Setup(shopItem, () => MainMenuViewModel.OnBuyItemClicked(shopItem.itemID));

            if (shopItem.GetAPIData() != null)
                shopItemUI.SetAPIData(shopItem.GetAPIData());

            count++;
            if (count % 3 == 0) yield return null;
        }

        if (shopScrollRect != null)
            StartCoroutine(UpdateScrollPositionDelayed());
    }

    private IEnumerator UpdateScrollPositionDelayed()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(shopItemsContainer.GetComponent<RectTransform>());
        yield return null;

        if (shopScrollRect != null)
        {
            shopScrollRect.verticalNormalizedPosition = 1f;
#if UNITY_EDITOR
            var contentRect = shopItemsContainer.GetComponent<RectTransform>();
            var viewportRect = shopScrollRect.viewport;
            GameLog.Info($"[ShopController] Content height: {contentRect.sizeDelta.y}");
            GameLog.Info($"[ShopController] Viewport height: {viewportRect.rect.height}");
#endif
        }
        else
        {
            Debug.LogError("[ShopController] shopScrollRect is null in UpdateScrollPositionDelayed!");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HEADER
    // ═════════════════════════════════════════════════════════════════════════

    private void UpdateShopHeaderPreview()
    {
        if (shopHeaderText != null && MainMenuViewModel.CurrentShopData != null)
        {
            shopHeaderText.text = $"{MainMenuViewModel.CurrentShopData.shopName}";
#if UNITY_EDITOR
            GameLog.Info($"[ShopController] Preview updated shop header to: {MainMenuViewModel.CurrentShopData.shopName}");
#endif
        }
    }

    private void UpdateShopHeader()
    {
        if (shopHeaderText != null && MainMenuViewModel.CurrentShopData != null)
            shopHeaderText.text = $"{MainMenuViewModel.CurrentShopData.shopName}";
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CLEAR — KHÔNG Destroy children, chỉ deactivate (reuse spawnedItems pool)
    // ═════════════════════════════════════════════════════════════════════════

    private void ClearShopItems()
    {
        shopItemsContainer.DOKill(complete: false);
        shopItemsContainer.localPosition = _containerOriginalPos;

        foreach (var go in _spawnedItems)
        {
            if (go == null) continue;
            go.transform.DOKill(complete: false);

            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = false;
            }

            go.SetActive(false);
        }

        if (MainMenuViewModel.CurrentShopData != null)
            MainMenuViewModel.CurrentShopData.ClearCache();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═════════════════════════════════════════════════════════════════════════

    public void Close_BT_Shop()
    {
        if (shopButton != null) shopButton.gameObject.SetActive(false);
    }

    public void ToggleDisplayMode()
    {
        useCarouselMode = !useCarouselMode;
        _carouselCenterIndex = 0;
        PopulateShopItems();
    }

    public void ToggleSwipeControl()
    {
        enableSwipeControl = !enableSwipeControl;
#if UNITY_EDITOR
        GameLog.Info($"[ShopController] Swipe control: {(enableSwipeControl ? "Enabled" : "Disabled")}");
#endif
    }

    public void OnViewModelChanged(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(MainMenuViewModel.PendingDialogue):
                shopButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);
                break;

            case nameof(MainMenuViewModel.IsShopVisible):
                shopPanel.SetActive(MainMenuViewModel.IsShopVisible);
                PlayerController.Instance?.SetCanMove(!MainMenuViewModel.IsShopVisible);
                closeShopButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (playerImage != null)
                    playerImage.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (leftArrowButton != null)
                    leftArrowButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);
                if (rightArrowButton != null)
                    rightArrowButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (MainMenuViewModel.IsShopVisible)
                {
                    UpdateShopHeader();
                    PopulateShopItems();
                    Close_BT_Shop();
                }
                else
                {
                    StopAllCoroutines();
                    _isSwipeActive = false;
                    _swipeProcessed = false;
                    ClearShopItems();
                    Close_BT_Shop();
                    if (shopHeaderText != null) shopHeaderText.text = " ";
                }
                break;
        }
    }

#if UNITY_EDITOR
    // Refactor: bọc OnGUI trong UNITY_EDITOR — debug-only, không cần ship build
    private void OnGUI()
    {
        if (!debugSwipe || !useCarouselMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Carousel Index: {_carouselCenterIndex}/{_currentCarouselItems.Count - 1}");
        GUILayout.Label($"Swipe Active: {_isSwipeActive}");
        GUILayout.Label($"Swipe Processed: {_swipeProcessed}");

        if (_isSwipeActive)
        {
            Vector2 currentPos = Input.mousePosition;
            Vector2 delta = currentPos - _swipeStartPos;
            GUILayout.Label($"Swipe Delta: {delta}");
            GUILayout.Label($"Swipe Distance: {delta.magnitude:F1}");
        }

        if (GUILayout.Button("Toggle Swipe Control")) ToggleSwipeControl();
        if (GUILayout.Button("Toggle Display Mode")) ToggleDisplayMode();

        GUILayout.EndArea();
    }
#endif

    // Callback từ ChatMessageUI khi bấm vào product link
    public void OnProductLinkCallback(string productID)
    {
        TutorialGamePlay.Instance?.OnPlayerTappedItem();
#if UNITY_EDITOR
        GameLog.Info($"[ShopController] Received request to open product: {productID}");
#endif

        if (MainMenuViewModel == null || MainMenuViewModel.CurrentShopData == null) return;

        if (!MainMenuViewModel.CurrentShopData.ItemsDictionary.TryGetValue(productID, out var shopItem))
        {
            GameLog.Warn($"[ShopController] Product ID {productID} not found in current shop data.");
            return;
        }

        if (productDetailUI == null)
        {
            Debug.LogError("[ShopController] ProductDetailUI reference is missing!");
            return;
        }

        // Lấy customId từ APIData nếu có, fallback dùng itemID
        string customId = shopItem.GetAPIData()?.customId ?? shopItem.itemID;
#if UNITY_EDITOR
        GameLog.Info($"[ShopController] Opening detail for CustomID: {customId}");
#endif
        productDetailUI.ShowUnpaidProductDetail(customId);
    }
}