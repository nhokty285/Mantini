using System.Collections.Generic;
using UnityEngine;

public class WallFadeTarget : MonoBehaviour
{
    [System.Serializable]
    private class MaterialEntry
    {
        public Renderer renderer;
        public int materialIndex;
        public Material material;
        public Color originalColor;
    }

    [Header("Fade")]
    [Range(0f, 1f)] public float occludedAlpha = 70f / 255f;
    public float fadeSpeed = 8f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly List<MaterialEntry> entries = new();
    private bool targetOccluded;
    private bool initialized;

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null || !mat.HasProperty(BaseColorId)) continue;

                entries.Add(new MaterialEntry
                {
                    renderer = r,
                    materialIndex = i,
                    material = mat,
                    originalColor = mat.GetColor(BaseColorId)
                });
            }
        }
    }

    public void SetOccluded(bool value)
    {
        if (!initialized) Initialize();

        if (targetOccluded == value) return;
        targetOccluded = value;

        foreach (var e in entries)
        {
            if (targetOccluded)
                SetTransparent(e.material);
        }
    }

    private void Update()
    {
        if (!initialized || entries.Count == 0) return;

        bool allRestored = true;

        foreach (var e in entries)
        {
            if (e.material == null) continue;

            Color c = e.material.GetColor(BaseColorId);
            float targetA = targetOccluded ? occludedAlpha : e.originalColor.a;
            float newA = Mathf.MoveTowards(c.a, targetA, fadeSpeed * Time.deltaTime);

            c.a = newA;
            e.material.SetColor(BaseColorId, c);

            if (!targetOccluded)
            {
                if (Mathf.Abs(newA - e.originalColor.a) <= 0.001f)
                {
                    c.a = e.originalColor.a;
                    e.material.SetColor(BaseColorId, c);
                }
                else
                {
                    allRestored = false;
                }
            }
        }

        if (!targetOccluded && allRestored)
        {
            foreach (var e in entries)
                SetOpaque(e.material);

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (targetOccluded && !enabled)
            enabled = true;
    }

    private static void SetTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetOpaque(Material mat)
    {
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = -1;
    }
}