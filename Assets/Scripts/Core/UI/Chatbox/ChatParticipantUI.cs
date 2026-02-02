using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatParticipantUI : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject onlineIndicator;
    [SerializeField] private Button selectButton;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite companionAvatar;
    [SerializeField] private Sprite vendorAvatar;
    [SerializeField] private Sprite defaultAvatar;

    private IChatParticipant participant;
    private bool isSelected = false;

    public void Setup(IChatParticipant participant)
    {
        this.participant = participant;

        if (participant == null) return;

        // Set name
        if (nameText != null)
            nameText.text = participant.GetParticipantName();

        // Set avatar based on participant type
        SetAvatarByType(participant.GetParticipantType());

        // Set initial online status
        UpdateOnlineStatus(participant.IsActive());

        // Setup click event
        if (selectButton != null)
            selectButton.onClick.AddListener(OnParticipantClicked);

        Debug.Log($"✅ Setup UI for {participant.GetParticipantName()}");
    }

    private void SetAvatarByType(ChatParticipantType type)
    {
        if (avatarImage == null) return;

        Sprite targetSprite = type switch
        {
            ChatParticipantType.Companion => companionAvatar,
            ChatParticipantType.VendorNPC => vendorAvatar,
            _ => defaultAvatar
        };

        avatarImage.sprite = targetSprite ?? defaultAvatar;
    }

    private void OnParticipantClicked()
    {
        if (participant == null) return;

        // Set as target recipient for private messages
        var multiChatManager = FindFirstObjectByType<MultiChatManager>();
        if (multiChatManager != null)
        {
            // TODO: Implement SetTargetRecipient method
            Debug.Log($"Selected participant: {participant.GetParticipantName()}");
        }

        SetSelected(true);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        // Visual feedback for selection
        if (avatarImage != null)
        {
            avatarImage.color = selected ? Color.yellow : Color.white;
        }

        // You can add more visual feedback here
        transform.localScale = selected ? Vector3.one * 1.1f : Vector3.one;
    }

    public void UpdateOnlineStatus(bool isOnline)
    {
        if (onlineIndicator != null)
            onlineIndicator.SetActive(isOnline);

        // Gray out if offline
        if (avatarImage != null)
        {
            avatarImage.color = isOnline ? Color.white : Color.gray;
        }
    }
}
