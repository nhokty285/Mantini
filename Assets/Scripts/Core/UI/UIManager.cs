using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    [SerializeField] private UIConfig config;

    private readonly Stack<GameObject> screenStack = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void ShowScreen(GameObject screenPrefab)
    {
        var screen = Instantiate(screenPrefab, transform);
        screenStack.Push(screen);
        // Có thể thêm animation fade-in sử dụng config.fadeDuration
    }

    public void CloseScreen()
    {
        if (screenStack.Count == 0) return;
        var screen = screenStack.Pop();
        Destroy(screen);
    }

  

}
