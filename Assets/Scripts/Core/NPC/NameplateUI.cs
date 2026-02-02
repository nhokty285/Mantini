using UnityEngine;
using TMPro;

public class NameplateUI : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public RectTransform rectTransform;

    // Hàm khởi tạo nhanh
    public void Setup(string name)
    {
        nameText.text = name;
        gameObject.SetActive(true);
    }
}
