using UnityEngine;
using UnityEngine.EventSystems;

public class EventTriggerTest : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        GameLog.Info("[EventTriggerTest] Button clicked!");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GameLog.Info("[EventTriggerTest] Pointer entered button.");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GameLog.Info("[EventTriggerTest] Pointer exited button.");
    }
}