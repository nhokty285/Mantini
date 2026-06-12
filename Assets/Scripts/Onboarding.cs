using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Onboarding : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject onboardingPanel;
    [SerializeField] private Image npcImage;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button continueChatButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private ProfileData profileData;

    [Header("Character Chat")]
    [SerializeField] private GameObject characterChat;

    [Header("NPC Images")]
    [SerializeField] private Sprite[] npcSprites;

    [System.Serializable]
    public class DialogueData
    {
        [TextArea(3, 5)]
        public string dialogue;
        public int npcSpriteIndex;
    }

    [Header("Dialogue Settings")]
    [SerializeField] private float typingSpeed = 0.05f;

    [Header("Name Input (sau onboarding)")]
    [SerializeField] private VoiceNameInput voiceNameInput;
    [SerializeField] private TMP_InputField nameInputField;

    [Header("Dialogues")]
    [SerializeField] private DialogueData[] dialogues;

    private int _currentDialogueIndex = 0;
    private bool _isTyping = false;
    private string _currentFullText = "";

    [SerializeField] private DialogueAudioSync audioSync;
    [SerializeField] private AudioClip helloSound;

    void Start()
    {
        if (onboardingPanel != null)
            onboardingPanel.SetActive(false);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        if (characterChat != null)
            characterChat.SetActive(false);

        // ✅ FIX: Ẩn continueButton ngay từ đầu.
        // Nút này chỉ hiện khi toàn bộ dialogue chạy xong cùng lúc với characterChat.
        // Trước đây nút luôn visible → player có thể nhấn skip dialogue sớm.
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (continueChatButton != null)
            continueChatButton.onClick.RemoveAllListeners();
        if (continueChatButton != null)
            continueChatButton.onClick.AddListener(OnContinueButtonClicked);

        audioSync = FindAnyObjectByType<DialogueAudioSync>();

        // Đăng ký callback khi typewriter chạy xong
        if (audioSync != null)
            audioSync.OnTypewriterComplete += OnTypewriterFinished;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() =>
            {
                SkipOnboarding();
                profileData.SaveProfile();
            });
        }

        if (voiceNameInput != null)
            voiceNameInput.OnSpeechResult += (name) => nameInputField.text = name;
    }

    private void OnDestroy()
    {
        // Hủy đăng ký để tránh memory leak
        if (audioSync != null)
            audioSync.OnTypewriterComplete -= OnTypewriterFinished;
    }

    // Callback: được gọi tự động khi typewriter chạy xong hoàn toàn
    private void OnTypewriterFinished()
    {
        UpdateContinueButtonText("Tiếp tục");

        // Nếu đây là dialogue cuối, tự động hoàn thành không cần nhấn nút
        bool isLastDialogue = (_currentDialogueIndex >= dialogues.Length - 1);
        if (isLastDialogue)
        {
            Debug.Log("[Onboarding] Last dialogue finished. Auto-completing.");
            OnAllDialoguesCompleted();
        }
    }

    // Hiển thị đoạn hội thoại
    public void ShowDialogue(int index)
    {
        if (index >= dialogues.Length)
        {
            OnAllDialoguesCompleted();
            return;
        }

        DialogueData currentDialogue = dialogues[index];
        AudioManager.Instance.PlayDialogue(helloSound, 0.6f);

        if (npcImage != null && npcSprites != null && currentDialogue.npcSpriteIndex < npcSprites.Length)
            npcImage.sprite = npcSprites[currentDialogue.npcSpriteIndex];

        StopAllCoroutines();
        UpdateContinueButtonText("...");

        if (audioSync != null)
            audioSync.StartTypewriter(dialogueText, currentDialogue.dialogue);
    }

    // Gọi khi tất cả dialogue đã chạy xong
    private void OnAllDialoguesCompleted()
    {
        Debug.Log("[Onboarding] All dialogues completed. Showing characterChat.");

        // ✅ FIX: Hiện characterChat và continueButton cùng lúc.
        // Trước đây continueButton luôn visible từ đầu nên player skip được sớm.
        if (characterChat != null)
            characterChat.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);
    }

    // Cập nhật text của nút Continue
    private void UpdateContinueButtonText(string text)
    {
        if (continueChatButton != null)
        {
            TextMeshProUGUI buttonText = continueChatButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
                buttonText.text = text;
        }
    }

    // Xử lý khi nhấn nút Continue
    private void OnContinueButtonClicked()
    {
        bool isRunning = (audioSync != null && audioSync.IsTypewriting());

        if (isRunning)
        {
            // Đang chạy: skip về full text, OnTypewriterFinished sẽ KHÔNG được gọi tự nhiên
            // vì StopTypewriter() cancel coroutine trước khi nó kết thúc
            dialogueText.text = _currentFullText;
            UpdateContinueButtonText("Tiếp tục");
            audioSync.StopTypewriter();

            // Nếu là dialogue cuối, tự động hoàn thành luôn
            bool isLastDialogue = (_currentDialogueIndex >= dialogues.Length - 1);
            if (isLastDialogue)
            {
                Debug.Log("[Onboarding] Last dialogue skipped. Auto-completing.");
                OnAllDialoguesCompleted();
            }
        }
        else
        {
            // Typewriter đã xong, chuyển sang dialogue tiếp theo
            _currentDialogueIndex++;
            ShowDialogue(_currentDialogueIndex);
        }
    }

    // Kết thúc onboarding
    private void EndOnboarding()
    {
        Debug.Log("[Onboarding] Onboarding completed!");
        audioSync.StopTypeSound();
        LevelLoader.Instance.ShowLoadingThenSwitch(onboardingPanel, characterSelectionPanel, 2f);

        if (onboardingPanel != null)
            onboardingPanel.SetActive(false);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);
    }

    // Hàm khởi động onboarding
    public void StartOnboarding()
    {
        _currentDialogueIndex = 0;

        if (onboardingPanel != null)
            onboardingPanel.SetActive(true);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        ShowDialogue(_currentDialogueIndex);
    }

    // Hàm bỏ qua onboarding
    public void SkipOnboarding()
    {
        StopAllCoroutines();
        EndOnboarding();
    }
}
