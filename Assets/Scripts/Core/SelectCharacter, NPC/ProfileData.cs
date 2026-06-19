using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProfileData : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameField, usernameField, mailField, phoneField;
    [SerializeField] private RawImage avatarPreview;

    /// <summary>Fires khi avatar sprite thay đổi (pick mới hoặc load từ server).</summary>
    public event Action<Sprite> OnAvatarChanged;

    /// <summary>Sprite avatar đã cache, dùng chung cho các UI khác (Chat, HUD...).</summary>
    public Sprite AvatarSprite { get; private set; }

    private PlayerApiService _apiService;
    private string _currentAvatarUrl;

    /// <summary>Gọi từ Scene 2 để trỏ ProfileData sang UI mới thay vì UI Scene 1.</summary>
    public void RebindUI(TMP_InputField name, TMP_InputField username, TMP_InputField mail, TMP_InputField phone, RawImage avatar)
    {
        nameField = name;
        usernameField = username;
        mailField = mail;
        phoneField = phone;
        if (avatar != null) avatarPreview = avatar;
    }

    private void Awake()
    {
        // Tìm PlayerApiService dù nó ở Scene 1 (DontDestroyOnLoad) hay Scene 2
        _apiService = FindAnyObjectByType<PlayerApiService>();
        if (_apiService == null)
            Debug.LogError("[ProfileData] Không tìm thấy PlayerApiService!");
    }

    private void Start() => LoadProfile();

    public void LoadProfile()
    {
        if (_apiService == null) return;

        _apiService.LoadProfileFromServer(
            data =>
            {
                if (nameField != null) nameField.text = data.name ?? "";
                if (usernameField != null) usernameField.text = data.username_email ?? "";
                if (mailField != null) mailField.text = data.mail ?? "";
                if (phoneField != null) phoneField.text = data.phone ?? "";
                _currentAvatarUrl = data.avatar_url;

                if (!string.IsNullOrEmpty(data.avatar_url))
                    StartCoroutine(LoadAvatarFromUrl(data.avatar_url));
            },
            error => Debug.LogError("[ProfileData] Load profile fail: " + error)
        );
    }

    public void SetDefaultAvatar(Sprite defaultIcon)
    {
        if (AvatarSprite != null) return; // đã có avatar, bỏ qua

        AvatarSprite = defaultIcon;

        if (avatarPreview != null && defaultIcon != null)
            avatarPreview.texture = defaultIcon.texture;

        OnAvatarChanged?.Invoke(AvatarSprite);
#if UNITY_EDITOR
        GameLog.Info("[ProfileData] Default avatar applied from CharacterData icon.");
#endif
    }

    private IEnumerator LoadAvatarFromUrl(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                // Refactor: destroy sprite cũ trước khi tạo mới — tránh memory leak
                ReleaseCurrentSprite();

                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                if (avatarPreview != null) avatarPreview.texture = tex;
                AvatarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                OnAvatarChanged?.Invoke(AvatarSprite);
            }
        }
    }

    public void OnClickPickAvatar()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
#if UNITY_EDITOR
                GameLog.Info("[ProfileData] User cancel image pick");
#endif
                return;
            }
            StartCoroutine(LoadTextureFromPath(path));
        },
        "Chọn ảnh", "image/*");
    }

    private IEnumerator LoadTextureFromPath(string path)
    {
        string url = "file:///" + path.Replace("\\", "/");
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ProfileData] Load fail: " + req.error);
                yield break;
            }

            // Refactor: destroy sprite cũ trước khi tạo mới
            ReleaseCurrentSprite();

            var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            if (avatarPreview != null) avatarPreview.texture = tex;
            _currentAvatarUrl = url;
            AvatarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            OnAvatarChanged?.Invoke(AvatarSprite);
        }
    }

    // Helper: destroy sprite cũ — tránh leak khi reload avatar nhiều lần
    private void ReleaseCurrentSprite()
    {
        if (AvatarSprite != null)
        {
            Destroy(AvatarSprite);
            AvatarSprite = null;
        }
    }

    public void SaveProfile()
    {
        if (_apiService == null) return;

        string name = nameField != null ? nameField.text : "";
        string username = usernameField != null ? usernameField.text : "";
        string mail = mailField != null ? mailField.text : "";
        string phone = phoneField != null ? phoneField.text : "";

        _apiService.UpdatePlayerInfo(
            newName: name,
            newUserName: username,
            newMail: mail,
            newPhone: phone,
            newAvatarUrl: _currentAvatarUrl,
            onSuccess: () =>
            {
#if UNITY_EDITOR
                GameLog.Info("[ProfileData] Profile cập nhật thành công!");
#endif
                _apiService.SyncSelectionToServer();
                ShowRestartMessageOrFallback();
            },
            onError: (err) => Debug.LogError("[ProfileData] Lỗi: " + err)
        );
    }

    private void ShowRestartMessageOrFallback()
    {
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.ShowPopup(
                "Thông báo",
                "Game sẽ khởi động lại để cập nhật thay đổi.",
                RestartFlow
            );
            return;
        }

        GameLog.Warn("[ProfileData] PopupManager is null.");
        RestartFlow();
    }

    private void RestartFlow()
    {
        if (SceneManager.GetActiveScene().name == "CreateCharacter") return;

        if (LevelLoader.Instance != null)
            LevelLoader.Instance.LoadLevel("MapTest2");
    }
}