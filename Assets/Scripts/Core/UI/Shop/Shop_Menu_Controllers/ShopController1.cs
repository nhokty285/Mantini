using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainMenuViewModel;

public class ShopController_1 : MonoBehaviour
{
    [Header("✅ NEW: Multi Chat Integration")]
    [SerializeField] private MultiChatManager multiChatController;
    [SerializeField] private bool enableChatDuringShop = true;

    [Header("Shop System")]
    [SerializeField] public Button shopButton;
    [SerializeField] private GameObject shopPanel;
    [Tooltip("Đây phải là Content GameObject nằm trong ScrollRect")]
    [SerializeField] private Transform shopItemsContainer;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private ShopData shopData;
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

    [Header("✅ DYNAMIC SCROLL SETTINGS")]
    [SerializeField] private ScrollRect shopScrollRect;
    [SerializeField] private float scrollSmoothTime = 0.2f;

    [Header("✅ SCALE EFFECT")]
    [SerializeField] private float scaleSpeed = 8f;
    [SerializeField] private Vector2 itemOriginalScale = new Vector2(0.9f, 0.9f);
    [SerializeField] private Vector2 itemCenterScale = new Vector2(1.3f, 1.3f);

    [Header("DEBUG")]
    [SerializeField] private bool showDebugLog = true;

    [Header("Player Control")]
    [SerializeField] public PlayerController playerController;

    // Private variables
    private MainMenuViewModel MainMenuViewModel;
    private BaseNPC currentInteractingNPC;
    private List<ShopItem> currentCarouselItems = new();

    // Logic Swipe Dynamic Variables
    private float[] itemSnapPositions;
    private float distanceBetweenItems;
    private float currentScrollPosition = 0;
    private bool isUserDragging = false;
    private int currentCenterIndex = 0;
    private bool isLayoutReady = false;
    private int lastCenterIndex = -1; // Track để tránh spam scale

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

    public void Close_BT_Shop()
    {
        shopButton.gameObject.SetActive(false);
    }

    // ✅ UPDATE: SNAP + SCALE + DEBUG
    private void Update()
    {
        if (!isLayoutReady || shopPanel == null || !shopPanel.activeInHierarchy) return;
        if (currentCarouselItems.Count <= 1) return;

        CalculateSnapPositions();
        HandleUserInput();
        PerformSnapLogic();

        // ✅ SCALE EFFECT theo currentCenterIndex
        ApplyScaleEffectToCenterItem();

        // ✅ DEBUG
        if (showDebugLog)
            DebugCenterItem();
    }

    private void CalculateSnapPositions()
    {
        itemSnapPositions = new float[currentCarouselItems.Count];
        distanceBetweenItems = 1f / Mathf.Max(1f, currentCarouselItems.Count - 1f);

        for (int i = 0; i < currentCarouselItems.Count; i++)
        {
            itemSnapPositions[i] = i * distanceBetweenItems;
        }
    }

    private void HandleUserInput()
    {
        if (Input.GetMouseButton(0))
        {
            isUserDragging = true;
            currentScrollPosition = shopScrollRect.horizontalNormalizedPosition;
        }
        else
        {
            isUserDragging = false;
        }
    }

    private void PerformSnapLogic()
    {
        if (isUserDragging) return;

        float nearestPos = itemSnapPositions[0];
        float minDistance = Mathf.Abs(itemSnapPositions[0] - currentScrollPosition);
        currentCenterIndex = 0;

        for (int i = 1; i < itemSnapPositions.Length; i++)
        {
            float dist = Mathf.Abs(itemSnapPositions[i] - currentScrollPosition);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestPos = itemSnapPositions[i];
                currentCenterIndex = i;
            }
        }

