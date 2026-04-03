using UnityEngine;

[System.Serializable]
public struct ImageLayout
{
    public Vector2 anchoredPosition;
    public Vector2 size;

    public static ImageLayout Default => new ImageLayout
    {
        anchoredPosition = Vector2.zero,
        size = new Vector2(200f, 200f)
    };
}
