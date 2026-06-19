using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CompanionSelection : MonoBehaviour
{
    [Header("Companion Data")]
    public CharacterData[] companionDataArray;

    [Header("Display Parents")]
    public Transform selectionDisplayParent;
    public Transform infoDisplayParent;

    [Header("Selection UI")]
    public GameObject selectionPanel;
    public Button[] selectionButtons;
    public TextMeshProUGUI selectionNameText;
    public Button selectionContinueButton;
    public Image[] characterIcons;

    [Header("Info UI")]
    public GameObject infoPanel;
    public TextMeshProUGUI greetingText;
    public TextMeshProUGUI infoNameText;
    public TextMeshProUGUI npcDescriptionText;
    public Button changeCompanionButton;
    public Button infoContinueButton;

    [Header("Description Skip Button")]
    [SerializeField] private Button btContinue;

    [Header("Confirmation Popup")]
    public GameObject confirmationPopup;
    public TextMeshProUGUI popupMessageText;
    public Button popupCancelButton;
    public Button popupConfirmButton;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public Button dialogueContinueButton;
    public DialogueAudioSync dialogueAudioSync;

    [Header("UI Resources")]
    public Sprite normalSprite;
    public Sprite selectedSprite;

    // ── State ─────────────────────────────────────────────────────────────────
    private int _selectedIndex = 0;
    private GameObject _currentSelectionInstance;
    private GameObject _currentInfoInstance;

    private int _currentDescriptionIndex = 0;
    private string _currentFullDescription = "";

    private const string KEY_SELECTED_COMPANION = "SelectedCompanion";

    private void Start()
    {
        PlayerDataManager.Instance.RegisterCompanionData(companionDataArray);

        SetupInfoListeners();
        SetupSelectionListeners();

        _selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, 0);

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dialogueContinueButton != null)
        {
            dialogueContinueButton.onClick.RemoveAllListeners();
            dialogueContinueButton.onClick.AddListener(EndDialogue);
        }

        // Đăng ký callback tự động khi typewriter chạy xong hoàn toàn
        if (dialogueAudioSync != null)
            dialogueAudioSync.OnTypewriterComplete += OnDescriptionTypewriterFinished;

        ShowSelectionPanel();
        if (selectionPanel != null) selectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (dialogueAudioSync != null)
            dialogueAudioSync.OnTypewriterComplete -= OnDescriptionTypewriterFinished;

        CleanupInstances();
    }

    #region Dialogue Logic
    private void EndDialogue()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        ShowInfoPanel();
    }
    #endregion

    #region Selection Panel

    private void SetupSelectionListeners()
    {
        for (int i = 0; i < selectionButtons.Length && i < companionDataArray.Length; i++)
        {
            int index = i; // capture
            selectionButtons[i].onClick.RemoveAllListeners();
            selectionButtons[i].onClick.AddListener(() => OnSelectCompanion(index));
        }

        if (selectionContinueButton != null)
        {
            selectionContinueButton.onClick.RemoveAllListeners();
            selectionContinueButton.onClick.AddListener(GoToInfoPanel);
        }
    }

    private void OnSelectCompanion(int index)
    {
        if (index < 0 || index >= companionDataArray.Length)
        {
            GameLog.Warn($"[CompanionSelection] Invalid index {index}");
            return;
        }

        _selectedIndex = index;
        PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, _selectedIndex);
        PlayerPrefs.Save();

        SpawnPreviewAt(ref _currentSelectionInstance, selectionDisplayParent,
                       companionDataArray[_selectedIndex].previewPrefab);

        if (selectionNameText != null)
            selectionNameText.text = companionDataArray[_selectedIndex].characterName;

        UpdateSelectionButtonStates();
#if UNITY_EDITOR
        GameLog.Info($"[CompanionSelection] Selected: {companionDataArray[_selectedIndex].characterName}");
