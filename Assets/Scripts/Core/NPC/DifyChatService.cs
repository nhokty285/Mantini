// DifyChatService.cs
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class DifyChatService : MonoBehaviour
{
    private const string BASE_URL = "https://dify.staging.storims.com/v1";

    [Serializable]
    private class ChatRequest
    {
        public string query;
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

    public IEnumerator SendMessageAI(
        string query,
        string apiKey,           // ← nhận aiPersonality vào đây
        string userId,
        string conversationId,
        Action<string, string> onSuccess,
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

        using var req = new UnityWebRequest(BASE_URL + "/chat-messages", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + apiKey); // ← dùng key của NPC đó
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(response.answer, response.conversation_id);
        }
        else
        {
            Debug.LogError($"[DifyChatService] Error: {req.error}");
            onError?.Invoke(req.error);
        }
    }
}