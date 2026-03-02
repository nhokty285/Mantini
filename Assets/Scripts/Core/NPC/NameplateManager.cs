/*using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

public class NameplateManager : MonoBehaviour
{
    public static NameplateManager Instance; // Singleton để gọi từ bất kỳ đâu

    [Header("Settings")]
    public NameplateUI nameplatePrefab; // Prefab UI mẫu
    public Transform canvasContainer;   // UI Parent (thường là một Panel trong Canvas)
    public Vector3 defaultOffset = new Vector3(0, 2f, 0);

    public Camera _mainCam;

    // Class lưu trữ mối liên kết giữa Target 3D và UI 2D
    private class TrackedObject
    {
        public Transform Target;
        public NameplateUI UI;
        public Vector3 Offset;
    }

    private List<TrackedObject> _activeList = new List<TrackedObject>();
    private Queue<NameplateUI> _pool = new Queue<NameplateUI>(); // Object Pooling

    void Awake()
    {
        Instance = this;
    }

    // --- PUBLIC API (NPC sẽ gọi hàm này) ---
    public void Register(Transform target, string name, Vector3? offset = null)
    {
        // 1. Lấy UI từ Pool hoặc tạo mới
        NameplateUI ui = GetFromPool();

        // 2. Setup dữ liệu
        ui.Setup(name);

        // 3. Thêm vào danh sách theo dõi
        _activeList.Add(new TrackedObject
        {
            Target = target,
            UI = ui,
            Offset = offset ?? defaultOffset
        });
    }

    public void Unregister(Transform target)
    {
        // Tìm và gỡ bỏ target
        for (int i = _activeList.Count - 1; i >= 0; i--)
        {
            if (_activeList[i].Target == target)
            {
                ReturnToPool(_activeList[i].UI);
                _activeList.RemoveAt(i);
                break; // Xử lý xong thì thoát
            }
        }
    }

    // --- CORE LOGIC (Chạy 1 vòng lặp duy nhất) ---
    void LateUpdate()
    {
        // Cache các giá trị màn hình để tối ưu
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        for (int i = 0; i < _activeList.Count; i++)
        {
            var item = _activeList[i];

            // Nếu target bị hủy (null) mà chưa Unregister, tự động dọn dẹp
            if (item.Target == null)
            {
                ReturnToPool(item.UI);
                _activeList.RemoveAt(i);
                i--;
                continue;
            }

            // Tính toán vị trí
            Vector3 worldPos = item.Target.position + item.Offset;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPos);

            // Tối ưu hóa: Chỉ hiển thị nếu nằm trong màn hình và phía trước camera
            bool isOnScreen = screenPos.z > 0 &&
                              screenPos.x > 0 && screenPos.x < screenWidth &&
                              screenPos.y > 0 && screenPos.y < screenHeight;

            if (isOnScreen)
            {
                if (!item.UI.gameObject.activeSelf) item.UI.gameObject.SetActive(true);
                item.UI.rectTransform.position = screenPos;
            }
            else
            {
                // Ẩn đi để đỡ tốn chi phí render UI
                if (item.UI.gameObject.activeSelf) item.UI.gameObject.SetActive(false);
            }
        }
    }

    // --- POOLING SYSTEM ---
    private NameplateUI GetFromPool()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }
        return Instantiate(nameplatePrefab, canvasContainer);
    }

    private void ReturnToPool(NameplateUI ui)
    {
        ui.gameObject.SetActive(false);
        _pool.Enqueue(ui);
    }
}

*/

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

public class NameplateManager : MonoBehaviour
{
    public static NameplateManager Instance; // Singleton để gọi từ bất kỳ đâu

    [Header("Settings")]
    public NameplateUI nameplatePrefab; // Prefab UI mẫu (world space)
    public Transform worldSpaceContainer; // Parent transform trong world space
    public Vector3 defaultOffset = new Vector3(0, 2f, 0);
    public Camera _mainCam;

    // Class lưu trữ mối liên kết giữa Target 3D và UI World Space
    private class TrackedObject
    {
        public Transform Target;
        public NameplateUI UI;
        public Vector3 Offset;
    }

    private List<TrackedObject> _activeList = new List<TrackedObject>();
    private Queue<NameplateUI> _pool = new Queue<NameplateUI>(); // Object Pooling

    void Awake()
    {
        Instance = this;
        if (_mainCam == null) _mainCam = Camera.main;
    }

    // --- PUBLIC API (NPC sẽ gọi hàm này) ---
    public void Register(Transform target, string name, Vector3? offset = null)
    {
        // 1. Lấy UI từ Pool hoặc tạo mới
        NameplateUI ui = GetFromPool();

        // 2. Setup dữ liệu
        ui.Setup(name);

        // 3. Thêm vào danh sách theo dõi
        _activeList.Add(new TrackedObject
        {
            Target = target,
            UI = ui,
            Offset = offset ?? defaultOffset
        });
    }

    public void Unregister(Transform target)
    {
        // Tìm và gỡ bỏ target
        for (int i = _activeList.Count - 1; i >= 0; i--)
        {
            if (_activeList[i].Target == target)
            {
                ReturnToPool(_activeList[i].UI);
                _activeList.RemoveAt(i);
                break; // Xử lý xong thì thoát
            }
        }
    }

    /// <summary>
    /// Cập nhật text của nameplate cho một target cụ thể
    /// </summary>
    public void UpdateNameplateText(Transform target, string newName)
    {
        for (int i = 0; i < _activeList.Count; i++)
        {
            if (_activeList[i].Target == target)
            {
                _activeList[i].UI.Setup(newName);
                Debug.Log($"[NameplateManager] Updated nameplate text for {target.name} to: {newName}");
                return;
            }
        }
        Debug.LogWarning($"[NameplateManager] No nameplate found for target: {target.name}");
    }

    // --- CORE LOGIC (Chạy 1 vòng lặp duy nhất) ---
    void LateUpdate()
    {
        for (int i = 0; i < _activeList.Count; i++)
        {
            var item = _activeList[i];

            // Nếu target bị hủy (null) mà chưa Unregister, tự động dọn dẹp
            if (item.Target == null)
            {
                ReturnToPool(item.UI);
                _activeList.RemoveAt(i);
                i--;
                continue;
            }

            // Tính toán vị trí world space
            Vector3 worldPos = item.Target.position + item.Offset;

            // Kiểm tra nếu đối tượng nằm phía trước camera
            Vector3 viewportPos = _mainCam.WorldToViewportPoint(worldPos);
            bool isInFrontOfCamera = viewportPos.z > 0;

            if (isInFrontOfCamera)
            {
                if (!item.UI.gameObject.activeSelf) item.UI.gameObject.SetActive(true);
                // Đặt vị trí trực tiếp trong world space
                item.UI.transform.position = worldPos;
            }
            else
            {
                // Ẩn đi để đỡ tốn chi phí render
                if (item.UI.gameObject.activeSelf) item.UI.gameObject.SetActive(false);
            }
        }
    }

    // --- POOLING SYSTEM ---
    private NameplateUI GetFromPool()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }
        return Instantiate(nameplatePrefab, worldSpaceContainer);
    }

    private void ReturnToPool(NameplateUI ui)
    {
        ui.gameObject.SetActive(false);
        _pool.Enqueue(ui);
    }
}