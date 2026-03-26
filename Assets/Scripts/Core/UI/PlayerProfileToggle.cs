using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn vào bất kỳ GameObject nào trên HUD Scene 2.
/// Click avatar button → toggle hiển thị Profile Panel, tự động load dữ liệu mới nhất từ server khi mở.
/// </summary>
public class PlayerProfileToggle : MonoBehaviour
{
    [Header("Avatar Button")]
    [SerializeField] private Button avatarButton;
    [SerializeField] private Image avatarButtonIcon; // Image icon trên avatarButton
    [SerializeField] private Button closeButton;
    [SerializeField] private Button saveButton;

    [Header("Scene 2 Profile Panel")]
    [SerializeField] private GameObject profilePanel;


    [SerializeField] private ProfileData profileData;

    private void Start()
    {
        // Tìm ProfileData persist từ Scene 1 (DontDestroyOnLoad)
        profileData = FindAnyObjectByType<ProfileData>();
        if (profileData == null)
        {
            Debug.LogError("[PlayerProfileToggle] Không tìm thấy ProfileData!");
            return;
        }


        if (avatarButtonIcon != null)
        {
            // Gán ngay nếu đã có cache (ProfileData load trước)
            if (profileData.AvatarSprite != null)
                avatarButtonIcon.sprite = profileData.AvatarSprite;

            profileData.OnAvatarChanged += sprite => avatarButtonIcon.sprite = sprite;
        }

        if (avatarButton != null)
            avatarButton.onClick.AddListener(TogglePanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        if (saveButton != null)
            saveButton.onClick.AddListener(() => { profileData.SaveProfile(); ClosePanel(); });

        profilePanel.gameObject.SetActive(false);

        PlayerController.OnMovementStateChanged += OnMovementStateChanged;
        if (avatarButton != null)
            avatarButton.interactable = PlayerController.Instance == null || true;
    }

    private void OnDestroy()
    {
        PlayerController.OnMovementStateChanged -= OnMovementStateChanged;
    }

    private void OnMovementStateChanged(bool canMove)
    {
        if (avatarButton != null)
            avatarButton.interactable = canMove;
    }

    public void TogglePanel()
    {
        if (profilePanel == null) return;

        /*    bool willOpen = !profilePanel.activeSelf;
            profilePanel.SetActive(willOpen);
            PlayerController.Instance?.SetCanMove(!willOpen);*/

        profilePanel.SetActive(true);
        PlayerController.Instance?.SetCanMove(false);
        profileData?.LoadProfile();
    }

    public void ClosePanel()
    {
        profilePanel?.SetActive(false);
        PlayerController.Instance?.SetCanMove(true);
    }
}
