using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel;
using static System.Net.Mime.MediaTypeNames;


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
    [Header("✅ NEW: Multi Chat Integration")]
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

    [Header("✅ NEW: Fixed Position System")]
    [SerializeField] private bool useFixedPositions = true;

    [Header("✅ Canvas Absolute Positions")]
    [SerializeField] private float leftPosition_3 = 251f;      // Vị trí 1
    [SerializeField] private float centerPosition_5 = 421F;   // Vị trí 2  
    [SerializeField] private float rightPosition_4 = 590f;    // Vị trí 3
    [SerializeField] private float leftPosition_1 = 151f;
    [SerializeField] private float rightPosition_2 = 690f;
    [SerializeField] private float positionY = 94f;
    [SerializeField] private float positionY_Center = -130f;

    // Cache các vị trí cố định
    private readonly Dictionary<string, CarouselPosition> fixedPositions = new();
    private bool positionsInitialized = false;

    [SerializeField] private bool useCarouselMode = true;
    [SerializeField] private float carouselOffsetX = 300f;
    [SerializeField] private float centerScale = 1.2f;
    [SerializeField] private float sideScale = 0.8f;
    [SerializeField] private float sideAlpha = 0.6f;
    [SerializeField] private RectTransform ParticipantsContainer;
    [SerializeField] private bool debugTouchArea = false;

    [Header("✅ NEW: Swipe Control Settings")]
    [SerializeField] private bool enableSwipeControl = true;
    [SerializeField] private float minSwipeDistance = 50f;
    [SerializeField] private float maxSwipeTime = 0.5f;
    [SerializeField] private bool debugSwipe = false;

    [Header("Player Control")]
    [SerializeField] public PlayerController playerController;

    // Private variables
    private MainMenuViewModel MainMenuViewModel;
    private BaseNPC currentInteractingNPC;
    private int carouselCenterIndex = 0;
    private List<ShopItem> currentCarouselItems = new();

    // Swipe detection variables
    private Vector2 swipeStartPos;
    private Vector2 swipeEndPos;
    private float swipeStartTime;
    private bool isSwipeActive = false;
    private bool swipeProcessed = false;

    [SerializeField] private CarouselIndicator carouselIndicator;
    private int lastCarouselIndex = -1;

    [Header("Sound")]
    [SerializeField] private AudioClip openBGM;
    [SerializeField] private AudioClip openaAmbient;
    public void Initialize(MainMenuViewModel viewModel)
    {
        this.MainMenuViewModel = viewModel;
        SetupEventListeners();
        SetupInitialState();
    }

    private void SetupEventListeners()
    {
        // Hide in canvas , it will be shown when player press talk button near NPC with shop
        shopButton.onClick.AddListener(() =>
        {
            MainMenuViewModel.IsDialogueVisible = false;
            MainMenuViewModel.OnShopClicked();

            // ✅ THÊM: Gọi trực tiếp MultiChatManager
            if (multiChatController != null && enableChatDuringShop)
            {
                multiChatController.OpenDialogWithShop();
            }
        });
        closeShopButton.onClick.AddListener(() =>
        {
            multiChatController.CloseCompanionChat();


            MainMenuViewModel.OnCloseShopClicked();
            AudioManager.Instance.PlayBGM(openBGM);
            AudioManager.Instance.PlayAmbient(openaAmbient);
        });
    }

    private void SetupInitialState()
    {
        shopButton.gameObject.SetActive(false);
        shopPanel.SetActive(false);
        playerImage.gameObject.SetActive(false);
    }

    // ✅ THÊM: Update method để detect swipe
    private void Update()
    {
        if (!useCarouselMode || !enableSwipeControl) return;
        if (currentCarouselItems == null || currentCarouselItems.Count <= 1) return;
        if (shopPanel == null || !shopPanel.activeInHierarchy) return;
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (useCarouselMode && enableSwipeControl &&
            currentCarouselItems != null &&
            currentCarouselItems.Count > 1)
        {
            Vector2 inputPos = Vector2.zero;
            bool hasValidInput = false;

#if UNITY_ANDROID || UNITY_IOS
            if (Input.touchCount > 0)
            {
                inputPos = Input.GetTouch(0).position;
                hasValidInput = true;
            }
#else
            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            {
                inputPos = Input.mousePosition;
                hasValidInput = true;
            }
#endif

            // ✅ CHỈ PROCESS SWIPE KHI TRONG VÙNG CAROUSEL
            if (hasValidInput && IsPointerInCarouselArea(inputPos) && 
               !cartPanel.activeInHierarchy && !productDetailPanel.activeInHierarchy)
            {
                DetectSwipeInput();
            }
            else if (isSwipeActive && !IsPointerInCarouselArea(inputPos))
            {
                // Cancel swipe nếu drag ra ngoài vùng
                CancelCurrentSwipe();
            }
        }
    }

    private void CancelCurrentSwipe()
    {
        if (debugSwipe)
            Debug.Log("Swipe cancelled - moved outside carousel area");

        isSwipeActive = false;
        swipeProcessed = false;
    }

    // ✅ THÊM: Main swipe detection method
    private void DetectSwipeInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        DetectTouchSwipe();
#else
        DetectMouseSwipe();
