/*// SharedRoomContext.cs
// ============================================================
// Singleton quản lý 1 conversation_id CHUNG cho toàn bộ phòng chat.
// Tất cả NPC và Player đều dùng chung ID này, phân biệt nhau
// bằng prefix "[TÊN]:" trong nội dung query.
// ============================================================
using UnityEngine;

public class SharedRoomContext : MonoBehaviour
{
    public static SharedRoomContext Instance { get; private set; }

    // conversation_id dùng chung — rỗng = phiên mới
    private string _sharedConversationId = "";

    // user đại diện cho "phòng" — Dify dùng để phân biệt session
    // KHÔNG dùng userId riêng của từng NPC nữa
    [SerializeField] private string roomUserId = "room-001";

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    /// <summary>Trả về conversation_id hiện tại của phòng.</summary>
    public string GetConversationId() => _sharedConversationId;

    /// <summary>Lưu conversation_id trả về từ Dify vào phòng chung.</summary>
    public void SetConversationId(string id)
    {
        if (!string.IsNullOrEmpty(id))
            _sharedConversationId = id;
    }

    /// <summary>userId đại diện cho phòng — dùng trong mọi API call.</summary>
    public string GetRoomUserId() => roomUserId;

    /// <summary>Reset phòng khi thoát scene / bắt đầu game mới.</summary>
    public void ResetRoom()
    {
        _sharedConversationId = "";
        GameLog.Info("[SharedRoomContext] Room conversation reset.");
    }

    /// <summary>
    /// Format query với prefix tên người nói.
    /// VD: "[NPC_Lan]: Xin chào!" hoặc "[Player]: Tôi muốn mua đồ."
    /// </summary>
    public static string FormatQuery(string senderName, string message)
    {
        return $"[{senderName}]: {message}";
    }
}*/