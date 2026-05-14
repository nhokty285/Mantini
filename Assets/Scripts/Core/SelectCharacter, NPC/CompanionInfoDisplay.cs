using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class CompanionInfoDisplay : MonoBehaviour
{
    [Header("Companion Display")]
    public Transform companionDisplayPosition; // Vị trí hiển thị companion model
    public GameObject[] companionPrefabs; // Array companion prefabs

    [Header("Companion Info")]
    public TextMeshProUGUI companionGreetingText; // Text "Xin chào mình là Max, mình sẽ đồng hành cùng bạn"
    public TextMeshProUGUI companionNameText; // Text hiển thị tên companion
    public TextMeshProUGUI npcDescriptionText; // Text mô tả NPC và thông tin

    [Header("UI Elements")]
    public Button changeCompanionButton; // Nút "Đổi companion" 
    public Button continueButton; // Nút "Tiếp tục"

    [Header("Popup")]
    public GameObject confirmationPopup; // Popup xác nhận
    public Button confirmYesButton; // Nút "Khoan đã" 
    public Button confirmNoButton; // Nút "Đi nào"
    public TextMeshProUGUI popupMessageText; // Text trong popup

    [Header("Panels")]
    public GameObject companionInfoPanel; // Panel hiện tại
    public GameObject companionSelectionPanel; // Panel CompanionSelection

    private GameObject currentCompanionInstance;
    private int selectedCompanionIndex;

    void Start()
    {
        InitializeCompanionInfo();
        SetupButtonListeners();
        LoadSelectedCompanion();
    }
    void OnEnable()
    {
        // THÊM: Auto refresh khi panel được kích hoạt
        Debug.Log("[CompanionInfoDisplay] OnEnable - Auto refreshing companion info");
        RefreshCompanionInfo();
    }

    void InitializeCompanionInfo()
    {
        // Lấy companion được chọn từ CompanionSelection
        selectedCompanionIndex = PlayerPrefs.GetInt("SelectedCompanion", 0);

        // Setup popup message
        if (popupMessageText != null)
        {
            popupMessageText.text = "Nào mình cùng bắt đầu Shoppin nhé! :D";
        }

        // Ẩn popup ban đầu
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }
    }

    void SetupButtonListeners()
    {
        // Button để đổi companion
        if (changeCompanionButton != null)
        {
            changeCompanionButton.onClick.AddListener(OnChangeCompanionClicked);
        }

        // Button tiếp tục
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        // Popup buttons
        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.AddListener(OnConfirmNoClicked);
        }
    }

    /* void LoadSelectedCompanion()
     {
         // Hiển thị companion được chọn
         DisplayCompanion(selectedCompanionIndex);

         // Cập nhật thông tin companion
         UpdateCompanionInfo();
     }*/

    void LoadSelectedCompanion()
    {
        // Lấy companion index từ PlayerPrefs
        selectedCompanionIndex = PlayerPrefs.GetInt("SelectedCompanion", 0);
        Debug.Log($"[CompanionInfoDisplay] Loading companion index {selectedCompanionIndex}");

        // Kiểm tra index hợp lệ
        if (selectedCompanionIndex >= companionPrefabs.Length)
        {
            Debug.LogError($"[CompanionInfoDisplay] Invalid companion index {selectedCompanionIndex}, resetting to 0");
            selectedCompanionIndex = 0;
            PlayerPrefs.SetInt("SelectedCompanion", 0);
            PlayerPrefs.Save();
        }

        DisplayCompanion(selectedCompanionIndex);
        UpdateCompanionInfo();
    }

    // QUAN TRỌNG: Method này được gọi từ CompanionSelection
    public void RefreshCompanionInfo()
    {
        Debug.Log("[CompanionInfoDisplay] RefreshCompanionInfo() called");

        // Lấy lại companion index mới nhất từ PlayerPrefs
        int newIndex = PlayerPrefs.GetInt("SelectedCompanion", 0);

        if (newIndex != selectedCompanionIndex || currentCompanionInstance == null)
        {
            Debug.Log($"[CompanionInfoDisplay] Companion changed from {selectedCompanionIndex} to {newIndex}");
            selectedCompanionIndex = newIndex;
            LoadSelectedCompanion();
        }
        else
        {
            Debug.Log($"[CompanionInfoDisplay] Companion unchanged (index {selectedCompanionIndex})");
        }
    }


    void DisplayCompanion(int companionIndex)
    {
        // Xóa companion instance cũ
        if (currentCompanionInstance != null)
        {
            DestroyImmediate(currentCompanionInstance);
        }

        // Tạo companion instance mới
        if (companionIndex < companionPrefabs.Length)
        {
            currentCompanionInstance = Instantiate(companionPrefabs[companionIndex], companionDisplayPosition);
            currentCompanionInstance.transform.localPosition = Vector3.zero;
            currentCompanionInstance.transform.localScale = Vector3.one;
        }
    }

    void UpdateCompanionInfo()
    {
        if (selectedCompanionIndex < companionPrefabs.Length)
        {
            string companionName = companionPrefabs[selectedCompanionIndex].name;

            // Cập nhật greeting text
            if (companionGreetingText != null)
            {
                companionGreetingText.text = $"Xin chào mình là {companionName}, mình sẽ đồng hành cùng bạn";
            }

            // Cập nhật companion name
            if (companionNameText != null)
            {
                companionNameText.text = $"Character\nCompanion\n{companionName}";
            }

            // Cập nhật NPC description
            if (npcDescriptionText != null)
            {
                npcDescriptionText.text = GetCompanionDescription(selectedCompanionIndex);
            }
        }
    }

    string GetCompanionDescription(int companionIndex)
    {
        // Mô tả cho từng companion
        string[] descriptions = {
            "NPC\nText ingame Text ingame Text ingame Text ingame\nText ingame Text ingame Text ingame Text ingame\nText ingame Text ingame Text ingame Text ingame",
            "NPC\nMô tả companion thứ hai với những đặc điểm riêng biệt\nVà thông tin chi tiết về khả năng và tính cách\nCủa companion này trong game",
            "NPC\nCompanion thứ ba với phong cách và khả năng độc đáo\nMang lại trải nghiệm khác biệt cho người chơi\nVới những tính năng đặc biệt của riêng mình"
        };

        if (companionIndex < descriptions.Length)
        {
            return descriptions[companionIndex];
        }

        return "NPC\nThông tin companion\nMô tả chi tiết về companion";
    }

    void OnChangeCompanionClicked()
    {
        Debug.Log("Chuyển về Companion Selection để đổi companion");

        // Ẩn panel hiện tại
        if (companionInfoPanel != null)
        {
            companionInfoPanel.SetActive(false);
        }

        // Hiển thị panel CompanionSelection
        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(true);
        }
    }

    void OnContinueClicked()
    {
        Debug.Log("Hiển thị popup xác nhận");

        // Hiển thị popup
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(true);
        }
    }

    void OnConfirmYesClicked()
    {
        Debug.Log("User chọn Khoan đã - Ẩn popup");

        // Ẩn popup
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }
    }

    void OnConfirmNoClicked()
    {
        Debug.Log("User chọn Đi nào - Bắt đầu game");

        // Ẩn popup
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }

        // Bắt đầu game
        StartGame();
    }

    void StartGame()
    {
        Debug.Log("Bắt đầu game với companion đã chọn!");

        // Lưu trạng thái hoàn thành setup
        PlayerPrefs.SetInt("SetupCompleted", 1);
        PlayerPrefs.Save();

        // TODO: Load game scene hoặc activate game UI
        // SceneManager.LoadScene("GameScene");
    }
}
