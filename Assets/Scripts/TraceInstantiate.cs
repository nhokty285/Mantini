using UnityEngine;
using System.Diagnostics; // BẮT BUỘC có thư viện này

public class TraceInstantiate : MonoBehaviour
{
    void Awake()
    {
        // 1. Tạo mới một StackTrace
        // Tham số 'true' nghĩa là lấy cả thông tin về số dòng (line number) và tên file
        StackTrace stackTrace = new StackTrace(true);

        // 2. In ra Console
        // stackTrace.ToString() sẽ chuyển toàn bộ lịch sử gọi hàm thành văn bản
        UnityEngine.Debug.Log($"Object '{name}' được tạo bởi:\n{stackTrace.ToString()}");
    }
}
