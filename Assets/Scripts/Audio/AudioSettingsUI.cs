using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider dialogueVolumeSlider;
    [SerializeField] private Slider ambientVolumeSlider;
    [SerializeField] private Toggle muteToggle;

    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [SerializeField] private TextMeshProUGUI bgmVolumeText;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;
    [SerializeField] private TextMeshProUGUI dialogueVolumeText;
    [SerializeField] private TextMeshProUGUI ambientVolumeText;
    [SerializeField] private AudioManager audioManager;
    private void Start()
    {
        if (AudioManager.Instance == null)
        {
            audioManager = GetComponent<AudioManager>();   
        }

        InitializeSliders();
        SetupEventListeners();
    }

    private void InitializeSliders()
    {
        masterVolumeSlider.value = AudioManager.Instance.GetMasterVolume();
        bgmVolumeSlider.value = AudioManager.Instance.GetBGMVolume();
        sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
        dialogueVolumeSlider.value = AudioManager.Instance.GetDialogueVolume();
        ambientVolumeSlider.value = AudioManager.Instance.GetAmbientVolume();
        muteToggle.isOn = AudioManager.Instance.IsMuted();

        UpdateVolumeTexts();
    }

    private void SetupEventListeners()
    {
        masterVolumeSlider.onValueChanged.AddListener(value =>
        {
            AudioManager.Instance.SetMasterVolume(value);
            UpdateVolumeTexts();
        });

        bgmVolumeSlider.onValueChanged.AddListener(value =>
        {
            AudioManager.Instance.SetBGMVolume(value);
            UpdateVolumeTexts();
        });

        sfxVolumeSlider.onValueChanged.AddListener(value =>
        {
            AudioManager.Instance.SetSFXVolume(value);
            UpdateVolumeTexts();
        });

        dialogueVolumeSlider.onValueChanged.AddListener(value =>
        {
            AudioManager.Instance.SetDialogueVolume(value);
            UpdateVolumeTexts();
        });

        ambientVolumeSlider.onValueChanged.AddListener(value =>
        {
            AudioManager.Instance.SetAmbientVolume(value);
            UpdateVolumeTexts();
        });

        muteToggle.onValueChanged.AddListener(isOn =>
        {
            AudioManager.Instance.SetMute(isOn);
        });
    }

    private void UpdateVolumeTexts()
    {
        masterVolumeText.text = $"{Mathf.RoundToInt(AudioManager.Instance.GetMasterVolume() * 100)}%";
        bgmVolumeText.text = $"{Mathf.RoundToInt(AudioManager.Instance.GetBGMVolume() * 100)}%";
        sfxVolumeText.text = $"{Mathf.RoundToInt(AudioManager.Instance.GetSFXVolume() * 100)}%";
        dialogueVolumeText.text = $"{Mathf.RoundToInt(AudioManager.Instance.GetDialogueVolume() * 100)}%";
        ambientVolumeText.text = $"{Mathf.RoundToInt(AudioManager.Instance.GetAmbientVolume() * 100)}%";
    }
}
