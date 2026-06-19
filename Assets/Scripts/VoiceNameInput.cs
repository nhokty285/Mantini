using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chỉ xử lý: nhấn nút → ghi âm → trả kết quả qua event.
/// Không quản lý panel, không có confirm button riêng.
/// </summary>
public class VoiceNameInput : MonoBehaviour, ISpeechToTextListener
{
    [Header("UI")]
    [SerializeField] private Button voiceButton;
    [SerializeField] private TextMeshProUGUI voiceButtonLabel;

    [Header("Settings")]
    [SerializeField] private string language = "vi-VN";
    [SerializeField] private bool preferOffline = true;

    /// <summary>
    /// Onboarding.cs subscribe event này.
    /// Trả về text nhận diện được — Onboarding tự điền vào InputField.
    /// </summary>
    public event Action<string> OnSpeechResult;

    // ─────────────────────────────────────────────────────────────────

    private void Start()
    {
        SpeechToText.Initialize(language);
        voiceButton.onClick.AddListener(OnVoiceButtonClicked);
    }

    // ─── Button Handler ───────────────────────────────────────────────

    private void OnVoiceButtonClicked()
    {
#if UNITY_EDITOR
        // Giả lập kết quả ngay lập tức khi test trên Editor
        SimulateVoiceResult("Nguyen Van A");
        return;
#endif
        if (SpeechToText.IsBusy())
        {
            SpeechToText.ForceStop();
            voiceButtonLabel.text = "🎤 Talk";
            return;
        }

        SpeechToText.RequestPermissionAsync(OnPermissionResult);
    }

#if UNITY_EDITOR
    private void SimulateVoiceResult(string fakeName)
    {
        voiceButtonLabel.text = "🎤 Talk";
        GameLog.Info($"[VoiceNameInput] EDITOR MOCK → \"{fakeName}\"");
        OnSpeechResult?.Invoke(CapitalizeName(fakeName));
    }
#endif

    private void OnPermissionResult(SpeechToText.Permission permission)
    {
        if (permission != SpeechToText.Permission.Granted)
        {
            SpeechToText.OpenSettings();
            return;
        }

        bool started = SpeechToText.Start(
            listener: this,
            useFreeFormLanguageModel: true,
            preferOfflineRecognition: preferOffline
        );

        if (started)
            voiceButtonLabel.text = "⏹ Dừng";
    }

    // ─── ISpeechToTextListener ────────────────────────────────────────

    void ISpeechToTextListener.OnReadyForSpeech() { }
    void ISpeechToTextListener.OnBeginningOfSpeech() { }
    void ISpeechToTextListener.OnVoiceLevelChanged(float level) { }
    void ISpeechToTextListener.OnPartialResultReceived(string text) { }

    void ISpeechToTextListener.OnResultReceived(string spokenText, int? errorCode)
    {
        voiceButtonLabel.text = "🎤 Talk";

        if (!string.IsNullOrEmpty(spokenText))
            OnSpeechResult?.Invoke(CapitalizeName(spokenText));
    }

    // ─── Utility ──────────────────────────────────────────────────────

    private static string CapitalizeName(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        string[] words = input.Trim().ToLower().Split(' ');
        for (int i = 0; i < words.Length; i++)
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
        return string.Join(" ", words);
    }
}