#endif
    }

    private void UpdateSelectionButtonStates()
    {
        for (int i = 0; i < selectionButtons.Length; i++)
        {
            if (i >= companionDataArray.Length) continue;

            var button = selectionButtons[i];
            bool isSelected = (i == _selectedIndex);

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

    private void GoToInfoPanel()
    {
        PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, _selectedIndex);
        PlayerPrefs.Save();

        if (selectionPanel != null) selectionPanel.SetActive(false);
        ShowInfoPanel();
    }

    private void ShowSelectionPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (selectionPanel != null) selectionPanel.SetActive(true);

        _selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, _selectedIndex);

        SpawnPreviewAt(ref _currentSelectionInstance, selectionDisplayParent,
                       companionDataArray[_selectedIndex].previewPrefab);

        if (selectionNameText != null)
            selectionNameText.text = companionDataArray[_selectedIndex].characterName;

        UpdateSelectionButtonStates();
    }

    #endregion

    #region Info Panel

    private void SetupInfoListeners()
    {
        if (changeCompanionButton != null)
        {
            changeCompanionButton.onClick.RemoveAllListeners();
            changeCompanionButton.onClick.AddListener(ShowSelectionPanel);
        }

        if (infoContinueButton != null)
        {
            infoContinueButton.onClick.RemoveAllListeners();
            infoContinueButton.onClick.AddListener(ConfirmAndStart);
        }

        if (btContinue != null)
        {
            btContinue.onClick.RemoveAllListeners();
            btContinue.onClick.AddListener(OnContinueDescriptionClicked);
        }
    }

    private void ShowInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(true);

        _selectedIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, _selectedIndex);

        SpawnPreviewAt(ref _currentInfoInstance, infoDisplayParent,
                       companionDataArray[_selectedIndex].previewPrefab);

        // Reset description sequence
        _currentDescriptionIndex = 0;

        // Ẩn 2 nút action khi mới vào info panel
        if (changeCompanionButton != null) changeCompanionButton.gameObject.SetActive(false);
        if (infoContinueButton != null) infoContinueButton.gameObject.SetActive(false);

        // Hiện nút skip
        if (btContinue != null) btContinue.gameObject.SetActive(true);

        UpdateInfoTexts();
        ShowDescription(_currentDescriptionIndex);
    }

    private void UpdateInfoTexts()
    {
        var companionData = companionDataArray[_selectedIndex];
        if (infoNameText != null) infoNameText.text = companionData.characterName;
    }

    private void ShowDescription(int index)
    {
        var companionData = companionDataArray[_selectedIndex];

        if (companionData.description == null || companionData.description.Length == 0)
        {
            GameLog.Warn($"[CompanionSelection] description array is empty for {companionData.characterName}");
            ShowFinalInfoButtons();
            return;
        }

        if (index < 0 || index >= companionData.description.Length)
        {
            GameLog.Warn($"[CompanionSelection] description index {index} out of range");
            return;
        }

        _currentFullDescription = companionData.description[index];

        if (npcDescriptionText != null)
            npcDescriptionText.text = "";

        if (dialogueAudioSync != null)
            dialogueAudioSync.StartTypewriter(npcDescriptionText, _currentFullDescription);
    }

    private void OnDescriptionTypewriterFinished()
    {
        var companionData = companionDataArray[_selectedIndex];
        int lastIndex = (companionData.description != null)
            ? companionData.description.Length - 1
            : -1;

        if (_currentDescriptionIndex >= lastIndex)
        {
#if UNITY_EDITOR
            GameLog.Info("[CompanionSelection] Last description finished. Auto-showing final buttons.");
#endif
            ShowFinalInfoButtons();
        }
    }

    private void OnContinueDescriptionClicked()
    {
        bool isRunning = (dialogueAudioSync != null && dialogueAudioSync.IsTypewriting());

        if (isRunning)
        {
            // Bước 1: đang typewriter → hiện full text ngay
            if (npcDescriptionText != null)
                npcDescriptionText.text = _currentFullDescription;

            dialogueAudioSync.StopTypewriter();

            // StopTypewriter cancel coroutine → OnTypewriterComplete không tự gọi → check thủ công
            var companionData = companionDataArray[_selectedIndex];
            int lastIndex = (companionData.description != null)
                ? companionData.description.Length - 1
                : -1;

            if (_currentDescriptionIndex >= lastIndex)
            {
#if UNITY_EDITOR
                GameLog.Info("[CompanionSelection] Last description skipped. Auto-showing final buttons.");
#endif
                ShowFinalInfoButtons();
            }
            return;
        }

        // Bước 2: typewriter đã xong → sang đoạn tiếp theo
        var data = companionDataArray[_selectedIndex];
        int last = (data.description != null) ? data.description.Length - 1 : -1;

        if (_currentDescriptionIndex >= last)
        {
            // Đã ở đoạn cuối mà vẫn nhấn → phòng trường hợp callback bị miss
            ShowFinalInfoButtons();
            return;
        }

        _currentDescriptionIndex++;
        ShowDescription(_currentDescriptionIndex);
    }

    private void ShowFinalInfoButtons()
    {
        if (btContinue != null) btContinue.gameObject.SetActive(false);
        if (changeCompanionButton != null) changeCompanionButton.gameObject.SetActive(true);
        if (infoContinueButton != null) infoContinueButton.gameObject.SetActive(true);
#if UNITY_EDITOR
        GameLog.Info("[CompanionSelection] All descriptions shown. Final buttons revealed.");
#endif
    }
    #endregion

    #region Confirmation Popup

    private void ConfirmAndStart()
    {
        PlayerDataManager.Instance.SaveCompanionIndex(_selectedIndex);
#if UNITY_EDITOR
        GameLog.Info($"[CompanionSelection] Starting game with companion: {companionDataArray[_selectedIndex].characterName}");
#endif

        if (dialogueAudioSync != null)
            dialogueAudioSync.StopTypewriter();

        var syncer = FindFirstObjectByType<PlayerApiService>();
        if (syncer != null)
            syncer.SyncSelectionToServer();
        else
            GameLog.Warn("[CompanionSelection] PlayerApiService not found in scene!");

        if (LevelLoader.Instance != null)
            LevelLoader.Instance.LoadLevel("MapTest2");
    }
    #endregion

    #region Helper Methods

    private static void SpawnPreviewAt(ref GameObject holder, Transform parent, GameObject prefab)
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

    private void CleanupInstances()
    {
        if (_currentSelectionInstance != null) Destroy(_currentSelectionInstance);
        if (_currentInfoInstance != null) Destroy(_currentInfoInstance);
    }

    #endregion
}