using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CompanionSelection : MonoBehaviour
{
    [Header("Companion Data")]
    public CharacterData[] companionDataArray; // Array CompanionData thay vì GameObject[]

    [Header("Display Parents")]
    public Transform selectionDisplayParent; // Vị trí hiển thị trong màn selection
    public Transform infoDisplayParent; // Vị trí hiển thị trong màn info

    [Header("Selection UI")]
    public GameObject selectionPanel; // Panel chọn companion
    public Button[] selectionButtons; // 3 nút chọn (button-based selection)
    public TextMeshProUGUI selectionNameText; // Tên companion đang chọn
    public Button selectionContinueButton; // Nút "Tiếp tục"
    public Image[] characterIcons;

    [Header("Info UI")]
    public GameObject infoPanel; // Panel hiển thị info
    public TextMeshProUGUI greetingText; // "Xin chào mình là..."
    public TextMeshProUGUI infoNameText; // Tên companion
    public TextMeshProUGUI npcDescriptionText; // Mô tả companion
    public Button changeCompanionButton; // Nút "Đổi companion"
    public Button infoContinueButton; // Nút "Tiếp tục"

    [Header("Confirmation Popup")]
    public GameObject confirmationPopup;
    public TextMeshProUGUI popupMessageText;
    public Button popupCancelButton; // "Khoan đã"
    public Button popupConfirmButton; // "Đi nào"

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public Button dialogueContinueButton;
        
    [Header("UI Resources")]
    public Sprite normalSprite;   // Kéo ảnh trạng thái thường vào đây
    public Sprite selectedSprite; // Kéo ảnh trạng thái được chọn (sáng lên) vào đây

    // State management
    private int selectedIndex = 0;
    private int infoStep = 0; // 0: Greeting, 1: Description
    private GameObject currentSelectionInstance; // Preview instance ở selection panel
    private GameObject currentInfoInstance; // Preview instance ở info panel

    const string KEY_SELECTED_COMPANION = "SelectedCompanion";

    void Start()
    {
        // Đăng ký CompanionData vào PlayerDataManager
        PlayerDataManager.Instance.RegisterCompanionData(companionDataArray);

        // Setup button listeners
        SetupInfoListeners();
        SetupSelectionListeners();

        // Load saved selection
        selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, 0);

      
            dialoguePanel.SetActive(false);
            if (dialogueContinueButton)
            {
                dialogueContinueButton.onClick.RemoveAllListeners();
                dialogueContinueButton.onClick.AddListener(EndDialogue);
            }
        

        // Start với selection panel (hidden initially)
            ShowSelectionPanel();
            selectionPanel.SetActive(false); // Hide ban đầu, PlayerCharacterSelection sẽ show
        

    }
    #region Dialogue Logic
    void EndDialogue()
    {
        if (dialoguePanel) dialoguePanel.SetActive(false);
        ShowInfoPanel();
    }
    #endregion

    #region Selection Panel

    void SetupSelectionListeners()
    {
        // Setup 3 nút chọn companion
        for (int i = 0; i < selectionButtons.Length && i < companionDataArray.Length; i++)
        {
            int index = i; // Capture index cho closure
            selectionButtons[i].onClick.AddListener(() => OnSelectCompanion(index));
        }

        selectionContinueButton.onClick.AddListener(GoToInfoPanel);
    }

    void OnSelectCompanion(int index)
    {
        if (index < 0 || index >= companionDataArray.Length)
        {
            Debug.LogWarning($"[CompanionSelection] Invalid index {index}");
            return;
        }

        selectedIndex = index;

        // Lưu ngay vào PlayerPrefs để sync
        PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, selectedIndex);
        PlayerPrefs.Save();

        // Update preview display (dùng previewPrefab)
        SpawnPreviewAt(ref currentSelectionInstance, selectionDisplayParent,
                       companionDataArray[selectedIndex].previewPrefab);

        // Update UI text
        if (selectionNameText)
            selectionNameText.text = companionDataArray[selectedIndex].characterName;

        // Highlight selected button
        UpdateSelectionButtonStates();

        Debug.Log($"[CompanionSelection] Selected: {companionDataArray[selectedIndex].characterName}");
    }

    /* void UpdateSelectionButtonStates()
     {
         for (int i = 0; i < selectionButtons.Length; i++)
         {
             if (i >= companionDataArray.Length) continue;

             var button = selectionButtons[i];
             var colors = button.colors;
             bool isSelected = (i == selectedIndex);

             // 1. Thay đổi trực tiếp ảnh hiển thị
             if (button.image != null)
             {
                 // Nếu được chọn -> Dùng ảnh Selected, ngược lại dùng ảnh Normal
                 button.image.sprite = isSelected ? selectedSprite : normalSprite;

             }

             // Visual feedback cho button đã chọn
             colors.normalColor = isSelected ? new Color(1f, 0.9f, 0.2f, 1f) : Color.white;
             button.colors = colors;
             button.transform.localScale = isSelected ? Vector3.one * 1.1f : Vector3.one;
         }
     }*/

    void UpdateSelectionButtonStates()
    {
        for (int i = 0; i < selectionButtons.Length; i++)
        {
            if (i >= companionDataArray.Length) continue;

            var button = selectionButtons[i];
            bool isSelected = (i == selectedIndex);

            // --- 1. XỬ LÝ NÚT NỀN (BACKGROUND) ---
            if (button.image != null)
            {
                // Đổi khung nền: Sáng (Selected) hoặc Thường (Normal)
                button.image.sprite = isSelected ? selectedSprite : normalSprite;
            }

            // --- 2. XỬ LÝ ẢNH ĐẠI DIỆN NHÂN VẬT (ICON) ---
            // Kiểm tra xem mảng icon có đủ phần tử tương ứng không
            if (characterIcons != null && i < characterIcons.Length && characterIcons[i] != null)
            {
                // Nếu chọn: Màu gốc sáng rõ (White).
                // Nếu không chọn: Màu tối đi (Gray hoặc chỉnh tay new Color(0.5f, 0.5f, 0.5f, 1f)).
                characterIcons[i].color = isSelected ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
            }

            // --- 3. HIỆU ỨNG PHỤ (Scale / Button Colors) ---
            // Giữ lại hiệu ứng phóng to nút nếu muốn
            button.transform.localScale = isSelected ? Vector3.one * 1.1f : Vector3.one;

            // Reset màu của bản thân nút về trắng để không bị ám màu lên sprite nền mới thay
            var colors = button.colors;
            colors.normalColor = Color.white;
            button.colors = colors;
        }
    }


    void GoToInfoPanel()
    {
        // Save selection
        PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, selectedIndex);
        PlayerPrefs.Save();

        // Transition UI
        selectionPanel.SetActive(false);
        ShowInfoPanel();
    }

    void ShowSelectionPanel()
    {
        // Tắt info panel, bật selection panel
        if (infoPanel) infoPanel.SetActive(false);
        if (selectionPanel) selectionPanel.SetActive(true);

        // Sync state từ PlayerPrefs
        selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, selectedIndex);

        // Spawn preview instance
        SpawnPreviewAt(ref currentSelectionInstance, selectionDisplayParent,
                       companionDataArray[selectedIndex].previewPrefab);

        // Update UI
        if (selectionNameText)
            selectionNameText.text = companionDataArray[selectedIndex].characterName;

        UpdateSelectionButtonStates();
    }

    #endregion

    #region Info Panel

    void SetupInfoListeners()
    {
        if (changeCompanionButton)
            changeCompanionButton.onClick.AddListener(ShowSelectionPanel);

        if (infoContinueButton)
            infoContinueButton.onClick.AddListener(ConfirmAndStart);
/*
        if (popupCancelButton)
            popupCancelButton.onClick.AddListener(HidePopup);

        if (popupConfirmButton)
            popupConfirmButton.onClick.AddListener(ConfirmAndStart);*/
    }

    void ShowInfoPanel()
    {
        infoPanel.SetActive(true);

        // Sync state
        selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, selectedIndex);

        // Spawn preview instance (dùng previewPrefab)
        SpawnPreviewAt(ref currentInfoInstance, infoDisplayParent,
                       companionDataArray[selectedIndex].previewPrefab);

        // Update text info
        UpdateInfoTexts();
        var companionData = companionDataArray[selectedIndex];
    }

    void UpdateInfoTexts()
    {
        var companionData = companionDataArray[selectedIndex];

        if (greetingText)
            greetingText.text = $"Xin chào mình là {companionData.characterName}, mình sẽ đồng hành cùng bạn";

        if (infoNameText)
            infoNameText.text = companionData.characterName;

        if (npcDescriptionText)
            npcDescriptionText.text = companionData.description;
    }
    #endregion

    /* #region Confirmation Popup

      void ShowPopup()
      {
          if (confirmationPopup)
          {
              if (popupMessageText)
                  popupMessageText.text = "Nào mình cùng bắt đầu Shoppin nhé! :D";

              confirmationPopup.SetActive(true);
          }
      }

      void HidePopup()
      {
          if (confirmationPopup)
              confirmationPopup.SetActive(false);
      }

      void ConfirmAndStart()
      {
          HidePopup();

          // Lưu companion selection qua PlayerDataManager
          PlayerDataManager.Instance.SaveCompanionIndex(selectedIndex);
          Debug.Log($"[CompanionSelection] Starting game with companion: {companionDataArray[selectedIndex].characterName}");

          // *** THÊM: Sync lên server trước khi vào gameplay ***
          var syncer = FindFirstObjectByType<PlayerApiService>();
          if (syncer != null)
          {
              syncer.SyncSelectionToServer();
          }
          else
          {
              Debug.LogWarning("[CompanionSelection] PlayerSelectionSync not found in scene!");
          }

          // Load gameplay scene
          SceneManager.LoadScene("MapTest2");
      }


      #endregion*/

    #region Confirmation Popup

    void ShowPopup()
    {
        if (confirmationPopup)
        {
            // Get the selected companion data
            var companionData = companionDataArray[selectedIndex];

            // Update popup message
            if (popupMessageText)
                popupMessageText.text = "Nào mình cùng bắt đầu Shoppin nhé! :D";

            // Update companion image in popup
            Image popupImage = confirmationPopup.GetComponentsInChildren<Image>()[2];
            if (popupImage != null && companionData.characterIcon != null)
            {
                popupImage.sprite = companionData.characterIcon;
            }
            else if (popupImage == null)
            {
                Debug.LogWarning("[CompanionSelection] No Image component found in confirmationPopup children!");
            }

            confirmationPopup.SetActive(true);
            Debug.Log($"[CompanionSelection] Showing popup for: {companionData.characterName}");
        }
    }

    /*void HidePopup()
    {
        if (confirmationPopup)
        {
            confirmationPopup.SetActive(false);
            
            // Optional: Clear the image when hiding
            Image popupImage = confirmationPopup.GetComponentInChildren<Image>();
            if (popupImage != null)
            {
                popupImage.sprite = null;
            }
        }
    }*/

    void ConfirmAndStart()
    {
        /*HidePopup();*/

        // Lưu companion selection qua PlayerDataManager

        PlayerDataManager.Instance.SaveCompanionIndex(selectedIndex);
        Debug.Log($"[CompanionSelection] Starting game with companion: {companionDataArray[selectedIndex].characterName}");

        // *** THÊM: Sync lên server trước khi vào gameplay ***
        var syncer = FindFirstObjectByType<PlayerApiService>();
        if (syncer != null)
        {
            syncer.SyncSelectionToServer();
        }
        else
        {
            Debug.LogWarning("[CompanionSelection] PlayerSelectionSync not found in scene!");
        }

        // Load gameplay scene
        //SceneManager.LoadScene("MapTest2");

        LevelLoader.Instance.LoadLevel("MapTest2");
    }
    #endregion

    #region Helper Methods

    void SpawnPreviewAt(ref GameObject holder, Transform parent, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[CompanionSelection] Preview prefab is null!");
            return;
        }

        // Cleanup old instance
        if (holder != null)
        {
            DestroyImmediate(holder);
        }

        // Instantiate new preview (RawImage prefab)
        holder = Instantiate(prefab, parent);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        CleanupInstances();
    }

    private void CleanupInstances()
    {
        if (currentSelectionInstance != null)
            Destroy(currentSelectionInstance);

        if (currentInfoInstance != null)
            Destroy(currentInfoInstance);
    }

    #endregion
}