        shopScrollRect.horizontalNormalizedPosition = Mathf.Lerp(
            shopScrollRect.horizontalNormalizedPosition,
            nearestPos,
            scrollSmoothTime
        );
    }

    // ✅ THÊM MỚI: Scale effect CHO RIÊNG item ở currentCenterIndex
    private void ApplyScaleEffectToCenterItem()
    {
        // Chỉ scale khi centerIndex thay đổi (tránh spam)
        if (currentCenterIndex == lastCenterIndex) return;

        for (int i = 0; i < shopItemsContainer.childCount; i++)
        {
            Transform child = shopItemsContainer.GetChild(i);
            ShopItemUI itemUI = child.GetComponent<ShopItemUI>();

            if (itemUI == null) continue;

            if (i == currentCenterIndex)
            {
                // ✅ CENTER ITEM: Scale TO + Đưa lên trên + Bật interact
                child.localScale = Vector3.Lerp(child.localScale,
                    new Vector3(itemCenterScale.x, itemCenterScale.y, 1f),
                    Time.deltaTime * scaleSpeed);
                child.SetAsLastSibling(); // Đưa lên trên cùng
                EnableItemInteraction(child, true);
            }
            else
            {
                // ✅ CÁC ITEM KHÁC: Scale nhỏ lại
                child.localScale = Vector3.Lerp(child.localScale,
                    new Vector3(itemOriginalScale.x, itemOriginalScale.y, 1f),
                    Time.deltaTime * scaleSpeed);
                EnableItemInteraction(child, false);
            }
        }

        lastCenterIndex = currentCenterIndex;
    }

    private void EnableItemInteraction(Transform itemTransform, bool isInteractable)
    {
        CanvasGroup canvasGroup = itemTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = itemTransform.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = isInteractable;
        canvasGroup.blocksRaycasts = true; // Luôn cho phép click để scroll
    }

    // ✅ DEBUG: In ra thông tin item đang ở giữa ScrollView
    private void DebugCenterItem()
    {
        float scrollPos = shopScrollRect.horizontalNormalizedPosition;
        string itemName = currentCenterIndex < currentCarouselItems.Count
            ? currentCarouselItems[currentCenterIndex].itemName
            : "N/A";

        Debug.Log($"🔍 ScrollPos: {scrollPos:F3} | CenterIndex: {currentCenterIndex} | Item: {itemName} | Total: {currentCarouselItems.Count} | Scale: {itemCenterScale}");
    }

    private void PopulateShopItems()
    {
        ClearShopItems();
        if (MainMenuViewModel.CurrentShopData == null) return;

        var shopItems = new List<ShopItem>(MainMenuViewModel.CurrentShopData.ItemsDictionary.Values);
        currentCarouselItems = shopItems;
        UpdateShopHeader();

        for (int i = 0; i < shopItems.Count; i++)
        {
            var shopItem = shopItems[i];
            var itemUI = Instantiate(shopItemPrefab, shopItemsContainer);
            var shopItemUI = itemUI.GetComponent<ShopItemUI>();

            if (globalDefaultSprite != null)
                shopItemUI.SetDefaultSprite(globalDefaultSprite);

            int capturedIndex = i;
            shopItemUI.Setup(shopItem, () => OnCarouselItemClicked(shopItem, capturedIndex));

            if (shopItem.GetAPIData() != null)
                shopItemUI.SetAPIData(shopItem.GetAPIData());
        }

        StartCoroutine(WaitForLayoutAndInitialize());
    }

    private IEnumerator WaitForLayoutAndInitialize()
    {
        for (int i = 0; i < 3; i++)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(shopItemsContainer.GetComponent<RectTransform>());
            yield return null;
        }

        if (shopScrollRect != null)
        {
            shopScrollRect.horizontalNormalizedPosition = 0f;
            currentScrollPosition = 0f;
            currentCenterIndex = 0;
            lastCenterIndex = -1;
        }

        isLayoutReady = true;
        Debug.Log("✅ Shop Layout READY! ScrollPos: " + shopScrollRect.horizontalNormalizedPosition);
    }

    private void OnCarouselItemClicked(ShopItem shopItem, int itemIndex)
    {
        if (itemIndex == currentCenterIndex)
        {
            Debug.Log($"✅ BUY: {shopItem.itemName}");
            MainMenuViewModel.OnBuyItemClicked(shopItem.itemID);
        }
        else
        {
            Debug.Log($"🔄 Scroll to Item {itemIndex}: {shopItem.itemName}");
            if (currentCarouselItems.Count > 1)
            {
                float targetPos = itemIndex * distanceBetweenItems;
                shopScrollRect.horizontalNormalizedPosition = targetPos;
                currentScrollPosition = targetPos;
            }
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
        foreach (Transform child in shopItemsContainer)
            Destroy(child.gameObject);

        if (MainMenuViewModel.CurrentShopData != null)
            MainMenuViewModel.CurrentShopData.ClearCache();
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
                    isLayoutReady = false;
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
