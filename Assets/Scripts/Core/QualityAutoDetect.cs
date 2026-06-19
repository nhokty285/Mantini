using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tự động chọn quality tier (URP Asset + target FPS) theo phần cứng máy.
/// Đặt trong scene bootstrap/login đầu tiên. DontDestroyOnLoad.
///
/// CÁCH SETUP (1 lần trong Editor):
///  1. Duplicate URP Asset hiện tại thành 3 bản: URP_Low / URP_Mid / URP_High.
///     - URP_Low : Render Scale 0.75, Shadows OFF (hoặc distance 15m, no cascades),
///                 MSAA Disabled, HDR OFF, Additional Lights = Disabled.
///     - URP_Mid : Render Scale 0.9, Shadow distance 25m, MSAA Disabled, HDR OFF.
///     - URP_High: Render Scale 1.0, shadow/quality như bạn đang dùng.
///  2. Kéo 3 asset vào Inspector của script này.
///  3. (Khuyến nghị) Trong Project Settings > Quality cũng tạo 3 level
///     Low/Mid/High gán đúng 3 asset trên — script sẽ sync cả 2 đường.
///
/// Tại sao swap asset thay vì sửa renderScale runtime?
///  - Sửa property của ScriptableObject lúc Play trong Editor sẽ LƯU VĨNH VIỄN
///    vào asset → dễ "mất" config gốc mà không hiểu vì sao.
///  - 3 asset baked sẵn = dữ liệu rõ ràng, dễ tune, dễ QA từng tier.
///
/// Complexity: toàn bộ logic chạy 1 LẦN trong Awake — zero cost per frame.
/// </summary>
[DefaultExecutionOrder(-1000)] // chạy trước mọi script khác
public class QualityAutoDetect : MonoBehaviour
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    public static QualityAutoDetect Instance { get; private set; }
    public QualityTier CurrentTier { get; private set; }

    [Header("URP Assets (baked sẵn từng tier)")]
    [SerializeField] private UniversalRenderPipelineAsset lowAsset;
    [SerializeField] private UniversalRenderPipelineAsset mediumAsset;
    [SerializeField] private UniversalRenderPipelineAsset highAsset;

    [Header("Ngưỡng phát hiện (MB)")]
    [Tooltip("RAM hệ thống dưới mức này → Low tier")]
    [SerializeField] private int lowRamMB = 3500;     // máy 2-3GB
    [Tooltip("RAM hệ thống từ mức này trở lên → ứng viên High tier")]
    [SerializeField] private int highRamMB = 6500;    // máy 8GB+
    [Tooltip("VRAM GPU dưới mức này → ép xuống Low")]
    [SerializeField] private int lowGpuMemMB = 1024;

    [Header("Target FPS theo tier")]
    [SerializeField] private int lowTierFps = 30;     // 30 ổn định > 45 giật
    [SerializeField] private int defaultFps = 60;

    private const string PREF_OVERRIDE = "Mantini_QualityTierOverride"; // -1 = auto

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // User đã chọn tay trong Settings? → tôn trọng lựa chọn đó
        int saved = PlayerPrefs.GetInt(PREF_OVERRIDE, -1);
        QualityTier tier = (saved >= 0 && saved <= 2)
            ? (QualityTier)saved
            : DetectTier();

        ApplyTier(tier, save: false);
    }

    // ═════════════════════ DETECTION ═════════════════════

    /// <summary>
    /// Phân tier dựa trên RAM + VRAM + graphics API.
    /// SystemInfo là cached static — đọc rẻ, không I/O.
    /// </summary>
    public QualityTier DetectTier()
    {
        int ramMB = SystemInfo.systemMemorySize;     // RAM hệ thống
        int vramMB = SystemInfo.graphicsMemorySize;  // VRAM (mobile = shared, vẫn là tín hiệu tốt)
        var gfx = SystemInfo.graphicsDeviceType;

        // 1. GPU quá cũ (GLES2) hoặc không xác định được RAM → an toàn nhất là Low
        if (gfx == GraphicsDeviceType.OpenGLES2) return QualityTier.Low;
        if (ramMB <= 0) return QualityTier.Medium;   // không đọc được → trung dung

        // 2. RAM hoặc VRAM yếu → Low
        if (ramMB < lowRamMB || (vramMB > 0 && vramMB < lowGpuMemMB))
            return QualityTier.Low;

        // 3. RAM dồi dào + GPU hiện đại (Vulkan/Metal) → High
        bool modernApi = gfx == GraphicsDeviceType.Vulkan || gfx == GraphicsDeviceType.Metal;
        if (ramMB >= highRamMB && modernApi)
            return QualityTier.High;

        // 4. Còn lại → Medium
        return QualityTier.Medium;
    }

    // ═════════════════════ APPLY ═════════════════════

    /// <summary>
    /// Áp tier. Gọi từ Settings menu với save=true khi user chọn tay.
    /// </summary>
    public void ApplyTier(QualityTier tier, bool save)
    {
        CurrentTier = tier;

        // 1. Swap URP asset cho quality level hiện hành
        var asset = GetAsset(tier);
        if (asset != null)
            QualitySettings.renderPipeline = asset;
        else
            GameLog.Warn($"[QualityAutoDetect] Chưa gán URP asset cho tier {tier}!");

        // 2. Sync QualitySettings level nếu project có đủ 3 level (an toàn nếu không có)
        if (QualitySettings.names.Length > (int)tier)
            QualitySettings.SetQualityLevel((int)tier, applyExpensiveChanges: true);

        // 3. FPS: máy yếu chạy 30 FPS ổn định mượt hơn 40-50 FPS giật cục,
        //    đồng thời mát máy + đỡ tụt pin → ít bị thermal throttle về sau.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = tier == QualityTier.Low ? lowTierFps : defaultFps;

        if (save)
        {
            PlayerPrefs.SetInt(PREF_OVERRIDE, (int)tier);
            PlayerPrefs.Save();
        }

        GameLog.Info($"[QualityAutoDetect] Tier={tier} | RAM={SystemInfo.systemMemorySize}MB " +
                  $"| VRAM={SystemInfo.graphicsMemorySize}MB | GPU={SystemInfo.graphicsDeviceName} " +
                  $"| API={SystemInfo.graphicsDeviceType} | FPS={Application.targetFrameRate}");
    }

    /// <summary>Trở về chế độ auto-detect (gọi từ nút "Auto" trong Settings).</summary>
    public void ResetToAuto()
    {
        PlayerPrefs.DeleteKey(PREF_OVERRIDE);
        ApplyTier(DetectTier(), save: false);
    }

    private UniversalRenderPipelineAsset GetAsset(QualityTier tier) => tier switch
    {
        QualityTier.Low => lowAsset,
        QualityTier.Medium => mediumAsset,
        _ => highAsset,
    };
}