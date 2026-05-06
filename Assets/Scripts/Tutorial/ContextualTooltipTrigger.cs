using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ContextualTooltipTrigger : MonoBehaviour, IPointerClickHandler
{
    [TextArea(1, 3)]
    [SerializeField] private string tooltipMessage = "Đây là tính năng mới!";
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private float autoHideDuration = 3f; // 0 = không tự ẩn

    private string seenKey;
    private bool isEnabled = false;

    private void Awake()
        => seenKey = "Tooltip_Seen_" + gameObject.name;

    public void Enable() => isEnabled = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isEnabled) return;
        if (PlayerPrefs.GetInt(seenKey, 0) == 1) return; // Chỉ hiện lần đầu
        if (tooltipText != null) tooltipText.text = tooltipMessage;
        if (tooltipPanel != null) tooltipPanel.SetActive(true);
        PlayerPrefs.SetInt(seenKey, 1);
        PlayerPrefs.Save();
        if (autoHideDuration > 0)
            StartCoroutine(AutoHide());
    }

    private System.Collections.IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(autoHideDuration);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    [ContextMenu("DEBUG: Reset Seen")]
    public void DebugReset()
    {
        PlayerPrefs.DeleteKey(seenKey);
        PlayerPrefs.Save();
    }
}