// DifyChatService.cs
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class DifyChatService : MonoBehaviour
{
    private const string BASE_URL = "https://dify.staging.storims.com/v1";
    private const string API_KEY = "YOUR_API_KEY_HERE"; // ⚠️ Nên để server-side khi release

    [Serializable]
    private class ChatRequest
    {
        public string query;
        public object inputs = new { };
        public string response_mode = "blocking";
        public string user;
        public string conversation_id;
    }

    [Serializable]
    private class ChatResponse
    {
        public string answer;
        public string conversation_id;
        public string message_id;
    }

    /// <summary>
    /// Gửi message tới Dify API. 
    /// conversationId rỗng = tạo conversation mới.
    /// </summary>
    public IEnumerator SendMessage(
        string query,
        string userId,
        string conversationId,
        Action<string, string> onSuccess,  // (answer, newConversationId)
        Action<string> onError)
    {
        var requestBody = new ChatRequest
        {
            query = query,
            user = userId,
            conversation_id = conversationId ?? "",
            response_mode = "blocking"
        };

        string json = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(BASE_URL + "/chat-messages", "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + API_KEY);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(response.answer, response.conversation_id);
        }
        else
        {
            Debug.LogError($"[DifyChatService] Error: {req.error}\n{req.downloadHandler.text}");
            onError?.Invoke(req.error);
        }
    }
}