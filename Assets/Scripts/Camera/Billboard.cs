using UnityEngine;

/// <summary>
/// Billboard: object luôn song song mặt phẳng màn hình (screen-aligned).
/// Copy thẳng rotation camera (rẻ hơn LookAt), cache camera dùng chung cho
/// MỌI Billboard trong scene để tránh GameObject.Find lặp lại mỗi lần spawn.
/// Dùng cho các prefab nhân vật (Male/Female). Nameplate đã được
/// NameplateManager xử lý billboard tập trung nên KHÔNG còn gắn script này.
/// </summary>
public class Billboard : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private Transform cam;   // Có thể gán sẵn ở Inspector để bỏ qua Find

    // Cache dùng chung cho MỌI Billboard trong scene -> Find đúng 1 lần
    private static Transform _sharedCam;

    private void OnEnable()
    {
        if (cam != null) return;

        if (_sharedCam == null)
        {
            GameObject camObj = GameObject.Find("GO_Camera");
            if (camObj != null) _sharedCam = camObj.transform;
            else if (Camera.main != null) _sharedCam = Camera.main.transform;
        }
        cam = _sharedCam;

        if (cam == null)
            GameLog.Warn("[Billboard] Không tìm thấy camera (GO_Camera / Camera.main).");
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        transform.rotation = cam.rotation; // 1 assignment, không tính LookAt
    }
}
