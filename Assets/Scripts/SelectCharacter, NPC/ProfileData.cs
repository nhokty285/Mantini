using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;


public class ProfileData : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameField, usernameField, mailField, phoneField;
    [SerializeField] private PlayerApiService apiService; // Drag PlayerApiService GameObject
    [SerializeField] private RawImage avatarPreview;

    private string currentAvatarUrl; // lưu file:///path hoặc persistent path
    
    //Test show profile at awaken
    private void Start()
    {
        LoadProfile();
    }
    public void LoadProfile()
    {
        apiService.LoadProfileFromServer(
            data =>
            {
                // Fill vào InputField
                nameField.text = data.name ?? "";
                usernameField.text = data.username_email ?? "";
                mailField.text = data.mail ?? "";
                phoneField.text = data.phone ?? "";
                currentAvatarUrl = data.avatar_url;

                // Load avatar nếu có URL
                if (!string.IsNullOrEmpty(data.avatar_url))
                {
                    StartCoroutine(LoadAvatarFromUrl(data.avatar_url));
                }
            },
            error => Debug.LogError("Load profile fail: " + error)
        );
    }

    IEnumerator LoadAvatarFromUrl(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                avatarPreview.texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            }
        }
    }
    public void OnClickPickAvatar()
    {
        // Gọi thư viện ảnh
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("User cancel");
                return;
            }

            // Tạo texture từ file đã chọn
            StartCoroutine(LoadTextureFromPath(path));
        },
        "Chọn ảnh", "image/*");
    }

    IEnumerator LoadTextureFromPath(string path)
    {
        string url = "file:///" + path.Replace("\\", "/");
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Load fail: " + req.error);
                yield break;
            }

            avatarPreview.texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            currentAvatarUrl = url;
        }
    }
    public void SaveProfile()
    {
        string name = nameField.text;
        string username = usernameField.text;
        string mail = mailField.text;
        string phone = phoneField.text;

        apiService.UpdatePlayerInfo(
            newName: name,
            newUserName: username,
            newMail: mail,
            newPhone: phone,
            newAvatarUrl: currentAvatarUrl,
            onSuccess: () => {
                Debug.Log("Profile cập nhật thành công!");
                // Tùy chọn: Gọi SyncSelectionToServer() sau
                // apiService.SyncSelectionToServer();
            },
            onError: (err) => Debug.LogError("Lỗi: " + err)
        );
    }
}
