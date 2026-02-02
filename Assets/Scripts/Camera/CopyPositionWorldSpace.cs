using UnityEngine;

public class CopyPositionScreenSpace : MonoBehaviour
{
    public Transform target3D;       // Vật thể 3D cần theo dõi
    public RectTransform uiElement;  // UI Element
    public Camera mainCam;           // Camera chính (World Space Camera)
    public Vector3 offset;           // Khoảng cách điều chỉnh (ví dụ: cao hơn đầu nhân vật chút xíu)

    void Start()
    {
        if (mainCam == null) mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (target3D != null && uiElement != null)
        {
            // 1. Lấy vị trí của vật thể + khoảng cách offset
            Vector3 worldPos = target3D.position + offset;

            // 2. Chuyển đổi từ World Space sang Screen Space
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            // 3. Gán vào UI
            // Lưu ý: Nếu vật thể ở sau lưng camera, đôi khi cần ẩn UI đi
            if (screenPos.z > 0)
            {
                uiElement.position = screenPos;
                uiElement.gameObject.SetActive(true);
            }
            else
            {
                uiElement.gameObject.SetActive(false); // Ẩn nếu ở sau lưng
            }
        }
    }
}
