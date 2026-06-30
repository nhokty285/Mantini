using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tự động chọn quality tier (URP Asset + target FPS) theo phần cứng máy.
/// Đặt trong scene bootstrap/login đầu tiên. DontDestroyOnLoad.
///
/// Pacing: dùng vSyncCount (khóa theo vblank) để nhịp frame ĐỀU thay vì timer.
///   - Panel 120Hz: 60fps -> vSync=2, 30fps -> vSync=4 (đều tăm tắp).
///   - Panel lẻ (90/144Hz) không chia chẵn -> fallback Application.targetFrameRate.
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
    [Tooltip("Máy yếu: 30fps ổn định mượt hơn 40-50fps giật")]
    [SerializeField] private int lowTierFps = 30;
    [Tooltip("Máy tầm trung: 30fps ổn định (đổi 60 nếu muốn)")]
    [SerializeField] private int midTierFps = 30;
    [Tooltip("Máy mạnh: 60fps (Adaptive Performance sẽ hạ động khi nóng)")]
    [SerializeField] private int highTierFps = 60;

    private const string PREF_OVERRIDE = "Mantini_QualityTierOverride"; // -1 = auto

    // ═════════════════════ FRAME CAP (bulletproof) ═════════════════════
    /// <summary>
    /// Baseline cap chạy TRƯỚC mọi scene (kể cả vào thẳng gameplay). Dùng timer
    /// đơn giản vì Screen.refreshRate có thể chưa sẵn ở BeforeSceneLoad.
    /// Awake() sau đó áp pacing vSync chính xác theo tier qua ApplyFrameRate().
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnforceFrameCapEarly()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60; // baseline an toàn cho mọi máy/entry scene
    }

    /// <summary>
    /// Cap FPS với pacing ĐỀU: nếu refresh panel chia chẵn cho targetFps thì dùng
    /// vSyncCount (khóa vblank → nhịp đều), ngược lại fallback timer.
    /// VD panel 120Hz: 60→vSync=2, 30→vSync=4.
    /// </summary>
    private static void ApplyFrameRate(int targetFps)
    {
        if (targetFps <= 0) targetFps = 60;

        float refresh = 60f;
        try { refresh = (float)Screen.currentResolution.refreshRateRatio.value; } catch { }
        if (refresh < 1f) refresh = 60f;

        int divisor = Mathf.Max(1, Mathf.RoundToInt(refresh / targetFps));
        bool clean = Mathf.Abs(refresh / divisor - targetFps) < 1.5f;

        if (clean)
        {
            QualitySettings.vSyncCount = divisor;     // pacing đều theo vblank
            Application.targetFrameRate = targetFps;  // fallback nếu engine bỏ qua vSync
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFps;  // panel lẻ (90/144Hz) → timer
        }
    }

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

        // 3. FPS + pacing đều theo tier (vSync divisor nếu panel chia chẵn).
        int targetFps = tier switch
        {
            QualityTier.Low => lowTierFps,
            QualityTier.Medium => midTierFps,
            _ => highTierFps,
        };
        ApplyFrameRate(targetFps);

        if (save)
        {
            PlayerPrefs.SetInt(PREF_OVERRIDE, (int)tier);
            PlayerPrefs.Save();
        }

        GameLog.Info($"[QualityAutoDetect] Tier={tier} | RAM={SystemInfo.systemMemorySize}MB " +
                  $"| VRAM={SystemInfo.graphicsMemorySize}MB | GPU={SystemInfo.graphicsDeviceName} " +
                  $"| API={SystemInfo.graphicsDeviceType} | FPS={targetFps} | vSync={QualitySettings.vSyncCount}");
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
