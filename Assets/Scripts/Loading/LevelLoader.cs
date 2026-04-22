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