#endif
    }

    // Touch swipe detection for mobile
    private void DetectTouchSwipe()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                        StartSwipeDetection(touch.position);
                    break;

                case TouchPhase.Moved:
                    if (isSwipeActive && !swipeProcessed)
                    {
                        ProcessSwipeMovement(touch.position);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    EndSwipeDetection(touch.position);
                    break;
            }
        }
    }

    // ✅ THÊM: Mouse swipe detection for desktop
    private void DetectMouseSwipe()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartSwipeDetection(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isSwipeActive && !swipeProcessed)
        {
            ProcessSwipeMovement(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndSwipeDetection(Input.mousePosition);
        }
    }

    // ✅ THÊM: Start swipe detection
    private void StartSwipeDetection(Vector2 position)
    {
        if (!IsPointerInCarouselArea(position))
            return;

        swipeStartPos = position;
        swipeStartTime = Time.time;
        isSwipeActive = true;
        swipeProcessed = false;
        if (debugSwipe)
            Debug.Log($"Swipe started at: {position}");
    }

    // ✅ THÊM: Process swipe movement
    private void ProcessSwipeMovement(Vector2 currentPosition)
    {
        if (swipeProcessed) return;

        Vector2 swipeDelta = currentPosition - swipeStartPos;
        float swipeDistance = Mathf.Abs(swipeDelta.x);

        // Check if swipe distance is sufficient
        if (swipeDistance >= minSwipeDistance)
        {
            // Determine swipe direction
            if (swipeDelta.x > 0)
            {
                // Swipe right - go to previous item
                OnSwipeRight();
            }
            else
            {
                // Swipe left - go to next item
                OnSwipeLeft();
            }
            swipeProcessed = true;
            AudioManager.Instance.PlaySFXOneShot("Swipe");
        }
    }

    // End swipe detection
    private void EndSwipeDetection(Vector2 position)
    {
        swipeEndPos = position;
        float swipeTime = Time.time - swipeStartTime;

        if (debugSwipe)
        {
            Vector2 swipeDelta = swipeEndPos - swipeStartPos;
            Debug.Log($"Swipe ended. Delta: {swipeDelta}, Distance: {swipeDelta.magnitude}, Time: {swipeTime}");
        }

        isSwipeActive = false;
        swipeProcessed = false;
    }

    private void OnSwipeLeft()
    {
        if (carouselCenterIndex < currentCarouselItems.Count - 1)
        {
            if (debugSwipe)
                Debug.Log("Swiped Left - Next Item");

            NextCarouselItem();

            // Optional: Add haptic feedback
#if UNITY_ANDROID || UNITY_IOS
            if (SystemInfo.supportsVibration)
                Handheld.Vibrate();
#endif
        }
        else
        {
            // At end of carousel - show bounce effect
            if (debugSwipe)
                Debug.Log("Already at last item");
            StartCoroutine(ShowBounceEffect(SwipeDirection.Left));
        }
    }

    // Handle swipe right (previous item)
    private void OnSwipeRight()
    {
        if (carouselCenterIndex > 0)
        {
            if (debugSwipe)
                Debug.Log("Swiped Right - Previous Item");

            PreviousCarouselItem();

            // Optional: Add haptic feedback
#if UNITY_ANDROID || UNITY_IOS
            if (SystemInfo.supportsVibration)
                Handheld.Vibrate();
#endif
        }
        else
        {
            // At beginning of carousel - show bounce effect
            if (debugSwipe)
                Debug.Log("Already at first item");
            StartCoroutine(ShowBounceEffect(SwipeDirection.Right));
        }
    }
    private bool IsPointerInCarouselArea(Vector2 screenPos)
    {
        if (ParticipantsContainer == null) return false;

        // Kiểm tra screen position có nằm trong RectTransform không
        bool isInside = RectTransformUtility.RectangleContainsScreenPoint(
            ParticipantsContainer, screenPos, null);

        if (debugTouchArea)
            Debug.Log($"Touch at {screenPos} - Inside carousel area: {isInside}");

        return isInside;
    }

    public void SetNPCInteraction(bool isNear, string npcName, ShopData npcShopData = null, BaseNPC npc = null)
    {
        Debug.Log($"🔍 SetNPCInteraction: isNear={isNear}, npcName={npcName}, npc={npc?.name}");

        // ✅ Early null check
        if (isNear && npc == null)
        {
            Debug.LogError($"❌ CRITICAL: NPC is NULL but isNear=true for {npcName}");
            return;
        }

        MainMenuViewModel.PendingDialogue = isNear;
        currentInteractingNPC = npc;

        if (isNear)
        {
            MainMenuViewModel.CurrentNPCName = npcName;
            if (npcShopData != null)
            {
                MainMenuViewModel.CurrentShopData = npcShopData;
                UpdateShopHeaderPreview();
                Debug.Log($"Loaded shop data: {npcShopData.shopName} for NPC: {npcName}");
            }
            else
            {
                Debug.LogWarning($"NPC {npcName} không có ShopData!");
            }
        }
        else
        {
            MainMenuViewModel.CurrentShopData = null;
            MainMenuViewModel.CurrentNPCName = null;
            Debug.Log("Cleared shop data when leaving NPC");
            currentInteractingNPC = null;
        }

        var multiChatManager = MainMenuView.Instance?.GetComponentInChildren<MultiChatManager>();

        Debug.Log($"🔍 MultiChatManager found: {multiChatManager != null}");
        Debug.Log($"🔍 NPC provided: {npc != null}");
        Debug.Log($"🔍 Is near: {isNear}");

        if (multiChatManager != null && npc != null)
        {
            var npcAdapter = npc.GetComponent<NPCChatAdapter>();
            Debug.Log($"🔍 NPCChatAdapter found on {npc.name}: {npcAdapter != null}");

            if (isNear)
            {
                if (npcAdapter != null)
                {
                    multiChatManager.AddParticipant(npcAdapter);
                    Debug.Log($"✅ Successfully added {npc.name} to multi-chat");
                }
                else
                {
                    Debug.LogError($"❌ {npc.name} MISSING NPCChatAdapter component!");
                }
            }
            else
            {
                if (npcAdapter != null)
                {
                    multiChatManager.RemoveParticipant(npcAdapter);
                    Debug.Log($"❌ Removed {npc.name} from multi-chat");
                }
            }
        }
        else
        {
            if (multiChatManager == null)
                Debug.LogError("❌ MultiChatManager NOT FOUND!");
            if (npc == null && isNear)
                Debug.LogError("❌ NPC is NULL but isNear = true!");
        }
    }
    private void PopulateShopItems()
    {
        ClearShopItems();
        if (MainMenuViewModel.CurrentShopData == null)
        {
            Debug.LogError("CurrentShopData is null!");
            return;
        }

        var shopItems = new List<ShopItem>(MainMenuViewModel.CurrentShopData.ItemsDictionary.Values);
        currentCarouselItems = shopItems;
        carouselCenterIndex = 0;
        Debug.Log($"Displaying shop: {MainMenuViewModel.CurrentShopData.shopName} with {shopItems.Count} items");
        UpdateShopHeader();

        if (useCarouselMode)
        {
            //SetupCarouselNavigation();
            UpdateCarouselDisplay();
        }
        else
        {
            // Original grid display logic
            StartCoroutine(LoadItemsBatched());
        }
    }

    private void PreviousCarouselItem()
    {
        if (carouselCenterIndex > 0)
        {
            carouselCenterIndex--;
            StartCoroutine(AnimateCarouselTransition());
        }
    }

    private void NextCarouselItem()
    {
        if (carouselCenterIndex < currentCarouselItems.Count - 1)
        {
            carouselCenterIndex++;
            StartCoroutine(AnimateCarouselTransition());
        }
    }

    private void InitializeFixedPositions()
    {
        if (positionsInitialized) return;

        fixedPositions.Clear();

        // Single Center (Slot 4 - Topmost)
        fixedPositions["single_center"] = new CarouselPosition(
            new Vector3(centerPosition_5, positionY_Center, 0f),
            centerScale, 1f, true, 4
        );

        // ✅ PENTA LAYOUT - Update thứ tự Slot để render đúng lớp

        // Slot 0: Far Left (Lớp dưới cùng)
        fixedPositions["penta_left_far"] = new CarouselPosition(
            new Vector3(leftPosition_1, positionY, 0f),
            sideScale * 0.8f, sideAlpha * 0.5f, false, 0
        );

        // Slot 1: Far Right (Lớp dưới cùng)
        fixedPositions["penta_right_far"] = new CarouselPosition(
            new Vector3(rightPosition_2, positionY, 0f),
            sideScale * 0.8f, sideAlpha * 0.5f, false, 1
        );

        // Slot 2: Near Left (Lớp giữa)
        fixedPositions["penta_left_near"] = new CarouselPosition(
            new Vector3(leftPosition_3, positionY, 0f),
            sideScale, sideAlpha, false, 2
        );

        // Slot 3: Near Right (Lớp giữa)
        fixedPositions["penta_right_near"] = new CarouselPosition(
            new Vector3(rightPosition_4, positionY, 0f),
            sideScale, sideAlpha, false, 3
        );

        // Slot 4: Center (Lớp trên cùng - Render cuối cùng)
        fixedPositions["penta_center"] = new CarouselPosition(
            new Vector3(centerPosition_5, positionY_Center, 0f),
            centerScale, 1f, true, 4
        );

        positionsInitialized = true;
        Debug.Log("✅ Fixed absolute positions initialized (Center on TOP)!");
    }

    private string GetPositionKey(int totalItems, int itemIndex, int centerIndex)
    {

        if (totalItems == 1)
            return "single_center";

        // Logic chung cho 2+ items (luôn dùng hệ 5 slot)
        int diff = itemIndex - centerIndex;

        switch (diff)
        {
            case 0: return "penta_center";
            case -1: return "penta_left_near";
            case 1: return "penta_right_near";
            case -2: return "penta_left_far";
            case 2: return "penta_right_far";
            default: return ""; // Should not happen if logic is correct
        }
    }

    // ✅ FIXED: Sử dụng RectTransform.anchoredPosition
    private void SpawnObjectAtFixedPosition(ShopItem shopItem, int itemIndex, int totalItems, int centerIndex)
    {
        InitializeFixedPositions();

        string positionKey = GetPositionKey(totalItems, itemIndex, centerIndex);
        if (!fixedPositions.ContainsKey(positionKey))
        {
            Debug.LogError($"❌ Position key not found: {positionKey}");
            return;
        }

        CarouselPosition fixedPos = fixedPositions[positionKey];

        // ✅ SPAWN OBJECT
        var itemUI = Instantiate(shopItemPrefab, shopItemsContainer);
        var shopItemUI = itemUI.GetComponent<ShopItemUI>();

        // Setup item data
        if (globalDefaultSprite != null)
            shopItemUI.SetDefaultSprite(globalDefaultSprite);
        shopItemUI.Setup(shopItem, () => OnCarouselItemClicked(shopItem, itemIndex));
        if (shopItem.GetAPIData() != null)
            shopItemUI.SetAPIData(shopItem.GetAPIData());

        // ✅ CRITICAL FIX: Sử dụng RectTransform.anchoredPosition
        RectTransform rectTransform = itemUI.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(fixedPos.position.x, fixedPos.position.y);
            Debug.Log($"✅ Set anchoredPosition to: ({fixedPos.position.x}, {fixedPos.position.y}) for {shopItem.itemName}");
        }
        else
        {
            Debug.LogError($"❌ Could not get RectTransform from {itemUI.name}");
        }

        // Apply scale
        itemUI.transform.localScale = Vector3.one * fixedPos.scale;

        // Apply alpha
        CanvasGroup canvasGroup = itemUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = itemUI.AddComponent<CanvasGroup>();
        canvasGroup.alpha = fixedPos.alpha;

        // Configure carousel mode  
        shopItemUI.SetCarouselMode(fixedPos.isCenter);

        // Add center effects
        if (fixedPos.isCenter)
            AddCenterItemEffects(itemUI);

        Debug.Log($"✅ Spawned {shopItem.itemName} at position X={fixedPos.position.x}");
    }

    private readonly List<GameObject> spawnedItems = new();
    private void EnsureItemCount(int count)
    {
        // MAX 5 SLOTS
        int maxSlots = 5;
        while (spawnedItems.Count < Mathf.Min(count, maxSlots))
        {
            var go = Instantiate(shopItemPrefab, shopItemsContainer);
            spawnedItems.Add(go);
        }

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            spawnedItems[i].SetActive(i < Mathf.Min(count, maxSlots));
        }
    }

    // Cập nhật dữ liệu + vị trí thay vì Clear/Spawn:
    private void UpdateCarouselDisplay()
    {
        if (currentCarouselItems == null || currentCarouselItems.Count == 0) return;

        carouselCenterIndex = Mathf.Clamp(carouselCenterIndex, 0, currentCarouselItems.Count - 1);
        int totalItems = currentCarouselItems.Count;

        // Đảm bảo có đủ 5 slot vật lý (0,1,2,3,4)
        int requiredSlots = (totalItems >= 1) ? 5 : 0;
        EnsureItemCount(requiredSlots);

        // Tắt hết trước
        for (int i = 0; i < spawnedItems.Count; i++)
            spawnedItems[i].SetActive(false);

        // Tính toán index dữ liệu
        int farLeftIndex = carouselCenterIndex - 2;
        int nearLeftIndex = carouselCenterIndex - 1;
        int nearRightIndex = carouselCenterIndex + 1;
        int farRightIndex = carouselCenterIndex + 2;

        // --- CẤP PHÁT SLOT VẬT LÝ THEO THỨ TỰ RENDER (Dưới lên Trên) ---

        // 1. Lớp xa nhất (Far) -> Slot 0 & 1
        if (farLeftIndex >= 0 && totalItems >= 5)
            SetupSlot(0, farLeftIndex); // Slot 0: Far Left

        if (farRightIndex < totalItems && totalItems >= 5)
            SetupSlot(1, farRightIndex); // Slot 1: Far Right

        // 2. Lớp gần (Near) -> Slot 2 & 3
        if (nearLeftIndex >= 0 && totalItems >= 2)
            SetupSlot(2, nearLeftIndex); // Slot 2: Near Left

        if (nearRightIndex < totalItems && totalItems >= 2)
            SetupSlot(3, nearRightIndex); // Slot 3: Near Right

        // 3. Lớp Center (Topmost) -> Slot 4
        // Luôn luôn là slot cuối cùng để đè lên các item khác
        if (totalItems >= 1)
            SetupSlot(4, carouselCenterIndex); // Slot 4: Center

        carouselIndicator?.UpdateDots(carouselCenterIndex, currentCarouselItems.Count);
        // ✅ CHỈ UPDATE DOTS KHI INDEX THAY ĐỔI
        if (carouselCenterIndex != lastCarouselIndex)
        {
            lastCarouselIndex = carouselCenterIndex;
            carouselIndicator?.UpdateDots(carouselCenterIndex, currentCarouselItems.Count);
        }
    }
    private void SetupSlot(int slot, int itemIndex)
    {
        if (slot < 0 || slot >= spawnedItems.Count) return;

        var go = spawnedItems[slot];
        var ui = go.GetComponent<ShopItemUI>();
        var item = currentCarouselItems[itemIndex];

        go.SetActive(true);

        if (globalDefaultSprite != null)
            ui.SetDefaultSprite(globalDefaultSprite);

        ui.Setup(item, () => OnCarouselItemClicked(item, itemIndex));
        if (item.GetAPIData() != null)
            ui.SetAPIData(item.GetAPIData());

        ApplyFixedPosition(go, currentCarouselItems.Count, itemIndex, carouselCenterIndex);
    }

    private void ApplyFixedPosition(GameObject go, int totalItems, int itemIndex, int centerIndex)
    {
        InitializeFixedPositions();
        string key = GetPositionKey(totalItems, itemIndex, centerIndex);
        if (!fixedPositions.TryGetValue(key, out var fp)) return;

        var rt = go.transform as RectTransform;
        if (rt != null)
            rt.anchoredPosition = new Vector2(fp.position.x, fp.position.y);

        go.transform.localScale = Vector3.one * fp.scale;

        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.alpha = fp.alpha;

        var ui = go.GetComponent<ShopItemUI>();
        if (ui != null)
            ui.SetCarouselMode(fp.isCenter);

        // ✅ NEW: Set raycast target based on center position
        SetRaycastTarget(go, fp.isCenter);

        if (fp.isCenter)
            AddCenterItemEffects(go);
    }

    // ✅ NEW: Helper method to control raycast targeting
    private void SetRaycastTarget(GameObject itemGameObject, bool isCenter)
    {
        // Find the button component in children
        Button button = itemGameObject.GetComponentInChildren<Button>();
        if (button == null)
        {
            Debug.LogWarning($"No Button component found in children of {itemGameObject.name}");
            return;
        }

        // Enable/disable raycast target based on center position
        // raycastTarget is a property of Graphic, not Button
        Graphic graphic = button.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = isCenter;
        }

        // Also manage CanvasGroup if you want to block raycasts for non-center items
        CanvasGroup canvasGroup = itemGameObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = isCenter;
        }

        if (debugSwipe)
            Debug.Log($"Raycast target set to {isCenter} for {itemGameObject.name}");
    }
    private List<int> GetDisplayIndices()
    {
        List<int> displayIndices = new List<int>();
        int totalItems = currentCarouselItems.Count;
        int leftIndex = carouselCenterIndex - 1;
        int rightIndex = carouselCenterIndex + 1;

        if (totalItems == 1)
        {
            // Only center item
            displayIndices.Add(carouselCenterIndex);
        }
        else
        {
            // ✅ 2+ ITEMS: LUÔN CỐ GẮN 3 SLOT THEO THỨ TỰ left-center-right
            // Slot 0 = left (nếu có), Slot 1 = center, Slot 2 = right (nếu có)

            // LEFT SLOT (slot 0)
            if (leftIndex >= 0)
                displayIndices.Add(leftIndex);

            // CENTER SLOT (slot 1) - LUÔN CÓ
            displayIndices.Add(carouselCenterIndex);

            // RIGHT SLOT (slot 2)
            if (rightIndex < totalItems)
                displayIndices.Add(rightIndex);
        }

        Debug.Log($"GetDisplayIndices: center={carouselCenterIndex}, total={totalItems}, display={string.Join(",", displayIndices)}");
        return displayIndices;
    }

    private void AddCenterItemEffects(GameObject centerItem)
    {
        // Add outline or shadow effect
        var outline = centerItem.GetComponent<Outline>();
        if (outline == null)
        {
            outline = centerItem.AddComponent<Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(2f, 2f);
        }
        outline.enabled = true;
    }

    // ✅ THÊM: Handle carousel item clicks
    private void OnCarouselItemClicked(ShopItem shopItem, int itemIndex)
    {
        if (itemIndex == carouselCenterIndex)
        {
            // Center item clicked - show detail
            MainMenuViewModel.OnBuyItemClicked(shopItem.itemID);
        }
        else
        {
            // Side item clicked - move to center
            carouselCenterIndex = itemIndex;
            StartCoroutine(AnimateCarouselTransition());
        }
    }

    // ✅ THÊM: Smooth transition animation
    private IEnumerator AnimateCarouselTransition()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            if (t >= 0.5f && elapsed <= duration * 0.6f)
            {
                // Update layout at midpoint
                UpdateCarouselDisplay();
                break;
            }

            yield return null;
        }
    }

    private enum SwipeDirection { Left, Right }

    // ✅ THÊM: Show bounce effect when at carousel ends
    private IEnumerator ShowBounceEffect(SwipeDirection direction)
    {
        float bounceDistance = 20f;
        float duration = 0.3f;

        Vector3 originalPos = shopItemsContainer.localPosition;
        Vector3 bouncePos = originalPos + (direction == SwipeDirection.Left ?
            Vector3.left * bounceDistance : Vector3.right * bounceDistance);

        // Bounce out
        yield return StartCoroutine(AnimatePosition(originalPos, bouncePos, duration * 0.4f));

        // Bounce back
        yield return StartCoroutine(AnimatePosition(bouncePos, originalPos, duration * 0.6f));
    }

    // ✅ THÊM: Helper method for position animation
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

    // ✅ Original methods remain unchanged
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

            if (count % 3 == 0)
            {
                yield return null;
            }
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
            var contentRect = shopItemsContainer.GetComponent<RectTransform>();
            var viewportRect = shopScrollRect.viewport;
            Debug.Log($"Content height: {contentRect.sizeDelta.y}");
            Debug.Log($"Viewport height: {viewportRect.rect.height}");
        }
        else
        {
            Debug.LogError("shopScrollRect is null in UpdateScrollPositionDelayed!");
        }
    }

    private void UpdateShopHeaderPreview()
    {
        if (shopHeaderText != null && MainMenuViewModel.CurrentShopData != null)
        {
            shopHeaderText.text = $"{MainMenuViewModel.CurrentShopData.shopName}";
            Debug.Log($"Preview updated shop header to: {MainMenuViewModel.CurrentShopData.shopName}");
        }
    }

    private void UpdateShopHeader()
    {
        if (shopHeaderText != null && MainMenuViewModel.CurrentShopData != null)
        {
            shopHeaderText.text = $"{MainMenuViewModel.CurrentShopData.shopName}";
            // {MainMenuViewModel.CurrentNPCName}
        }
    }

    private void ClearShopItems()
    {
        foreach (Transform child in shopItemsContainer)
        {
            // Remove outline effects before destroying
            var outline = child.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            Destroy(child.gameObject);
        }

        spawnedItems.Clear();
        if (MainMenuViewModel.CurrentShopData != null)
        {
            MainMenuViewModel.CurrentShopData.ClearCache();
        }
    }

    public void Close_BT_Shop()
    {
        shopButton.gameObject.SetActive(false);
    }

    // ✅ THÊM: Toggle between carousel and grid mode
    public void ToggleDisplayMode()
    {
        useCarouselMode = !useCarouselMode;
        carouselCenterIndex = 0;
        PopulateShopItems();
    }

    // ✅ THÊM: Toggle swipe control
    public void ToggleSwipeControl()
    {
        enableSwipeControl = !enableSwipeControl;
        Debug.Log($"Swipe control: {(enableSwipeControl ? "Enabled" : "Disabled")}");
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
                Debug.Log($"IsShopVisible changed to: {MainMenuViewModel.IsShopVisible}");
                // ✅ Tắt/bật di chuyển player
                if (playerController != null)
                {
                    playerController.SetCanMove(!MainMenuViewModel.IsShopVisible);
                }

                closeShopButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (playerImage != null)
                    playerImage.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (MainMenuViewModel.IsShopVisible)
                {
                    UpdateShopHeader();
                    PopulateShopItems();
                    Close_BT_Shop();
                }
                else
                {
                    ClearShopItems();
                    Close_BT_Shop();
                    if (shopHeaderText != null)
                    {
                        shopHeaderText.text = " ";
                    }
                }
                break;
        }
    }

    private void OnGUI()
    {
        if (debugSwipe && useCarouselMode)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Carousel Index: {carouselCenterIndex}/{currentCarouselItems.Count - 1}");
            GUILayout.Label($"Swipe Active: {isSwipeActive}");
            GUILayout.Label($"Swipe Processed: {swipeProcessed}");

            if (isSwipeActive)
            {
                Vector2 currentPos = Input.mousePosition;
                Vector2 delta = currentPos - swipeStartPos;
                GUILayout.Label($"Swipe Delta: {delta}");
                GUILayout.Label($"Swipe Distance: {delta.magnitude:F1}");
            }

            if (GUILayout.Button("Toggle Swipe Control"))
            {
                ToggleSwipeControl();
            }

            if (GUILayout.Button("Toggle Display Mode"))
            {
                ToggleDisplayMode();
            }

            GUILayout.EndArea();
        }
    }

    // Hàm callback được gọi từ ChatMessageUI khi bấm vào link
    public void OnProductLinkCallback(string productID)
    {
        Debug.Log($"[ShopController] Received request to open product: {productID}");

        // 1. Tìm ShopItem tương ứng trong dữ liệu Shop hiện tại
        if (MainMenuViewModel != null && MainMenuViewModel.CurrentShopData != null)
        {
            // Tìm item trong dictionary
            if (MainMenuViewModel.CurrentShopData.ItemsDictionary.TryGetValue(productID, out var shopItem))
            {
                // 2. Gọi ProductDetailUI để hiển thị
                if (productDetailUI != null)
                {
                    // Lấy customId từ APIData nếu có, hoặc dùng itemID làm fallback
                    string customId = shopItem.GetAPIData()?.customId ?? shopItem.itemID;

                    Debug.Log($"[ShopController] Opening detail for CustomID: {customId}");

                    // Gọi hàm Show của ProductDetailUI
                    // Lưu ý: Hàm ShowUnpaidProductDetail nhận customId và size (optional)
                    productDetailUI.ShowUnpaidProductDetail(customId);
                }
                else
                {
                    Debug.LogError("[ShopController] ProductDetailUI reference is missing!");
                }
            }
            else
            {
                Debug.LogWarning($"[ShopController] Product ID {productID} not found in current shop data.");
            }
        }
    }

}


