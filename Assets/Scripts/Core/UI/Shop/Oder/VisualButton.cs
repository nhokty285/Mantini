using UnityEngine.UI;

public class VisualButton : Button
{
    public void ShowPressedVisual(bool instant = false)
    {
        DoStateTransition(SelectionState.Pressed, instant);
    }

    public void ShowNormalVisual(bool instant = false)
    {
        DoStateTransition(SelectionState.Normal, instant);
    }
}