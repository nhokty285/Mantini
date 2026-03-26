using UnityEngine;
using UnityEngine.UI;
public class AudioHelper : MonoBehaviour
{
    [SerializeField] private Button soundButton;
    [SerializeField] private GameObject soundPopup;

    private void Start()
    {
        if (soundPopup != null)
            soundPopup.SetActive(false);

        soundButton.onClick.AddListener(() =>
        {
            soundPopup.SetActive(true);
            PlayerController.Instance?.SetCanMove(false);
            AudioManager.Instance.PlaySFXOneShot("Button");
        });
    }
    public void CloseSoundButton()
    {
        if (soundPopup != null)
            soundPopup.SetActive(false);
        PlayerController.Instance?.SetCanMove(true);
        AudioManager.Instance.PlaySFXOneShot("Close");
    }
    public void UI_PlayBgmSound(AudioClip audioClip)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(audioClip);
        }
    }

    public void UI_PlayBgmStop()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM();
        }
    }
    public void UI_PlaySFXOneSHot(string nameSound)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot(nameSound);
        }
    }
    public void UI_PlayAbmSound(AudioClip audioClip)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAmbient(audioClip);
        }
    }
    public void UI_PlayAbmStopSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAmbient();
        }
    }
}
