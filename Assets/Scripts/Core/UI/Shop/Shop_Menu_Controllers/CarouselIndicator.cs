using UnityEngine;
using UnityEngine.UI;

public class CarouselIndicator : MonoBehaviour
{
    [SerializeField] private GameObject dotLeft, dotCenter, dotRight;
    private int lastIndex = -1;

    public void UpdateDots(int currentIndex, int totalItems)
    {

        // ✅ OPTIMIZE: Chỉ update khi index thay đổi
        if (lastIndex == currentIndex) return;
        lastIndex = currentIndex;

        if (dotLeft == null || dotCenter == null || dotRight == null) return;

        SetDotActive(dotLeft, false);
        SetDotActive(dotCenter, false);
        SetDotActive(dotRight, false);

        if (totalItems <= 1) return;

        if (currentIndex == 0)
            SetDotActive(dotLeft, true);
        else if (currentIndex == totalItems - 1)
            SetDotActive(dotRight, true);
        else
            SetDotActive(dotCenter, true);
    }

    private void SetDotActive(GameObject dot, bool active)
    {
        if (dot != null) dot.SetActive(active);
    }
}
