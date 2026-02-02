/*using UnityEngine;
using TMPro;            // Nếu dùng TextMeshPro
using UnityEngine.UI;  // Nếu dùng InputField thường

public class PlayerProfileUI : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField userName;
    [SerializeField] private TMP_InputField mailInput;
    [SerializeField] private TMP_InputField phoneInput;
    [SerializeField] private TMP_InputField avatarInput;

    [Header("Feedback (optional)")]
    [SerializeField] private TextMeshProUGUI statusText;

    public void OnClickSave()
    {
        var api = FindFirstObjectByType<PlayerSelectionSync>();
        if (api == null)
        {
            Debug.LogError("[PlayerProfileUI] PlayerSelectionSync not found in scene");
            if (statusText) statusText.text = "Không tìm thấy PlayerSelectionSync";
            return;
        }

        api.UpdatePlayerInfo(
            newName: nameInput ? nameInput.text.Trim() : null,
            newMail: mailInput ? mailInput.text.Trim() : null,
            newUserName : userName ? userName.text.Trim() : null,
            newPhone: phoneInput ? phoneInput.text.Trim() : null,
            newAvatarUrl: avatarInput ? avatarInput.text.Trim() : null,
            onSuccess: () =>
            {
                Debug.Log("Updated profile OK");
                if (statusText) statusText.text = "Cập nhật hồ sơ thành công";
            },
            onError: (e) =>
            {
                Debug.LogError("Update profile failed: " + e);
                if (statusText) statusText.text = "Cập nhật thất bại";
            }
        );
    }
}
*/