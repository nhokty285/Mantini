using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Đặt 1 instance trong scene đầu tiên (DontDestroyOnLoad).
/// Nhiệm vụ: phản ứng khi OS báo sắp hết RAM + dọn rác khi đổi scene.
///
/// Complexity: chỉ chạy khi có event (lowMemory / sceneUnloaded),
/// KHÔNG có Update() — zero cost per frame.
/// </summary>
public class MemoryWatchdog : MonoBehaviour
{
    public static MemoryWatchdog Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        Application.lowMemory += HandleLowMemory;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        Application.lowMemory -= HandleLowMemory;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void HandleLowMemory()
    {
        Debug.LogWarning("[MemoryWatchdog] OS LOW MEMORY — flushing caches");

        // Xả texture cache (chiếm nhiều RAM nhất)
        ImageDownloadManager.Instance?.ClearCache();

        // Xả cache JSON / shop items nếu CacheService tồn tại
        if (CacheService.Instance != null)
            CacheService.Instance.Clear();

        // Giải phóng asset không còn reference (async, không block frame)
        Resources.UnloadUnusedAssets();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        // Khi rời scene: asset của scene cũ không còn reference → dọn luôn,
        // tránh memory dồn tích qua nhiều lần đổi scene.
        Resources.UnloadUnusedAssets();
    }
}
