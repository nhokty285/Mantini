using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class DifyChatInputs { }

[Serializable]
public class DifyChatRequest
{
    public string query;
    public string user;
    public string response_mode = "streaming"; // ✅ BẮT BUỘC với Agent App
    public string conversation_id = "";
    public DifyChatInputs inputs = new DifyChatInputs();
}

// Dùng để parse từng SSE chunk
[Serializable]
public class DifyStreamChunk
{
    public string event_type; // JsonUtility không map "event" vì là keyword → dùng cách khác
    public string answer;
    public string conversation_id;
    public string message_id;
    public string task_id;
}

[Serializable]
public class DifyChatResponse
{
    public string answer;
    public string conversation_id;
    public string message_id;
}

public class DifyChatService : MonoBehaviour
{
    private const string BASE_URL = "https://dify.staging.storims.com/v1";

    public static DifyChatService Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public void SendMessageAI(
        string apiKey,
        string userId,
        string query,
        string conversationId,
        Action<string, string> onSuccess,
        Action<string> onError)
    {
        StartCoroutine(SendMessageCoroutine(apiKey, userId, query, conversationId, onSuccess, onError));
    }

    private IEnumerator SendMessageCoroutine(
        string apiKey,
        string userId,
        string query,
        string conversationId,
        Action<string, string> onSuccess,
        Action<string> onError)
    {
        var requestBody = new DifyChatRequest
        {
            query = query,
            user = userId,
            response_mode = "streaming",
            conversation_id = conversationId ?? ""
        };

        string json = JsonUtility.ToJson(requestBody);
        Debug.Log($"[DifyChatService] JSON: {json}");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(BASE_URL + "/chat-messages", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string rawBody = request.downloadHandler.text;
                ParseStreamingResponse(rawBody, onSuccess, onError);
            }
            catch (Exception e)
            {
                onError?.Invoke("Parse error: " + e.Message);
            }
        }
        else
        {
            string errorBody = request.downloadHandler?.text ?? "no body";
            Debug.LogError($"[DifyChatService] HTTP {request.responseCode} | Error: {request.error} | Body: {errorBody}");
            onError?.Invoke($"Network error: {request.error}");
        }
    }

    /// <summary>
    /// Parse SSE response — ghép tất cả chunk "agent_message"/"message" thành 1 câu hoàn chỉnh
    /// </summary>
    private void ParseStreamingResponse(string rawBody, Action<string, string> onSuccess, Action<string> onError)
    {
        StringBuilder fullAnswer = new StringBuilder();
        string conversationId = "";
        string messageId = "";

        // Mỗi SSE event cách nhau bằng "\n\n", mỗi dòng bắt đầu bằng "data: "
        string[] lines = rawBody.Split('\n');

        foreach (string line in lines)
        {
            if (!line.StartsWith("data: ")) continue;

            string jsonChunk = line.Substring(6).Trim(); // bỏ "data: "

            if (jsonChunk == "[DONE]") break;
            if (string.IsNullOrEmpty(jsonChunk)) continue;

            try
            {
                // Parse "event" field thủ công vì "event" là reserved keyword
                string eventType = ExtractJsonField(jsonChunk, "event");

                // Agent App dùng "agent_message", Chat App dùng "message"
                if (eventType == "agent_message" || eventType == "message")
                {
                    string chunk = ExtractJsonField(jsonChunk, "answer");
                    if (!string.IsNullOrEmpty(chunk))
                        fullAnswer.Append(chunk);

                    if (string.IsNullOrEmpty(conversationId))
                        conversationId = ExtractJsonField(jsonChunk, "conversation_id");

                    if (string.IsNullOrEmpty(messageId))
                        messageId = ExtractJsonField(jsonChunk, "message_id");
                }
                else if (eventType == "message_end" || eventType == "agent_thought")
                {
                    // message_end = stream kết thúc, lấy conversation_id lần cuối
                    if (string.IsNullOrEmpty(conversationId))
                        conversationId = ExtractJsonField(jsonChunk, "conversation_id");
                }
                else if (eventType == "error")
                {
                    string errMsg = ExtractJsonField(jsonChunk, "message");
                    onError?.Invoke($"Dify stream error: {errMsg}");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DifyChatService] Skip chunk parse error: {e.Message}");
            }
        }

        string finalAnswer = fullAnswer.ToString().Trim();
        Debug.Log($"[DifyChatService] Final answer: {finalAnswer}");

        if (!string.IsNullOrEmpty(finalAnswer))
            onSuccess?.Invoke(finalAnswer, conversationId);
        else
            onError?.Invoke("Empty response from Dify");
    }

    /// Extract 1 field từ JSON string không cần JsonUtility
    /// Dùng vì "event" là keyword trong C# và JsonUtility không map được
    private string ExtractJsonField(string json, string fieldName)
    {
        string search = $"\"{fieldName}\":\"";
        int start = json.IndexOf(search);
        if (start < 0) return "";

        start += search.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return "";

        return json.Substring(start, end - start);
    }
}


