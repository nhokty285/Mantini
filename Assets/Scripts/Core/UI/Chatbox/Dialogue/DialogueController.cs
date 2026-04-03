using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hệ thống quản lý dialogue cho các NPC khác nhau
/// Tự động mở shop sau khi dialogue kết thúc
/// </summary>


public class NPCDialogueSystem : MonoBehaviour
{
    [Header("📍 Singleton")]
    public static NPCDialogueSystem Instance { get; private set; }

    [Header("🎨 UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Image npcImageDisplay;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button continueButton;
    [SerializeField] private CanvasGroup dialoguePanelCanvasGroup;
    [SerializeField] private DialogueAudioSync audioSync;
    [Header("⚙️ Settings")]
    [SerializeField] private float textRevealSpeed = 0.05f; // Tốc độ hiển thị chữ
    [SerializeField] private float fadeDuration = 0.3f; // Thời gian fade in/out
    [SerializeField] private bool useTypewriterEffect = true;
    [SerializeField] private float delayBeforeAutoOpen = 0.5f; // Delay trước khi auto-open shop

    // Internal state
    private BaseNPC currentNPC;
    private List<DialogueEntry> currentDialogueSequence = new List<DialogueEntry>();
    private int currentDialogueIndex = 0;
    private Coroutine typewriterCoroutine;
    private bool isWaitingForInput = false;
    private ShopData cachedShopData; // Lưu shop data để gọi sau khi dialogue kết thúc

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

    }

    private void Start()
    {
        InitializeUI();

    }

    /// <summary>
    /// Khởi tạo UI ban đầu
    /// </summary>
    private void InitializeUI()
    {
        if (dialoguePanel == null)
        {
            Debug.LogError("❌ Dialogue Panel không được assign!");
            return;
        }

        // Ẩn panel ban đầu
        dialoguePanel.SetActive(false);

        // Setup button listener
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        // Setup canvas group (cho fade effect)
        if (dialoguePanelCanvasGroup == null)
        {
            dialoguePanelCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
        }

        if (dialoguePanelCanvasGroup != null)
        {
            dialoguePanelCanvasGroup.alpha = 0f;
        }
        audioSync = FindAnyObjectByType<DialogueAudioSync>();
    }

    /// <summary>
    /// Bắt đầu dialogue với NPC
    /// Được gọi từ MainMenuView khi button_talk được nhấn
    /// </summary>
    public void StartDialogue(BaseNPC npc, ShopData shopDataForLater = null)
    {
        if (npc == null)
        {
            Debug.LogError("❌ NPC is NULL!");
            return;
        }
        currentNPC = npc;
        cachedShopData = shopDataForLater;
        currentDialogueIndex = 0;

        // Lấy dialogue sequence từ NPC
        currentDialogueSequence = new List<DialogueEntry>(npc.GetDialogueSequence());

        if (currentDialogueSequence.Count == 0)
        {
            Debug.LogWarning($"⚠️ {npc.GetNPCName()} không có dialogue sequence!");
            EndDialogue();
            return;
        }

        Debug.Log($"🎬 Starting dialogue with {npc.GetNPCName()} ({currentDialogueSequence.Count} lines)");

        // Hiển thị panel và bắt đầu dialogue
        StartCoroutine(ShowDialoguePanel());
    }

    /// <summary>
    /// Fade in và hiển thị dialogue
    /// </summary>
    private IEnumerator ShowDialoguePanel()
    {
        dialoguePanel.SetActive(true);
        PlayerController.Instance?.SetCanMove(false);
        // Fade in
        yield return StartCoroutine(FadeCanvasGroup(dialoguePanelCanvasGroup, 0f, 1f, fadeDuration));

        // Hiển thị dialogue đầu tiên
        DisplayCurrentDialogue();
    }

    /// <summary>
    /// Hiển thị dialogue hiện tại
    /// </summary>
    private void DisplayCurrentDialogue()
    {
        if (currentDialogueIndex >= currentDialogueSequence.Count)
        {
            // Đã hết dialogue
            EndDialogue();        
            return;
        }
        DialogueEntry entry = currentDialogueSequence[currentDialogueIndex];
        
        // Update UI
        npcNameText.text = entry.speakerName;

        // Swap sprite + apply RectTransform theo từng entry
        if (npcImageDisplay != null)
        {
            if (entry.npcImage != null)
            {
                npcImageDisplay.sprite = entry.npcImage;
                npcImageDisplay.enabled = true;

                // ✅ Apply RectTransform từ imageLayout
                RectTransform rt = npcImageDisplay.rectTransform;
                rt.anchoredPosition = entry.imageLayout.anchoredPosition;
                rt.sizeDelta = entry.imageLayout.size;
            }
            else
            {
                npcImageDisplay.enabled = false;
            }
        }
        /* // Reset dialogue text
         dialogueText.text = "";
         isWaitingForInput = false;

         // Hiển thị text với effect
         if (useTypewriterEffect)
         {
             // Dừng coroutine cũ nếu có
             if (typewriterCoroutine != null)
                 StopCoroutine(typewriterCoroutine);

             typewriterCoroutine = StartCoroutine(TypewriterEffect(entry.dialogueText));
         }
         else
         {
             dialogueText.text = entry.dialogueText;
             isWaitingForInput = true;
         }*/
        AudioManager.Instance.PlayDialogue(entry.sfxOnStart);


        // Reset state
        isWaitingForInput = false;

   
        // --- PHẦN TÍCH HỢP MỚI ---
        if (audioSync != null)
        {
            // Gọi DialogueAudioSync chạy chữ + tiếng
            // Truyền: [Target Text UI], [Nội dung], [Âm thanh (null nếu ko có riêng)]
            audioSync.StartTypewriter(dialogueText, entry.dialogueText);
        }
        else
        {
            // Fallback nếu không dùng effect hoặc quên gắn script
            dialogueText.text = entry.dialogueText;
            isWaitingForInput = true;
        }
    }

  /*  private IEnumerator TypewriterEffect(string fullText)
    {
        dialogueText.text = "";

        foreach (char c in fullText)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(textRevealSpeed);
        }

        isWaitingForInput = true;
    }

    /// <summary>
    /// Xử lý khi player nhấn continue button hoặc màn hình
    /// </summary>*/
    private void OnContinueButtonClicked()
    {

        // Kiểm tra xem AudioSync có đang chạy chữ không
        if (audioSync != null && audioSync.IsTypewriting())
        {
            // Nếu đang chạy -> Skip để hiện hết chữ ngay lập tức
             dialogueText.text = currentDialogueSequence[currentDialogueIndex].dialogueText;
            audioSync.StopTypewriter();
            isWaitingForInput = true;
            return;
        }

        // Nếu đã hiện hết chữ (isWaitingForInput = true) -> Qua câu tiếp theo
        currentDialogueIndex++;

        if (currentDialogueIndex >= currentDialogueSequence.Count)
        {
            EndDialogue();
        }
        else
        {
            DisplayCurrentDialogue();
        }
    }

    /// <summary>
    /// Kết thúc dialogue và mở shop tự động
    /// </summary>
    private void EndDialogue()
    {
        Debug.Log($"✅ Dialogue ended with {currentNPC.GetNPCName()}");
     
        StartCoroutine(HideDialogueAndOpenShop());
    }

    /// <summary>
    /// Fade out, ẩn panel, và mở shop tự động
    /// </summary>
    private IEnumerator HideDialogueAndOpenShop()
    {
        // Fade out dialogue panel
        if (dialoguePanelCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(dialoguePanelCanvasGroup, 1f, 0f, fadeDuration));
        }

        dialoguePanel.SetActive(false);

        // ✅ FIX: Release dialogue lock trước khi mở shop
        PlayerController.Instance?.SetCanMove(true);

        // ✅ CHÍNH: Tự động mở shop thay vì chỉ show button
        if (MainMenuView.Instance != null && currentNPC != null)
        {
            Debug.Log($"🎬 Auto-opening shop for {currentNPC.GetNPCName()}");

            // Gọi SetNPCInteraction để setup state
            MainMenuView.Instance.SetNPCInteraction(
                true,
                currentNPC.GetNPCName(),
                cachedShopData,
                currentNPC
            );

            // Delay nhẹ cho mọi thứ setup xong
            yield return new WaitForSeconds(delayBeforeAutoOpen);

            // ✅ NEW: Tự động bấm shop button
            MainMenuView.Instance.AutoOpenShop();
        }

        // Reset state
        currentNPC = null;
        currentDialogueSequence.Clear();
        cachedShopData = null;
    }

    /// <summary>
    /// Helper function: Fade canvas group
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        canvasGroup.alpha = startAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
    }

    /// <summary>
    /// Cho phép bấm vào màn hình để tiếp tục (alternative to button)
    /// </summary>
    private void Update()
    {
        if (!dialoguePanel.activeInHierarchy)
            return;

        // Bấm bất kỳ chỗ nào trên màn hình
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            OnContinueButtonClicked();
        }
    }
}

/// <summary>
/// Data class cho một dòng dialogue
/// </summary>
[System.Serializable]
public class DialogueEntry
{
    public string speakerName = "NPC";
    public string dialogueText = "Hello!";
    public Sprite npcImage = null;
    public float displayDuration = 0f; // 0 = chờ input, >0 = auto-next sau n giây
    public AudioClip sfxOnStart;

    // ✅ THÊM: Layout riêng cho từng image
    public ImageLayout imageLayout = ImageLayout.Default;

    public DialogueEntry(string name, string text, Sprite image = null, float duration = 0f, ImageLayout? layout = null)
    {
        speakerName = name;
        dialogueText = text;
        npcImage = image;
        displayDuration = duration;
        imageLayout = layout ?? ImageLayout.Default;
    }
}

