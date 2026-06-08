using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;

public sealed class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance;

    [Header("UI Components")]
    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Settings")]
    [SerializeField] private float minLoadTime = 1.5f;
    [SerializeField] private float fakeLoadDuration = 1.5f;

    private Coroutine currentLoadingRoutine;
    private bool isLoading;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (loadingCanvas != null)
            {
                loadingCanvas.SetActive(false);
            }

            ResetLoadingUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadLevel(string sceneName)
    {
        if (isLoading)
            return;

        currentLoadingRoutine = StartCoroutine(LoadAsynchronously(sceneName));
    }

    public void ShowLoadingThenSwitch(GameObject currentPanel, GameObject nextPanel)
    {
        ShowLoadingThenSwitch(currentPanel, nextPanel, fakeLoadDuration);
    }

    public void ShowLoadingThenSwitch(GameObject currentPanel, GameObject nextPanel, float duration)
    {
        if (isLoading)
            return;

        currentLoadingRoutine = StartCoroutine(ShowLoadingThenSwitchCoroutine(currentPanel, nextPanel, duration));
    }

    public void ShowLoadingThenDo(Action onComplete)
    {
        ShowLoadingThenDo(onComplete, fakeLoadDuration);
    }

    public void ShowLoadingThenDo(Action onComplete, float duration)
    {
        if (isLoading)
            return;

        currentLoadingRoutine = StartCoroutine(ShowLoadingThenDoCoroutine(onComplete, duration));
    }

    private IEnumerator ShowLoadingThenSwitchCoroutine(GameObject currentPanel, GameObject nextPanel, float duration)
    {
        isLoading = true;
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

        if (currentPanel != null)
            currentPanel.SetActive(false);

        if (nextPanel != null)
            nextPanel.SetActive(true);

        HideLoadingUI();
        isLoading = false;
        currentLoadingRoutine = null;
    }

    private IEnumerator ShowLoadingThenDoCoroutine(Action onComplete, float duration)
    {
        isLoading = true;
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
        isLoading = false;
        currentLoadingRoutine = null;
    }

    private IEnumerator LoadAsynchronously(string sceneName)
    {
        isLoading = true;
        SetupLoadingUI();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float currentProgress = 0f;
        float endTime = Time.time + minLoadTime;

        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, 3f * Time.deltaTime);

            UpdateProgressUI(currentProgress);

            if (operation.progress >= 0.9f && Time.time >= endTime && currentProgress >= 0.99f)
            {
                UpdateProgressUI(1f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        HideLoadingUI();
        isLoading = false;
        currentLoadingRoutine = null;
    }

    private void SetupLoadingUI()
    {
        if (loadingCanvas != null)
            loadingCanvas.SetActive(true);

        ResetLoadingUI();
    }

    private void HideLoadingUI()
    {
        ResetLoadingUI();

        if (loadingCanvas != null)
            loadingCanvas.SetActive(false);
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
        {
            progressText.text = "Loading 0%";
        }
    }

    private void UpdateProgressUI(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (progressBar != null)
            progressBar.value = progress;

        if (progressText != null)
            progressText.text = $"Loading {(int)(progress * 100f)}%";
    }
}