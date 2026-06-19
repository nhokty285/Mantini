using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelLoader : MonoBehaviour
{
    // Refactor: singleton property theo Mantini convention (trước đây public field)
    public static LevelLoader Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Settings")]
    [SerializeField] private float minLoadTime = 1.5f;
    [SerializeField] private float fakeLoadDuration = 1.5f;

    // Smooth progress ramp rate (units/second) — extract magic number
    private const float ProgressSmoothRate = 3f;

    private Coroutine _currentLoadingRoutine;
    private bool _isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingCanvas != null) loadingCanvas.SetActive(false);
        ResetLoadingUI();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═════════════════════════════════════════════════════════════════════════

    public void LoadLevel(string sceneName)
    {
        if (_isLoading) return;
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LevelLoader] LoadLevel called with empty sceneName");
            return;
        }
        _currentLoadingRoutine = StartCoroutine(LoadAsynchronously(sceneName));
    }

    public void ShowLoadingThenSwitch(GameObject currentPanel, GameObject nextPanel)
        => ShowLoadingThenSwitch(currentPanel, nextPanel, fakeLoadDuration);

    public void ShowLoadingThenSwitch(GameObject currentPanel, GameObject nextPanel, float duration)
    {
        if (_isLoading) return;
        _currentLoadingRoutine = StartCoroutine(FakeProgressThenAction(duration, () =>
        {
            if (currentPanel != null) currentPanel.SetActive(false);
            if (nextPanel != null) nextPanel.SetActive(true);
        }));
    }

    public void ShowLoadingThenDo(Action onComplete)
        => ShowLoadingThenDo(onComplete, fakeLoadDuration);

    public void ShowLoadingThenDo(Action onComplete, float duration)
    {
        if (_isLoading) return;
        _currentLoadingRoutine = StartCoroutine(FakeProgressThenAction(duration, onComplete));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CORE COROUTINES
    // ═════════════════════════════════════════════════════════════════════════

    // Refactor: gộp 2 coroutine duplicate (ShowLoadingThenSwitchCoroutine + ShowLoadingThenDoCoroutine)
    // thành 1 helper nhận Action — DRY
    private IEnumerator FakeProgressThenAction(float duration, Action onComplete)
    {
        _isLoading = true;
        SetupLoadingUI();

        float elapsed = 0f;
        duration = Mathf.Max(0.1f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            UpdateProgressUI(progress);
            yield return null;
        }

        UpdateProgressUI(1f);
        onComplete?.Invoke();

        HideLoadingUI();
        _isLoading = false;
        _currentLoadingRoutine = null;
    }

    private IEnumerator LoadAsynchronously(string sceneName)
    {
        _isLoading = true;
        SetupLoadingUI();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LevelLoader] LoadSceneAsync returned null for: {sceneName}");
            HideLoadingUI();
            _isLoading = false;
            _currentLoadingRoutine = null;
            yield break;
        }
        operation.allowSceneActivation = false;

        float currentProgress = 0f;
        float endTime = Time.time + minLoadTime;

        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, ProgressSmoothRate * Time.deltaTime);
            UpdateProgressUI(currentProgress);

            if (operation.progress >= 0.9f && Time.time >= endTime && currentProgress >= 0.99f)
            {
                UpdateProgressUI(1f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        HideLoadingUI();
        _isLoading = false;
        _currentLoadingRoutine = null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UI HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private void SetupLoadingUI()
    {
        if (loadingCanvas != null) loadingCanvas.SetActive(true);
        ResetLoadingUI();
    }

    private void HideLoadingUI()
    {
        ResetLoadingUI();
        if (loadingCanvas != null) loadingCanvas.SetActive(false);
    }

    private void ResetLoadingUI()
    {
        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
        }
        if (progressText != null)
            progressText.text = "Loading 0%";
    }

    private void UpdateProgressUI(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progressBar != null) progressBar.value = progress;
        if (progressText != null) progressText.text = $"Loading {(int)(progress * 100f)}%";
    }
}
