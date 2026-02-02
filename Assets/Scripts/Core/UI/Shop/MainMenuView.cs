using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    public static MainMenuView Instance { get; private set; }

    [Header("Controllers")]
    [SerializeField] private MenuController menuController;
    [SerializeField] private ShopController shopController;
    [SerializeField] private MultiChatManager companionChatController;

    [Header("NPC Interaction")]
    [SerializeField] public Button talkButton;
    [SerializeField] private GameObject dialoguePopup;
    [SerializeField] private Button skipButton;
  

    private MainMenuViewModel MainMenuViewModel;
    private BaseNPC currentInteractingNPC;
    private ShopData cachedShopData;

    private void Start()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        MainMenuViewModel = new MainMenuViewModel();
        InitializeControllers();
        SetupNPCEventListeners();
        MainMenuViewModel.PropertyChangedd += OnViewModelChanged;
    }

    private void InitializeControllers()
    {
        if (menuController != null)
            menuController.Initialize(MainMenuViewModel);

        if (shopController != null)
            shopController.Initialize(MainMenuViewModel);

        if (companionChatController != null)
            companionChatController.InitializeWithoutButton(MainMenuViewModel);
    }

    private void SetupNPCEventListeners()
    {
        talkButton.onClick.AddListener(()=> { 
        
            OnTalkButtonClicked();
            AudioManager.Instance.PlaySFXOneShot("Button");
        });
        skipButton.onClick.AddListener(MainMenuViewModel.OnSkipClicked);
        talkButton.gameObject.SetActive(false);
    
    }

    private void SetupIcon(Sprite npcImage)
    {
        if (npcImage != null && talkButton != null)
        {
            // Get child by name (e.g., "IconImage")
            Transform childTransform = talkButton.transform.Find("IconImage");

            if (childTransform != null)
            {
                Image childImage = childTransform.GetComponent<Image>();
                if (childImage != null)
                {
                    childImage.sprite = npcImage;
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Child object 'IconImage' not found!");
            }
        }
    }

    // ✅ NEW: Handle talk button click
    private void OnTalkButtonClicked()
    {
        Debug.Log($"💬 Talk button clicked for {currentInteractingNPC?.GetNPCName()}");

        if (currentInteractingNPC != null)
        {
            // Ẩn talk button
            talkButton.gameObject.SetActive(false);

            // Bắt đầu dialogue thông qua NPCDialogueSystem
            NPCDialogueSystem.Instance.StartDialogue(currentInteractingNPC, cachedShopData);
        }
        else
        {
            Debug.LogError("❌ currentInteractingNPC is NULL when talk button clicked!");
        }
    }
    
  

    // ✅ NEW: Auto-open shop khi dialogue kết thúc
    public void AutoOpenShop()
    {
        Debug.Log($"🏪 Auto-opening shop from dialogue system");

        if (shopController != null && shopController.shopButton != null)
        {
            // Simulate button click
            shopController.shopButton.onClick.Invoke();
        }
        else
        {
            Debug.LogError("❌ shopController or shopButton not found!");
        }
    }

    public void SetNPCInteraction(bool isNear, string npcName, ShopData npcShopData = null, BaseNPC npc = null)
    {
        Debug.Log($"🔍 SetNPCInteraction: isNear={isNear}, npcName={npcName}, npc={npc?.name}");

        shopController.SetNPCInteraction(isNear, npcName, npcShopData, npc);

        if (isNear)
        {
            // Lưu reference cho dialogue system
            currentInteractingNPC = npc;
            cachedShopData = npcShopData;
            MainMenuViewModel.PendingDialogue = true;

            // ✅ NEW: Change talk button image to NPC image
            if (npc is VendorNPC vendorNPC)
            {
                Sprite npcImage = vendorNPC.GetVendorImage();
                SetupIcon(npcImage);
            }
        }
        else
        {
            companionChatController.OnPlayerLeavingNPC();
            currentInteractingNPC = null;
            cachedShopData = null;
        }
    }

    private void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
    {
        // Delegate to specific controllers
        menuController.OnViewModelChanged(e.PropertyName);
        shopController.OnViewModelChanged(e.PropertyName);

        // Handle NPC-specific properties
        switch (e.PropertyName)
        {
            case nameof(MainMenuViewModel.PendingDialogue):
                talkButton.gameObject.SetActive(MainMenuViewModel.PendingDialogue);
                shopController.shopButton.gameObject.SetActive(MainMenuViewModel.IsShopVisible);

                if (!MainMenuViewModel.PendingDialogue)
                {
                    MainMenuViewModel.IsDialogueVisible = false;
                    MainMenuViewModel.IsShopVisible = false;
                }
                break;

            case nameof(MainMenuViewModel.IsDialogueVisible):
                dialoguePopup.SetActive(!MainMenuViewModel.IsDialogueVisible);
                shopController.shopButton.gameObject.SetActive(MainMenuViewModel.PendingDialogue);
                skipButton.gameObject.SetActive(MainMenuViewModel.IsDialogueVisible);

                if (!MainMenuViewModel.IsDialogueVisible)
                {
                    Close_BT_Shop();
                }
                break;
        }
    }

    public void Close_BT_Shop()
    {
        shopController.Close_BT_Shop();
        talkButton.gameObject.SetActive(false);
        currentInteractingNPC = null;
        cachedShopData = null;
        AudioManager.Instance.StopBGM();
        AudioManager.Instance.StopAmbient();
    }
}