/*using UnityEngine;
using System.Collections.Generic;

public class MobileOcclusionHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera cam;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float targetAlpha = 0.35f;
    [SerializeField] private LayerMask occlusionLayers;
    [SerializeField] private float checkInterval = 0.1f; // Giảm tần suất check cho mobile

    [Header("Mobile Optimization")]
    [SerializeField] private int maxOccludersPerFrame = 5; // Giới hạn số vật thể xử lý
    [SerializeField] private float minDistanceCheck = 0.5f; // Khoảng cách tối thiểu

    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> instancedMaterials = new Dictionary<Renderer, Material[]>();
    private HashSet<Renderer> currentOccluders = new HashSet<Renderer>();
    private HashSet<Renderer> previousOccluders = new HashSet<Renderer>();
    private float checkTimer = 0f;

    void Start()
    {
        if (cam == null)
            cam = Camera.main;

      
    }

    void LateUpdate()
    {


        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Tối ưu: Không check mỗi frame
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            DetectOccluders();
        }

        // Update fade cho tất cả occluders
        UpdateOccluderFade();
    }

    void DetectOccluders()
    {
        previousOccluders = new HashSet<Renderer>(currentOccluders);
        currentOccluders.Clear();

        Vector3 direction = player.position - cam.transform.position;
        float distance = direction.magnitude;

        // Tối ưu: Bỏ qua nếu quá gần
       // if (distance < minDistanceCheck) return;

        // Raycast từ camera đến player
        RaycastHit[] hits = Physics.RaycastAll(
            cam.transform.position,
            direction.normalized,
            distance,
            occlusionLayers
        );

        int occluderCount = 0;
        foreach (RaycastHit hit in hits)
        {
            // Bỏ qua player
            if (hit.transform == player) continue;

            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null)
            {
                currentOccluders.Add(renderer);

                // Giới hạn số vật thể xử lý mỗi frame
                occluderCount++;
                if (occluderCount >= maxOccludersPerFrame)
                    break;
            }
        }

        // Phục hồi vật thể không còn bị che
        foreach (Renderer renderer in previousOccluders)
        {
            if (!currentOccluders.Contains(renderer))
            {
                RestoreRenderer(renderer);
            }
        }
    }

    void UpdateOccluderFade()
    {
        foreach (Renderer renderer in currentOccluders)
        {
            MakeTransparent(renderer);
        }
    }

    void MakeTransparent(Renderer renderer)
    {
        if (!instancedMaterials.ContainsKey(renderer))
        {
            // Lưu material gốc
            originalMaterials[renderer] = renderer.sharedMaterials;

            // Tạo instanced materials (tối ưu memory)
            Material[] materials = renderer.materials; // Tự động tạo instance
            instancedMaterials[renderer] = materials;

            // Chuyển sang Fade mode
            foreach (Material mat in materials)
            {
                SetupTransparentMaterial(mat);
            }
        }

        // Fade alpha
        Material[] currentMats = instancedMaterials[renderer];
        foreach (Material mat in currentMats)
        {
            Color color = mat.color;
            color.a = Mathf.Lerp(color.a, targetAlpha, fadeSpeed * Time.deltaTime);
            mat.color = color;
        }
    }

    void SetupTransparentMaterial(Material mat)
    {
        // Chuyển sang Fade rendering mode
        mat.SetFloat("_Mode", 2);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }

    void RestoreRenderer(Renderer renderer)
    {
        if (!instancedMaterials.ContainsKey(renderer))
            return;

        Material[] materials = instancedMaterials[renderer];
        bool fullyRestored = true;

        foreach (Material mat in materials)
        {
            Color color = mat.color;
            color.a = Mathf.Lerp(color.a, 1f, fadeSpeed * Time.deltaTime);
            mat.color = color;

            if (color.a < 0.99f)
                fullyRestored = false;
        }

        // Nếu đã restore hoàn toàn, phục hồi về Opaque
        if (fullyRestored)
        {
            renderer.sharedMaterials = originalMaterials[renderer];

            // Cleanup để tránh memory leak
            foreach (Material mat in materials)
            {
                if (mat != null)
                    Destroy(mat);
            }

            instancedMaterials.Remove(renderer);
            originalMaterials.Remove(renderer);
        }
    }

    void OnDestroy()
    {
        // Cleanup tất cả instanced materials
        foreach (var kvp in instancedMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.sharedMaterials = originalMaterials[kvp.Key];
            }

            foreach (Material mat in kvp.Value)
            {
                if (mat != null)
                    Destroy(mat);
            }
        }

        instancedMaterials.Clear();
        originalMaterials.Clear();
    }
}
*/