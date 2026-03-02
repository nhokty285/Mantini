using UnityEngine;
using UnityEngine.UI;

public class LayoutFixer : MonoBehaviour
{
    [SerializeField] private RectTransform targetTransform; 

    public void FixLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(targetTransform);
    }
}
    