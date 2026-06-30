using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

public class NameplateManager : MonoBehaviour
{
    public static NameplateManager Instance; // Singleton để gọi từ bất kỳ đâu

    [Header("Settings")]
    public NameplateUI nameplatePrefab;     // Prefab UI mẫu (world space)
    public Transform worldSpaceContainer;   // Parent transform trong world space
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
                GameLog.Info($"[NameplateManager] Updated nameplate text for {target.name} to: {newName}");
                return;
            }
        }
        GameLog.Warn($"[NameplateManager] No nameplate found for target: {target.name}");
    }

    // --- CORE LOGIC (Chạy 1 vòng lặp duy nhất) ---
    void LateUpdate()
    {
        if (_mainCam == null) return;

        // Cache rotation camera 1 lần/frame -> thay cho Billboard trên từng nameplate.
        // Mọi nameplate screen-aligned theo camera, position + rotation ghi cùng lúc.
        Quaternion camRotation = _mainCam.transform.rotation;

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
                // Ghi position + rotation trong 1 lần -> chỉ 1 lần canvas dirty/frame
                item.UI.transform.SetPositionAndRotation(worldPos, camRotation);
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
