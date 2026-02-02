using UnityEngine;

[CreateAssetMenu(fileName = "UIConfig", menuName = "UI/Config")]
public class UIConfig : ScriptableObject
{
    public float fadeDuration = 0.3f;
    public Vector2 popupOffset = new Vector2(0, -50);
    // Thêm các tham số chung khác...
}
