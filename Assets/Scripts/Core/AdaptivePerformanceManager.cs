using UnityEngine;
using UnityEngine.AdaptivePerformance;

/// <summary>
/// Nghe trạng thái NHIỆT của máy (Unity Adaptive Performance + Samsung/Google ADPF)
/// và hạ FPS động khi máy nóng → tránh thermal throttle ở phiên chơi dài.
///
/// Bổ trợ cho QualityAutoDetect:
///   - QualityAutoDetect: chọn tier theo PHẦN CỨNG (1 lần lúc khởi động).
///   - Script này: ghìm thêm theo NHIỆT THỰC TẾ (specs cao như 8 Gen 1 vẫn nóng → tự hạ).
///
/// Tự spawn qua RuntimeInitialize nên KHÔNG cần đặt vào scene. DontDestroyOnLoad.
///
/// YÊU CẦU: Project Settings > Adaptive Performance → bật "Initialize Adaptive
/// Performance on Startup" + chọn Android Provider. Chưa bật → script tự cảnh báo & tắt.
/// </summary>
[DefaultExecutionOrder(-900)] // sau QualityAutoDetect (-1000)
public class AdaptivePerformanceManager : MonoBehaviour
{
    public static AdaptivePerformanceManager Instance { get; private set; }

    [Header("Phản ứng nhiệt (hạ FPS khi nóng)")]
    [Tooltip("Tắt nếu bạn bật Adaptive Framerate Scaler trong Project Settings (tránh tranh nhau)")]
    [SerializeField] private bool enableFpsThrottle = true;
    [Tooltip("FPS khi máy SẮP throttle (ThrottlingImminent)")]
    [SerializeField] private int warmFps = 45;
    [Tooltip("FPS khi máy ĐANG throttle (Throttling)")]
    [SerializeField] private int hotFps = 30;

    [Header("Cho AP tự quản CPU/GPU clock (tiết kiệm điện, đỡ nóng)")]
    [SerializeField] private bool automaticCpuGpuControl = true;

    private IAdaptivePerformance _ap;
    private int _baseFps = 60;                               // FPS nền do QualityAutoDetect chọn theo tier
    private WarningLevel _lastLevel = WarningLevel.NoWarning;

    // Public cho hệ thống khác đọc (vd: cull thêm nameplate/particle khi nóng)
    public bool IsActive { get; private set; }
    public WarningLevel CurrentWarningLevel { get; private set; }
    public float TemperatureLevel { get; private set; }     // 0..1 (1 = chạm ngưỡng throttle)
    public float TemperatureTrend { get; private set; }     // -1..1 (1 = nóng lên nhanh)

    /// <summary>Tự tạo GameObject quản lý sau khi scene đầu load — không cần đặt tay vào scene.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[AdaptivePerformanceManager]");
        go.AddComponent<AdaptivePerformanceManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; } // chỉ xoá component → an toàn khi share GameObject với APIClient
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _ap = Holder.Instance;

        if (_ap == null || !_ap.Active)
        {
            GameLog.Warn("[AdaptivePerformanceManager] Adaptive Performance KHÔNG active. " +
                         "Vào Project Settings > Adaptive Performance: bật 'Initialize on Startup' + chọn Android Provider. Tắt script.");
            IsActive = false;
            enabled = false;
            return;
        }
        IsActive = true;

        // QualityAutoDetect (-1000) đã chạy trước → lấy FPS nền tier vừa chọn
        int t = Application.targetFrameRate;
        _baseFps = (t <= 0) ? 60 : Mathf.Max(30, t);

        // Cho AP tự điều chỉnh clock CPU/GPU thấp nhất đủ dùng → mát hơn, ít throttle hơn
        if (automaticCpuGpuControl)
            _ap.DevicePerformanceControl.AutomaticPerformanceControl = true;

        // Đọc trạng thái nhiệt ban đầu + đăng ký sự kiện
        var m = _ap.ThermalStatus.ThermalMetrics;
        CurrentWarningLevel = m.WarningLevel;
        TemperatureLevel = m.TemperatureLevel;
        TemperatureTrend = m.TemperatureTrend;
        _lastLevel = m.WarningLevel;

        _ap.ThermalStatus.ThermalEvent += OnThermalEvent;

        GameLog.Info($"[AdaptivePerformanceManager] Active. baseFps={_baseFps} | " +
                     $"thermal khởi đầu: level={m.WarningLevel}, temp={m.TemperatureLevel:F2}, trend={m.TemperatureTrend:F2}");
    }

    private void OnDestroy()
    {
        if (_ap != null && _ap.Active)
            _ap.ThermalStatus.ThermalEvent -= OnThermalEvent;
        if (Instance == this) Instance = null;
    }

    private void OnThermalEvent(ThermalMetrics metrics)
    {
        CurrentWarningLevel = metrics.WarningLevel;
        TemperatureLevel = metrics.TemperatureLevel;
        TemperatureTrend = metrics.TemperatureTrend;

        if (metrics.WarningLevel == _lastLevel) return;

        GameLog.Info($"[AdaptivePerformanceManager] Thermal {_lastLevel} → {metrics.WarningLevel} " +
                     $"(temp={metrics.TemperatureLevel:F2}, trend={metrics.TemperatureTrend:F2})");
        _lastLevel = metrics.WarningLevel;

        if (enableFpsThrottle) ReactFps(metrics.WarningLevel);
    }

    private void ReactFps(WarningLevel level)
    {
        int fps = level switch
        {
            WarningLevel.Throttling => hotFps,                          // đang throttle → 30
            WarningLevel.ThrottlingImminent => Mathf.Min(_baseFps, warmFps), // sắp → 45 (không vượt FPS tier)
            _ => _baseFps,                                              // mát → trả về FPS tier
        };
        SetFrameRate(fps);
    }

    /// <summary>Cap FPS pacing đều bằng vSync divisor (panel 120Hz: 60→vSync2, 30→vSync4).</summary>
    private static void SetFrameRate(int targetFps)
    {
        if (targetFps <= 0) targetFps = 60;
        float refresh = 60f;
        try { refresh = (float)Screen.currentResolution.refreshRateRatio.value; } catch { }
        if (refresh < 1f) refresh = 60f;

        int divisor = Mathf.Max(1, Mathf.RoundToInt(refresh / targetFps));
        if (Mathf.Abs(refresh / divisor - targetFps) < 1.5f)
        {
            QualitySettings.vSyncCount = divisor;
            Application.targetFrameRate = targetFps;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFps;
        }
    }
}