/*using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel;

public class ShopController : MonoBehaviour
{
    [Header("✅ NEW: Multi Chat Integration")]
    [SerializeField] private MultiChatManager multiChatController;
    [SerializeField] private bool enableChatDuringShop = true;

    [Header("Shop System")]
    [SerializeField] public Button shopButton;
    [SerializeField] private GameObject shopPanel;
    [Tooltip("Content của ScrollRect")]
    [SerializeField] private Transform shopItemsContainer;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private Button closeShopButton;
    [SerializeField] private TextMeshProUGUI shopHeaderText;
    [SerializeField] private RawImage playerImage;

    [Header("Product Detail System")]
    [SerializeField] private GameObject productDetailPanel;
    [SerializeField] private ProductDetailUI productDetailUI;

    [Header("Shopping Cart System")]
    [SerializeField] private Button cartButton;
    [SerializeField] private GameObject cartPanel;
    [SerializeField] private Sprite globalDefaultSprite;

    [Header("✅ SCROLL VIEW SETTINGS")]
    [SerializeField] private ScrollRect shopScrollRect;
    [SerializeField] private float scrollSmoothTime = 0.3f;
    [SerializeField] private bool debugMode = true;

    [Header("✅ VISUAL EFFECTS")]
    [SerializeField] private float scaleSpeed = 8f;
    [SerializeField] private Vector2 itemNormalScale = new Vector2(0.85f, 0.85f);
    [SerializeField] private Vector2 itemCenterScale = new Vector2(1.25f, 1.25f);
    [SerializeField] private float centerSortingOrder = 10f;
    [SerializeField] private float normalSortingOrder = 0f;

    [Header("Player Control")]
    [SerializeField] public PlayerController playerController;

    // Private variables
    private MainMenuViewModel MainMenuViewModel;
    private BaseNPC currentInteractingNPC;
    private List<ShopItem> currentCarouselItems = new();
    private List<GameObject> spawnedItems = new();

    // Scroll + Snap logic
    private float[] itemSnapPositions;
    private float distanceBetweenItems;
    private bool isUserDragging = false;
    private int currentCenterIndex = 0;
    private bool isLayoutReady = false;

    public void Initialize(MainMenuViewModel viewModel)
    {
        this.MainMenuViewModel = viewModel;
        SetupEventListeners();
        SetupInitialState();
    }

    private void SetupEventListeners()
    {
        shopButton.onClick.AddListener(() =>
        {
            MainMenuViewModel.IsDialogueVisible = false;
            MainMenuViewModel.OnShopClicked();
            if (multiChatController != null && enableChatDuringShop)
                multiChatController.OpenDialogWithShop();
        });

        closeShopButton.onClick.AddListener(() =>
        {
            multiChatController?.CloseCompanionChat();
            MainMenuViewModel.OnCloseShopClicked();
        });
    }

    private void SetupInitialState()
    {
        shopButton.gameObject.SetActive(false);
        shopPanel.SetActive(false);
        if (playerImage != null) playerImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isLayoutReady || !shopPanel.activeInHierarchy || currentCarouselItems.Count <= 1) return;

        HandleScrollInput();
        PerformSnapLogic();
        ApplyPureScaleEffect(); 
    }
    [Header("✅ SCALE EFFECT (NO LayoutElement)")]
    [SerializeField] private float centerScale = 2f;
    [SerializeField] private float normalScale = 0.85f;
    private void ApplyPureScaleEffect()  // ✅ KHÔNG LayoutElement
    {
        // Scale bình thường - KHÔNG bị Layout Group override
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            var item = spawnedItems[i].transform;
            float targetScale = (i == currentCenterIndex) ? centerScale : normalScale;
            item.localScale = Vector3.Lerp(item.localScale, new Vector3(targetScale, targetScale, 1f), Time.deltaTime * scaleSpeed);
        }
    }
    private void HandleScrollInput()
    {
        if (Input.GetMouseButton(0))
        {
            isUserDragging = true;
        }
        else
        {
            isUserDragging = false;
        }
    }

    private void PerformSnapLogic()
    {
        if (isUserDragging || currentCarouselItems.Count == 0) return;

        CalculateSnapPositions();

        float scrollPos = shopScrollRect.horizontalNormalizedPosition;
        int nearestIndex = 0;
        float minDistance = Mathf.Abs(itemSnapPositions[0] - scrollPos);

        for (int i = 1; i < itemSnapPositions.Length; i++)
        {
            float distance = Mathf.Abs(itemSnapPositions[i] - scrollPos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        if (nearestIndex != currentCenterIndex)
        {
            currentCenterIndex = nearestIndex;
        }

        // Smooth snap
        float targetPos = itemSnapPositions[currentCenterIndex];
        shopScrollRect.horizontalNormalizedPosition = Mathf.Lerp(
            shopScrollRect.horizontalNormalizedPosition,
            targetPos,
            scrollSmoothTime
        );
    }

    private void CalculateSnapPositions()
    {
        if (currentCarouselItems.Count == 0) return;

        itemSnapPositions = new float[currentCarouselItems.Count];
        distanceBetweenItems = 1f / Mathf.Max(1f, currentCarouselItems.Count - 1f);

        for (int i = 0; i < currentCarouselItems.Count; i++)
        {
            itemSnapPositions[i] = i * distanceBetweenItems;
        }
    }
    public void Close_BT_Shop()
    {
        shopButton.gameObject.SetActive(false);

    }


    // ✅ FIXED: Hiển thị TẤT CẢ 25 items (KHÔNG còn giới hạn 5)
    private void PopulateShopItems()
    {
        ClearShopItems();
        if (MainMenuViewModel.CurrentShopData == null)
        {
            Debug.LogError("CurrentShopData is null!");
            return;
        }

        var shopItems = new List<ShopItem>(MainMenuViewModel.CurrentShopData.ItemsDictionary.Values);
        currentCarouselItems = shopItems;
        currentCenterIndex = 0;

        Debug.Log($"🛒 Displaying {shopItems.Count} items in ScrollView mode");

        // SPAWN TẤT CẢ ITEMS (25+)
        spawnedItems.Clear();
        for (int i = 0; i < shopItems.Count; i++)
        {
            var shopItem = shopItems[i];
            var itemUI = Instantiate(shopItemPrefab, shopItemsContainer);
            var shopItemUI = itemUI.GetComponent<ShopItemUI>();

            spawnedItems.Add(itemUI);

            if (globalDefaultSprite != null)
                shopItemUI.SetDefaultSprite(globalDefaultSprite);

            int index = i;
            shopItemUI.Setup(shopItem, () => OnItemClicked(shopItem, index));

            if (shopItem.GetAPIData() != null)
                shopItemUI.SetAPIData(shopItem.GetAPIData());

            // Set Canvas cho sorting layer
            Canvas canvas = itemUI.GetComponent<Canvas>();
            if (canvas == null)
                canvas = itemUI.AddComponent<Canvas>();
            canvas.sortingOrder = (int)normalSortingOrder;
        }

        UpdateShopHeader();
        StartCoroutine(InitializeScrollView());
    }

    private IEnumerator InitializeScrollView()
    {
        // Đợi Layout Group tính toán
        yield return new WaitForEndOfFrame();

        // Force layout update
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(shopItemsContainer.GetComponent<RectTransform>());
        yield return null;

        // Reset scroll position
        if (shopScrollRect != null)
        {
            shopScrollRect.horizontalNormalizedPosition = 0f;
        }

        isLayoutReady = true;
        Debug.Log("✅ ScrollView READY - Showing all items!");
    }

    private void OnItemClicked(ShopItem shopItem, int itemIndex)
    {
        if (itemIndex == currentCenterIndex)
        {
            // Item ở giữa → MUA
            Debug.Log($"✅ BUY: {shopItem.itemName}");
            MainMenuViewModel.OnBuyItemClicked(shopItem.itemID);
        }
        else
        {
            // Item khác → Scroll tới giữa
            Debug.Log($"🔄 Scroll to: {shopItem.itemName} (Index {itemIndex})");
            ScrollToItem(itemIndex);
        }
    }

    private void ScrollToItem(int targetIndex)
    {
        if (targetIndex >= 0 && targetIndex < currentCarouselItems.Count)
        {
            currentCenterIndex = targetIndex;
            float targetPos = itemSnapPositions != null ? itemSnapPositions[targetIndex] : 0f;
            shopScrollRect.horizontalNormalizedPosition = targetPos;
        }
    }

    public void SetNPCInteraction(bool isNear, string npcName, ShopData npcShopData = null, BaseNPC npc = null)
    {
        MainMenuViewModel.PendingDialogue = isNear;
        currentInteractingNPC = npc;

        if (isNear)
        {
            MainMenuViewModel.CurrentNPCName = npcName;
            if (npcShopData != null)
            {
                MainMenuViewModel.CurrentShopData = npcShopData;
                UpdateShopHeaderPreview();
            }
        }
        else
        {
            MainMenuViewModel.CurrentShopData = null;
            MainMenuViewModel.CurrentNPCName = null;
            currentInteractingNPC = null;
        }

        // MultiChat integration
        var multiChatManager = MainMenuView.Instance?.GetComponentInChildren<MultiChatManager>();
        if (multiChatManager != null && npc != null)
        {
            var npcAdapter = npc.GetComponent<NPCChatAdapter>();
            if (isNear && npcAdapter != null)
                multiChatManager.AddParticipant(npcAdapter);
            else if (npcAdapter != null)
                multiChatManager.RemoveParticipant(npcAdapter);
        }
    }

    private void UpdateShopHeaderPreview()
    {
        if (shopHeaderText != null && MainMenuViewModel.CurrentShopData != null)
            shopHeaderText.text = $"{MainMenuViewModel.CurrentShopData.shopName}";
    }

    private void UpdateShopHeader()
    {
        if (shopHeaderText != null && MainMenuViewModel.CurrentShopData != null)
            shopHeaderText.text = $"{MainMenuViewModel.CurrentShopData.shopName} - {MainMenuViewModel.CurrentNPCName}";
    }

    private void ClearShopItems()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();

        if (MainMenuViewModel.CurrentShopData != null)
            MainMenuViewModel.CurrentShopData.ClearCache();

        isLayoutReady = false;
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

                if (playerController != null)
                    playerController.SetCanMove(!MainMenuViewModel.IsShopVisible);

                closeShopButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);
                if (playerImage != null)
                    playerImage.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (MainMenuViewModel.IsShopVisible)
                {
                    UpdateShopHeader();
                    PopulateShopItems();
                }
                else
                {
                    ClearShopItems();
                    if (shopHeaderText != null)
                        shopHeaderText.text = " ";
                }
                break;
        }
    }

    public void OnProductLinkCallback(string productID)
    {
        if (MainMenuViewModel?.CurrentShopData != null &&
            MainMenuViewModel.CurrentShopData.ItemsDictionary.TryGetValue(productID, out var shopItem))
        {
            if (productDetailUI != null)
            {
                string customId = shopItem.GetAPIData()?.customId ?? shopItem.itemID;
                productDetailUI.ShowUnpaidProductDetail(customId);
            }
        }
    }
}
*/