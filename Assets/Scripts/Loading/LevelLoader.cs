using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro; // Nếu dùng TextMeshPro

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance;

    [Header("UI Components")]
    [SerializeField] private GameObject loadingCanvas; // Kéo Canvas Loading vào đây
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Settings")]
    [SerializeField] private float minLoadTime = 1.5f; // Thời gian load tối thiểu để người dùng kịp đọc tips

    private void Awake()
    {
        // Singleton pattern & DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            loadingCanvas.SetActive(false); // Ẩn mặc định
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadLevel(string sceneName)
    {
        StartCoroutine(LoadAsynchronously(sceneName));
    }

    /*  private IEnumerator LoadAsynchronously(string sceneName)
      {
          // 1. Bật màn hình Loading
          loadingCanvas.SetActive(true);

          // 3. Bắt đầu tải Scene ngầm
          AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
          operation.allowSceneActivation = false; // Khoan hãy chuyển cảnh ngay

          float currentProgress = 0f;
          float startTime = Time.time;

          // 4. Vòng lặp update thanh loading
          while (!operation.isDone)
          {
              // Unity load đến 0.9 là xong phần data, 0.1 còn lại là activation
              float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);

              // Làm mượt thanh loading (Fake smooth)
              currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, 3f * Time.deltaTime);

              if (progressBar != null)  progressBar.value = currentProgress;
              if (progressText!= null)  progressText.text = $"Loading {(int)(currentProgress * 100)}%";

              // Logic kiểm tra điều kiện để kết thúc
              // Chỉ chuyển cảnh khi: Load xong (0.9) VÀ Đã chạy đủ thời gian tối thiểu (để ko bị nháy màn hình)
              if (operation.progress >= 0.9f && Time.time - startTime >= minLoadTime && currentProgress >= 0.99f)
              {
                  // Dọn rác bộ nhớ trước khi sang cảnh mới (Pro tip)
                  System.GC.Collect();

                  // Cho phép chuyển cảnh
                  operation.allowSceneActivation = true;
              }

              yield return null;
          }

          // 5. Sau khi load xong
          loadingCanvas.SetActive(false);

      }*/

    // LevelLoader.cs — Optimized
    private IEnumerator LoadAsynchronously(string sceneName)
    {
        if (loadingCanvas != null) loadingCanvas.SetActive(true); // ✅ null guard

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float currentProgress = 0f;
        float endTime = Time.time + minLoadTime; // ✅ tính 1 lần thay vì mỗi frame

        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, 3f * Time.deltaTime);

            if (progressBar != null) progressBar.value = currentProgress;
            if (progressText != null) progressText.text = $"Loading {(int)(currentProgress * 100)}%";

            if (operation.progress >= 0.9f && Time.time >= endTime && currentProgress >= 0.99f)
            {
                // ✅ KHÔNG gọi GC.Collect() thủ công — Unity tự quản lý GC tốt hơn
                // Nếu muốn unload assets không dùng, dùng Resources.UnloadUnusedAssets() thay thế
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        if (loadingCanvas != null) loadingCanvas.SetActive(false); // ✅ null guard
    }
}
