using System.Threading.Tasks;
using UnityEngine;

public interface IChatParticipant
{
    string GetParticipantName();
    string GetParticipantID();
    ChatParticipantType GetParticipantType();
    bool IsActive();
    string ProcessMessage(string message, string sender);
    void OnJoinChat();
    void OnLeaveChat();
    Sprite GetParticipantIcon();
    //string ProcessMessageWithContext(string message, string sender, string sharedContext);
}

public enum ChatParticipantType
{
    Player,
    Companion,
    VendorNPC,
    CustomerNPC,
    AIBot,
    ShopAssistant
}