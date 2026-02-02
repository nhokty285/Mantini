using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Onboarding : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject onboardingPanel;
    [SerializeField] private Image npcImage;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject characterSelectionPanel;

    [Header("NPC Images")]
    [SerializeField] private Sprite[] npcSprites; // Các sprite NPC khác nhau cho mỗi đoạn hội thoại

    [Header("Dialogue Settings")]
    [SerializeField] private float typingSpeed = 0.05f;

    // Cấu trúc cho mỗi đoạn hội thoại
    [System.Serializable]
    public class DialogueData
    {
        [TextArea(3, 5)]
        public string dialogue;
        public int npcSpriteIndex; // Index của sprite NPC trong mảng npcSprites
    }

    [Header("Dialogues")]
    [SerializeField] private DialogueData[] dialogues;

    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private string currentFullText = "";
    [SerializeField] private DialogueAudioSync audioSync;
    [SerializeField] private AudioClip helloSound;
    void Start()
    {
        // Khởi tạo test
        if (onboardingPanel != null)
            onboardingPanel.SetActive(false);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        // Gắn sự kiện cho nút Continue
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }
        audioSync = FindAnyObjectByType<DialogueAudioSync>();
        //ShowDialogue(currentDialogueIndex);
    }


    // Hiển thị đoạn hội thoại
    public void ShowDialogue(int index)
    {
        if (index >= dialogues.Length)
        {
            // Kết thúc onboarding, mở panel chọn nhân vật
            EndOnboarding();
            return;
        }
        DialogueData currentDialogue = dialogues[index];
        AudioManager.Instance.PlayDialogue(helloSound,0.6f);
        // Cập nhật hình ảnh NPC
        if (npcImage != null && npcSprites != null && currentDialogue.npcSpriteIndex < npcSprites.Length)
        {
            npcImage.sprite = npcSprites[currentDialogue.npcSpriteIndex];
        }
        StopAllCoroutines();
        UpdateContinueButtonText("...");

        if (audioSync != null)
        {
            // Gọi AudioSync chạy chữ (nó sẽ tự lo việc phát tiếng và chạy từng chữ)
            // audioSync.StartTypewriter(UI Text, Nội dung, Âm thanh);
            audioSync.StartTypewriter(dialogueText, currentDialogue.dialogue);
        }


    }

    // Cập nhật text của nút Continue
    private void UpdateContinueButtonText(string text)
    {
        if (continueButton != null)
        {
            TextMeshProUGUI buttonText = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = text;
            }
        }
    }

    // Xử lý khi nhấn nút Continue
    private void OnContinueButtonClicked()
    {
        bool isRunning = (audioSync != null && audioSync.IsTypewriting());

        if (isRunning)
        {
            // Nếu đang typing thì hiển thị hết text luôn
            dialogueText.text = currentFullText;
            UpdateContinueButtonText("Tiếp tục");
            audioSync.StopTypewriter();
        }
        else
        {
            // Chuyển sang đoạn hội thoại tiếp theo
            currentDialogueIndex++;
            ShowDialogue(currentDialogueIndex);
        }
    }

    // Kết thúc onboarding
    private void EndOnboarding()
    {
        Debug.Log("Onboarding completed!");
        audioSync.StopTypeSound();
        // Ẩn panel onboarding
        if (onboardingPanel != null)
            onboardingPanel.SetActive(false);

        // Hiển thị panel chọn nhân vật
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);
    }

    // Hàm khởi động onboarding (gọi sau khi đăng nhập thành công)
    public void StartOnboarding()
    {
        currentDialogueIndex = 0;

        if (onboardingPanel != null)
            onboardingPanel.SetActive(true);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        ShowDialogue(currentDialogueIndex);
    }

    // Hàm bỏ qua onboarding (optional)
    public void SkipOnboarding()
    {
        StopAllCoroutines();
        EndOnboarding();
    }
}
