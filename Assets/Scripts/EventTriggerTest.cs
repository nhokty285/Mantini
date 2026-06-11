using UnityEngine;
using UnityEngine.EventSystems;

public class EventTriggerTest : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[EventTriggerTest] Button clicked!");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("[EventTriggerTest] Pointer entered button.");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("[EventTriggerTest] Pointer exited button.");
    }
}
