using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;


public class ProfileData : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameField, usernameField, mailField, phoneField;
    [SerializeField] private RawImage avatarPreview;

    /// <summary>Fires khi avatar sprite thay đổi (pick mới hoặc load từ server).</summary>
    public event Action<Sprite> OnAvatarChanged;

    /// <summary>Sprite avatar đã cache, dùng chung cho các UI khác (Chat, HUD...).</summary>
    public Sprite AvatarSprite { get; private set; }

    private PlayerApiService apiService;
    private string currentAvatarUrl;

    /// <summary>
    /// Gọi từ Scene 2 để trỏ ProfileData sang UI mới thay vì UI Scene 1.
    /// </summary>
    public void RebindUI(TMP_InputField name, TMP_InputField username, TMP_InputField mail, TMP_InputField phone, RawImage avatar)
    {
        nameField     = name;
        usernameField = username;
        mailField     = mail;
        phoneField    = phone;
        if (avatar != null) avatarPreview = avatar;
    }

    private void Awake()
    {
        // Tìm PlayerApiService dù nó ở Scene 1 (DontDestroyOnLoad) hay Scene 2
        apiService = FindAnyObjectByType<PlayerApiService>();
        if (apiService == null)
            Debug.LogError("[ProfileData] Không tìm thấy PlayerApiService!");
    }

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
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                avatarPreview.texture = tex;
                AvatarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                OnAvatarChanged?.Invoke(AvatarSprite);
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

            var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            avatarPreview.texture = tex;
            currentAvatarUrl = url;
            AvatarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            OnAvatarChanged?.Invoke(AvatarSprite);
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

                // Đồng bộ companion selection với server
                apiService.SyncSelectionToServer();

                // Thông báo restart để cập nhật thay đổi
                PopupManager.Instance.ShowPopup(
                    "Thông báo",
                    "Game sẽ khởi động lại để cập nhật thay đổi.",
                    () => 
                    { 
                        LevelLoader.Instance.LoadLevel("MapTest2");
                    }
                );
            },
            onError: (err) => Debug.LogError("Lỗi: " + err)
        );
    }
}
