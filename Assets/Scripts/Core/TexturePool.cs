using System.Collections.Generic;
using UnityEngine;

public class TexturePool : MonoBehaviour
{
    public static TexturePool Instance { get; private set; }

    private Queue<Texture2D> availableTextures = new Queue<Texture2D>();
    private HashSet<Texture2D> usedTextures = new HashSet<Texture2D>();

    [SerializeField] private int maxPoolSize = 50;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public Texture2D GetTexture(int width, int height)
    {
        if (availableTextures.Count > 0)
        {
            var texture = availableTextures.Dequeue();
            usedTextures.Add(texture);
            return texture;
        }

        var newTexture = new Texture2D(width, height);
        usedTextures.Add(newTexture);
        return newTexture;
    }

    public void ReturnTexture(Texture2D texture)
    {
        if (texture == null || !usedTextures.Contains(texture)) return;

        usedTextures.Remove(texture);

        if (availableTextures.Count < maxPoolSize)
            availableTextures.Enqueue(texture);
        else
            DestroyImmediate(texture);
    }

    public void CleanupPool()
    {
        while (availableTextures.Count > 0)
        {
            var texture = availableTextures.Dequeue();
            DestroyImmediate(texture);
        }

        foreach (var texture in usedTextures)
            DestroyImmediate(texture);

        usedTextures.Clear();
    }
}
