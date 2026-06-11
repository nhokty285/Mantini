using System.Diagnostics;

/// <summary>
/// Wrapper log có [Conditional] — toàn bộ call site bị compiler XOÁ HẲN
/// khỏi release build (không phải if-check runtime, mà là zero cost tuyệt đối:
/// không alloc string, không boxing, không gọi hàm).
///
/// Cách dùng: thay Debug.Log(...) → GameLog.Info(...)
///           thay Debug.LogWarning(...) → GameLog.Warn(...)
/// Debug.LogError giữ nguyên (lỗi thật cần thấy cả trên production).
///
/// GC note: trên Android, mỗi Debug.Log = string interpolation alloc + JNI call.
/// AudioManager log mỗi lần play SFX, Dify log mỗi message → strip hết
/// giúp giảm GC churn rõ rệt khi gameplay dày tương tác.
/// </summary>
public static class GameLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string message) => UnityEngine.Debug.Log(message);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string message, UnityEngine.Object context)
        => UnityEngine.Debug.Log(message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(string message) => UnityEngine.Debug.LogWarning(message);

    // Error luôn log — kể cả production, để còn trace crash
    public static void Error(string message) => UnityEngine.Debug.LogError(message);
}