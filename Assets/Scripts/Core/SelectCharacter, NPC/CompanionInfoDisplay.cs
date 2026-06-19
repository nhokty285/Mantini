using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CompanionInfoDisplay : MonoBehaviour
{
    [Header("Companion Display")]
    public Transform companionDisplayPosition;
    public GameObject[] companionPrefabs;

    [Header("Companion Info")]
    public TextMeshProUGUI companionGreetingText;
    public TextMeshProUGUI companionNameText;
    public TextMeshProUGUI npcDescriptionText;

    [Header("UI Elements")]
    public Button changeCompanionButton;
    public Button continueButton;

    [Header("Popup")]
    public GameObject confirmationPopup;
    public Button confirmYesButton;
    public Button confirmNoButton;
    public TextMeshProUGUI popupMessageText;

    [Header("Panels")]
    public GameObject companionInfoPanel;
    public GameObject companionSelectionPanel;

    private GameObject _currentCompanionInstance;
    private int _selectedCompanionIndex;

    private const string KEY_SELECTED_COMPANION = "SelectedCompanion";

    // Refactor: cache descriptions thành static readonly thay vì alloc array mỗi call GetCompanionDescription
    private static readonly string[] CompanionDescriptions =
    {
        "NPC\nText ingame Text ingame Text ingame Text ingame\nText ingame Text ingame Text ingame Text ingame\nText ingame Text ingame Text ingame Text ingame",
        "NPC\nMô tả companion thứ hai với những đặc điểm riêng biệt\nVà thông tin chi tiết về khả năng và tính cách\nCủa companion này trong game",
        "NPC\nCompanion thứ ba với phong cách và khả năng độc đáo\nMang lại trải nghiệm khác biệt cho người chơi\nVới những tính năng đặc biệt của riêng mình"
    };
    private const string DefaultDescription = "NPC\nThông tin companion\nMô tả chi tiết về companion";

    private void Start()
    {
        InitializeCompanionInfo();
        SetupButtonListeners();
        LoadSelectedCompanion();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] OnEnable - Auto refreshing companion info");
#endif
        RefreshCompanionInfo();
    }

    private void InitializeCompanionInfo()
    {
        _selectedCompanionIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, 0);

        if (popupMessageText != null)
            popupMessageText.text = "Nào mình cùng bắt đầu Shoppin nhé! :D";

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);
    }

    private void SetupButtonListeners()
    {
        // Mantini convention: RemoveAllListeners trước AddListener
        if (changeCompanionButton != null)
        {
            changeCompanionButton.onClick.RemoveAllListeners();
            changeCompanionButton.onClick.AddListener(OnChangeCompanionClicked);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.RemoveAllListeners();
            confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.RemoveAllListeners();
            confirmNoButton.onClick.AddListener(OnConfirmNoClicked);
        }
    }

    private void LoadSelectedCompanion()
    {
        _selectedCompanionIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, 0);
#if UNITY_EDITOR
        GameLog.Info($"[CompanionInfoDisplay] Loading companion index {_selectedCompanionIndex}");
#endif

        if (_selectedCompanionIndex >= companionPrefabs.Length)
        {
            Debug.LogError($"[CompanionInfoDisplay] Invalid companion index {_selectedCompanionIndex}, resetting to 0");
            _selectedCompanionIndex = 0;
            PlayerPrefs.SetInt(KEY_SELECTED_COMPANION, 0);
            PlayerPrefs.Save();
        }

        DisplayCompanion(_selectedCompanionIndex);
        UpdateCompanionInfo();
    }

    public void RefreshCompanionInfo()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] RefreshCompanionInfo() called");
#endif

        int newIndex = PlayerPrefs.GetInt(KEY_SELECTED_COMPANION, 0);

        if (newIndex != _selectedCompanionIndex || _currentCompanionInstance == null)
        {
#if UNITY_EDITOR
            GameLog.Info($"[CompanionInfoDisplay] Companion changed from {_selectedCompanionIndex} to {newIndex}");
#endif
            _selectedCompanionIndex = newIndex;
            LoadSelectedCompanion();
        }
    }

    private void DisplayCompanion(int companionIndex)
    {
        if (_currentCompanionInstance != null)
            DestroyImmediate(_currentCompanionInstance);

        if (companionIndex < companionPrefabs.Length)
        {
            _currentCompanionInstance = Instantiate(companionPrefabs[companionIndex], companionDisplayPosition);
            _currentCompanionInstance.transform.localPosition = Vector3.zero;
            _currentCompanionInstance.transform.localScale = Vector3.one;
        }
    }

    private void UpdateCompanionInfo()
    {
        if (_selectedCompanionIndex >= companionPrefabs.Length) return;

        string companionName = companionPrefabs[_selectedCompanionIndex].name;

        if (companionGreetingText != null)
            companionGreetingText.text = $"Xin chào mình là {companionName}, mình sẽ đồng hành cùng bạn";

        if (companionNameText != null)
            companionNameText.text = $"Character\nCompanion\n{companionName}";

        if (npcDescriptionText != null)
            npcDescriptionText.text = GetCompanionDescription(_selectedCompanionIndex);
    }

    private static string GetCompanionDescription(int companionIndex)
    {
        if (companionIndex >= 0 && companionIndex < CompanionDescriptions.Length)
            return CompanionDescriptions[companionIndex];
        return DefaultDescription;
    }

    private void OnChangeCompanionClicked()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] Chuyển về Companion Selection để đổi companion");
#endif
        if (companionInfoPanel != null) companionInfoPanel.SetActive(false);
        if (companionSelectionPanel != null) companionSelectionPanel.SetActive(true);
    }

    private void OnContinueClicked()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] Hiển thị popup xác nhận");
#endif
        if (confirmationPopup != null) confirmationPopup.SetActive(true);
    }

    private void OnConfirmYesClicked()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] User chọn Khoan đã - Ẩn popup");
#endif
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
    }

    private void OnConfirmNoClicked()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] User chọn Đi nào - Bắt đầu game");
#endif
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
        StartGame();
    }

    private void StartGame()
    {
#if UNITY_EDITOR
        GameLog.Info("[CompanionInfoDisplay] Bắt đầu game với companion đã chọn!");
#endif
        PlayerPrefs.SetInt("SetupCompleted", 1);
        PlayerPrefs.Save();
        // TODO: Load game scene hoặc activate game UI
    }
}