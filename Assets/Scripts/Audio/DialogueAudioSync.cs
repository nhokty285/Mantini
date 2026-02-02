using UnityEngine;
using System.Collections;
using TMPro;

public class DialogueAudioSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] public AudioClip typewriterSound;

    [Header("Settings")]
    [SerializeField] private float characterDelay = 0.05f;
    [SerializeField] private bool playTypewriterSFX = true;

    private Coroutine typewriterCoroutine;
    private string fullText = "";
    private TextMeshProUGUI currentTextComponent;
    public void StartTypewriter(TextMeshProUGUI targetText, string text, AudioClip dialogueClip = null)
    {
        StopTypewriter();

        currentTextComponent = targetText; // Lưu lại text đang dùng
        fullText = text;
        // Stop existing typewriter
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        typewriterCoroutine = StartCoroutine(TypewriterRoutine(text, dialogueClip));
    }

    public void StopTypewriter()
    {
        StopTypeSound();
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        if (currentTextComponent != null)
            currentTextComponent.text = fullText;
    }
    public void StopTypeSound()
    {
        AudioManager.Instance.StopTypewriter();
    }
    private IEnumerator TypewriterRoutine(string text, AudioClip dialogueClip)
    {
        // Play dialogue audio if provided
        if (dialogueClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDialogue(dialogueClip);
        }

        currentTextComponent.text = "";
        float displayDelay = characterDelay;

        // Calculate display delay based on audio duration if syncing
        if (dialogueClip != null)
        {
            float audioDuration = dialogueClip.length;
            displayDelay = audioDuration / text.Length;
        }

        for (int i = 0; i < text.Length; i++)
        {
            currentTextComponent.text += text[i];

                if (text[i] != ' ' && text[i] != '\n')
                {
                    AudioManager.Instance.PlayTypewriter(typewriterSound,0.2f);
                }                
                   
                yield return new WaitForSeconds(displayDelay);
        }
        AudioManager.Instance.StopTypewriter();
        typewriterCoroutine = null;
    }

    public void SkipTypewriter()
    {
  /*      if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (currentTextComponent != null)
            currentTextComponent.text = fullText;*/

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopDialogue();
    }

    public bool IsTypewriting() => typewriterCoroutine != null;
}
