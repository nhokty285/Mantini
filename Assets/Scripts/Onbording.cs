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

        if (continueChatButton != null)
            continueChatButton.onClick.AddListener(OnContinueButtonClicked);

        audioSync = FindAnyObjectByType<DialogueAudioSync>();

        continueButton.onClick.AddListener(() =>
        {
            SkipOnboarding();
            profileData.SaveProfile();
        });

        if (voiceNameInput != null)
            voiceNameInput.OnSpeechResult += (name) => nameInputField.text = name;
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

        if (characterChat != null)
            characterChat.SetActive(true);
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
            dialogueText.text = _currentFullText;
            UpdateContinueButtonText("Tiếp tục");
            audioSync.StopTypewriter();
        }
        else
        {
            _currentDialogueIndex++;
            ShowDialogue(_currentDialogueIndex);
        }
    }

    // Kết thúc onboarding
    private void EndOnboarding()
    {
        Debug.Log("[Onboarding] Onboarding completed!");
        audioSync.StopTypeSound();
        LevelLoader.Instance.ShowLoadingThenSwitch(onboardingPanel, characterSelectionPanel, 1.5f);

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
