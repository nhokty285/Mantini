using UnityEngine;

public class MockChatParticipant : MonoBehaviour, IChatParticipant
{
    [SerializeField] private string mockName = "MockNPC";
    [SerializeField] private ChatParticipantType mockType = ChatParticipantType.VendorNPC;
    [SerializeField]
    private string[] mockResponses = {
        "Xin chào! Đây là mock response 1",
        "Tôi hiểu rồi, đây là mock response 2",
        "Cảm ơn bạn đã chat!"
    };

    private int responseIndex = 0;

    public string GetParticipantName() => mockName;
    public string GetParticipantID() => "mock_" + mockName;
    public ChatParticipantType GetParticipantType() => mockType;
    public bool IsActive() => true;

    public string ProcessMessage(string message, string sender)
    {
        // Giả lập delay API
        string response = mockResponses[responseIndex % mockResponses.Length];
        responseIndex++;

        GameLog.Info($"[MOCK-{mockName}] Received: '{message}' → Reply: '{response}'");
        return response;
    }
    Sprite IChatParticipant.GetParticipantIcon() { return null; }

    public void OnJoinChat() { GameLog.Info($"[MOCK] {mockName} joined chat"); }
    public void OnLeaveChat() { GameLog.Info($"[MOCK] {mockName} left chat"); }
}