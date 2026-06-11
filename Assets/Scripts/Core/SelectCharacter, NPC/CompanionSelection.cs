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
    public Transform infoDisplayParent;      // Vị trí hiển thị trong màn info

    [Header("Selection UI")]
    public GameObject selectionPanel;          // Panel chọn companion
    public Button[] selectionButtons;          // 3 nút chọn (button-based selection)
    public TextMeshProUGUI selectionNameText;  // Tên companion đang chọn
    public Button selectionContinueButton;     // Nút "Tiếp tục"
    public Image[] characterIcons;

    [Header("Info UI")]
    public GameObject infoPanel;                 // Panel hiển thị info
    public TextMeshProUGUI greetingText;         // "Xin chào mình là..."
    public TextMeshProUGUI infoNameText;         // Tên companion
    public TextMeshProUGUI npcDescriptionText;   // Mô tả companion
    public Button changeCompanionButton;         // Nút "Đổi companion"
    public Button infoContinueButton;            // Nút "Tiếp tục"

    [Header("Description Skip Button")]
    [SerializeField] private Button btContinue;  // Button ẩn (alpha 0) trùm lên Text_ChatnPC

    [Header("Confirmation Popup")]
    public GameObject confirmationPopup;
    public TextMeshProUGUI popupMessageText;
    public Button popupCancelButton; // "Khoan đã"
    public Button popupConfirmButton; // "Đi nào"

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public Button dialogueContinueButton;
    public DialogueAudioSync dialogueAudioSync;

    [Header("UI Resources")]
    public Sprite normalSprite;   // Trạng thái thường  
    public Sprite selectedSprite; // Trạng thái được chọn

    // State management
    private int selectedIndex = 0;
    private int infoStep = 0;
    private GameObject currentSelectionInstance;
    private GameObject currentInfoInstance;

    // State cho hệ thống description nhiều đoạn
    private int _currentDescriptionIndex = 0;
    private string _currentFullDescription = "";

    const string KEY_SELECTED_COMPANION = "SelectedCompanion";

    void Start()
    {
        PlayerDataManager.Instance.RegisterCompanionData(companionDataArray);

        SetupInfoListeners();
        SetupSelectionListeners();

        selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, 0);

        dialoguePanel.SetActive(false);
        if (dialogueContinueButton)
        {
            dialogueContinueButton.onClick.RemoveAllListeners();
            dialogueContinueButton.onClick.AddListener(EndDialogue);
        }

        ShowSelectionPanel();
        selectionPanel.SetActive(false);
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
        for (int i = 0; i < selectionButtons.Length && i < companionDataArray.Length; i++)
        {
            int index = i;
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
        PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, selectedIndex);
        PlayerPrefs.Save();

        SpawnPreviewAt(ref currentSelectionInstance, selectionDisplayParent,
                       companionDataArray[selectedIndex].previewPrefab);

        if (selectionNameText)
            selectionNameText.text = companionDataArray[selectedIndex].characterName;

        UpdateSelectionButtonStates();

        Debug.Log($"[CompanionSelection] Selected: {companionDataArray[selectedIndex].characterName}");
    }

    void UpdateSelectionButtonStates()
    {
        for (int i = 0; i < selectionButtons.Length; i++)
        {
            if (i >= companionDataArray.Length) continue;

            var button = selectionButtons[i];
            bool isSelected = (i == selectedIndex);

            if (button.image != null)
                button.image.sprite = isSelected ? selectedSprite : normalSprite;

            if (characterIcons != null && i < characterIcons.Length && characterIcons[i] != null)
            {
                characterIcons[i].color = isSelected
                    ? Color.white
                    : new Color(0.4f, 0.4f, 0.4f, 1f);
            }

            button.transform.localScale = isSelected ? Vector3.one * 1.1f : Vector3.one;

            var colors = button.colors;
            colors.normalColor = Color.white;
            button.colors = colors;
        }
    }

    void GoToInfoPanel()
    {
        PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, selectedIndex);
        PlayerPrefs.Save();

        selectionPanel.SetActive(false);
        ShowInfoPanel();
    }

    void ShowSelectionPanel()
    {
        if (infoPanel) infoPanel.SetActive(false);
        if (selectionPanel) selectionPanel.SetActive(true);

        selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, selectedIndex);

        SpawnPreviewAt(ref currentSelectionInstance, selectionDisplayParent,
                       companionDataArray[selectedIndex].previewPrefab);

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

        if (btContinue != null)
        {
            btContinue.onClick.RemoveAllListeners();
            btContinue.onClick.AddListener(OnContinueDescriptionClicked);
        }
    }

    void ShowInfoPanel()
    {
        infoPanel.SetActive(true);

        selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, selectedIndex);

        SpawnPreviewAt(ref currentInfoInstance, infoDisplayParent,
                       companionDataArray[selectedIndex].previewPrefab);

        // Reset description sequence
        _currentDescriptionIndex = 0;

        // Ẩn 2 nút action khi mới vào info panel
        if (changeCompanionButton) changeCompanionButton.gameObject.SetActive(false);
        if (infoContinueButton) infoContinueButton.gameObject.SetActive(false);

        // Hiện nút skip
        if (btContinue) btContinue.gameObject.SetActive(true);

        UpdateInfoTexts();
        ShowDescription(_currentDescriptionIndex);
    }

    void UpdateInfoTexts()
    {
        var companionData = companionDataArray[selectedIndex];

        if (infoNameText)
            infoNameText.text = companionData.characterName;
    }

    // Hiển thị 1 đoạn description theo index, có typewriter
    void ShowDescription(int index)
    {
        var companionData = companionDataArray[selectedIndex];

        if (companionData.description == null || companionData.description.Length == 0)
        {
            Debug.LogWarning($"[CompanionSelection] description array is empty for {companionData.characterName}");
            ShowFinalInfoButtons();
            return;
        }

        if (index < 0 || index >= companionData.description.Length)
        {
            Debug.LogWarning($"[CompanionSelection] description index {index} out of range");
            return;
        }

        _currentFullDescription = companionData.description[index];

        if (npcDescriptionText != null)
            npcDescriptionText.text = "";

        if (dialogueAudioSync != null)
            dialogueAudioSync.StartTypewriter(npcDescriptionText, _currentFullDescription);
    }

    // Logic 2 bước: nếu đang typing → skip về full text; nếu đã xong → sang đoạn tiếp
    void OnContinueDescriptionClicked()
    {
        bool isRunning = (dialogueAudioSync != null && dialogueAudioSync.IsTypewriting());

        if (isRunning)
        {
            // Bước 1: đang typewriter → hiện full text ngay
            if (npcDescriptionText != null)
                npcDescriptionText.text = _currentFullDescription;

            dialogueAudioSync.StopTypewriter();
            return;
        }

        // Bước 2: sang đoạn tiếp theo
        var companionData = companionDataArray[selectedIndex];
        int lastIndex = (companionData.description != null)
            ? companionData.description.Length - 1
            : -1;

        if (_currentDescriptionIndex >= lastIndex)
        {
            // Đã ở đoạn cuối → hiện 2 nút action, ẩn btContinue
            ShowFinalInfoButtons();
            return;
        }

        _currentDescriptionIndex++;
        ShowDescription(_currentDescriptionIndex);
    }

    void ShowFinalInfoButtons()
    {
        if (btContinue) btContinue.gameObject.SetActive(false);

        if (changeCompanionButton) changeCompanionButton.gameObject.SetActive(true);
        if (infoContinueButton) infoContinueButton.gameObject.SetActive(true);

        Debug.Log("[CompanionSelection] All descriptions shown. Final buttons revealed.");
    }
    #endregion

    #region Confirmation Popup


    void ConfirmAndStart()
    {
        PlayerDataManager.Instance.SaveCompanionIndex(selectedIndex);
        Debug.Log($"[CompanionSelection] Starting game with companion: {companionDataArray[selectedIndex].characterName}");

        if (dialogueAudioSync != null)
            dialogueAudioSync.StopTypewriter();

        var syncer = FindFirstObjectByType<PlayerApiService>();
        if (syncer != null)
            syncer.SyncSelectionToServer();
        else
            Debug.LogWarning("[CompanionSelection] PlayerSelectionSync not found in scene!");

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

        if (holder != null)
            DestroyImmediate(holder);

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
