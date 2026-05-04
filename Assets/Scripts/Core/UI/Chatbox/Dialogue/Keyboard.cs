using TMPro;
using UnityEngine;

public class Keyboard : MonoBehaviour
{
    [Header("Kéo GO_CompanionChat vào đây")]
    [SerializeField] private RectTransform targetPanel; // GO_CompanionChat

    [Header("Kéo Canvas gốc vào đây")]
    [SerializeField] private Canvas rootCanvas;

    [Header("Kéo InputField (TMP) vào đây")]
    [SerializeField] private TMP_InputField inputField;

    private Vector2 originalPos;
    private bool isMoved = false;

    private void Start()
    {
        // Chờ layout tính xong mới lưu vị trí gốc
        StartCoroutine(SaveOriginalPos());

        // Lắng nghe sự kiện focus của InputField
        inputField.onSelect.AddListener(_ => OnKeyboardOpen());
        inputField.onDeselect.AddListener(_ => OnKeyboardClose());
    }

    private System.Collections.IEnumerator SaveOriginalPos()
    {
        yield return new WaitForEndOfFrame();
        originalPos = targetPanel.anchoredPosition;
        Debug.Log("Original pos: " + originalPos);
    }

    private void OnKeyboardOpen()
    {
        if (isMoved) return;
        isMoved = true;

        float offsetY = GetKeyboardOffsetY();
        targetPanel.anchoredPosition = new Vector2(originalPos.x, originalPos.y + offsetY);
        Debug.Log($"Keyboard opened → moved up {offsetY} units");
    }

    private void OnKeyboardClose()
    {
        if (!isMoved) return;
        isMoved = false;

        targetPanel.anchoredPosition = originalPos;
        Debug.Log("Keyboard closed → reset position");
    }

    private float GetKeyboardOffsetY()
    {
#if UNITY_EDITOR
        return 875f; // Giả lập trong Editor, chỉnh số này cho vừa mắt
#else
        float keyboardH = TouchScreenKeyboard.area.height;
        if (keyboardH <= 0) return 875f; // fallback

        float canvasH = rootCanvas.GetComponent<RectTransform>().rect.height;
        float ratio = keyboardH / Screen.height;

        // Bù scale từ Canvas xuống local space của targetPanel
        float canvasScale = rootCanvas.transform.lossyScale.y;
        float panelScale = targetPanel.parent.lossyScale.y;
        float scaleRatio = (panelScale > 0) ? canvasScale / panelScale : 1f;

        return ratio * canvasH * scaleRatio;
#endif
    }
}
