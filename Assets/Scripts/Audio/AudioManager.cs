using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource dialogueSource;
    [SerializeField] private AudioSource ambientSource;

    [Header("Extra Sources")]
    [SerializeField] private AudioSource typewriterSource;

    [Header("SFX Pooling")]
    [SerializeField] private int sfxPoolSize = 8;
    [SerializeField] private AudioClip sfxPrefabClip;
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private int currentSfxIndex = 0;

    [Header("Volume Settings")]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float bgmVolume = 0.5f;
    [SerializeField] private float sfxVolume = 0.7f;
    [SerializeField] private float dialogueVolume = 0.8f;
    [SerializeField] private float ambientVolume = 0.3f;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip[] shopThemes;
    [SerializeField] private AudioClip[] uiSounds;
    [SerializeField] private AudioClip[] feedbackSounds;

    [Header("Settings")]
    [SerializeField] private bool isMuted = false;
    [SerializeField] private float crossfadeDuration = 1f;
    private bool isInitialized = false;

    private Coroutine bgmCrossfadeCoroutine;
    private AudioClip currentBGM;

    private Dictionary<string, AudioClip> _clipLookup;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeAudioSources();
        LoadVolumeSettings();
    }
   
    private void InitializeAudioSources()
    {
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        if (dialogueSource == null) dialogueSource = gameObject.AddComponent<AudioSource>();
        if (ambientSource == null) ambientSource = gameObject.AddComponent<AudioSource>();

        // Setup BGM
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume * masterVolume;

        // Setup Dialogue
        dialogueSource.loop = false;
        dialogueSource.volume = dialogueVolume * masterVolume;
        
        // Setup Ambient
        ambientSource.loop = true;
        ambientSource.volume = ambientVolume * masterVolume;

        // Create SFX pool
        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource sfx = gameObject.AddComponent<AudioSource>();
            sfx.volume = sfxVolume * masterVolume;
            sfx.loop = false;
            sfxPool.Add(sfx);
        }
        if (typewriterSource == null)
        {
            typewriterSource = gameObject.AddComponent<AudioSource>();
            typewriterSource.loop = false;
            typewriterSource.volume = sfxVolume * 0.25f * masterVolume;
        }
        BuildClipLookup();

        isInitialized = true;
        Debug.Log("[AudioManager] ✅ Initialized with " + sfxPoolSize + " SFX sources");
    }

    #region BGM CONTROL
    // ===== BGM CONTROL =====
    public void PlayBGM(AudioClip clip, bool crossfade = true)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] BGM clip is null");
            return;
        }

        if (crossfade && bgmSource.isPlaying)
        {
            if (bgmCrossfadeCoroutine != null)
                StopCoroutine(bgmCrossfadeCoroutine);

            bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBGM(clip));
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.Play();
            currentBGM = clip;
            Debug.Log("[AudioManager] 🎵 Playing BGM: " + clip.name);
        }
    }

    private IEnumerator CrossfadeBGM(AudioClip nextClip)
    {
        // Fade out current
        float elapsed = 0f;
        float startVolume = bgmSource.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0, elapsed / crossfadeDuration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = nextClip;
        bgmSource.Play();
        currentBGM = nextClip;

        // Fade in new
        elapsed = 0f;
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0, bgmVolume * masterVolume, elapsed / crossfadeDuration);
            yield return null;
        }

        bgmSource.volume = bgmVolume * masterVolume;
        bgmCrossfadeCoroutine = null;
    }

    public void StopBGM()
    {
        if (bgmCrossfadeCoroutine != null)
            StopCoroutine(bgmCrossfadeCoroutine);

        bgmSource.Stop();
        currentBGM = null;
    }
    #endregion

    #region SFX CONTROL
    // ===== SFX CONTROL =====
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] SFX clip is null");
            return;
        }
       

        if (sfxPool.Count == 0)
        {
            Debug.LogError("[AudioManager] SFX pool is empty!");
            return;
        }


        AudioSource source = sfxPool[currentSfxIndex];    
        source.clip = clip;
        source.volume = sfxVolume * volume * masterVolume;
        source.Play();       
      
        currentSfxIndex = (currentSfxIndex + 1) % sfxPool.Count;
        Debug.Log("[AudioManager] 🔊 Playing SFX: " + clip.name);
    }

    public void StopSFX(AudioClip clip)
    {
        /*  foreach (var source in sfxPool)
          {
              // Kiểm tra xem source có đang phát clip này không
              if (source.isPlaying && source.clip == clip)
              {
                  source.Stop();
                  // Nếu muốn chỉ tắt 1 cái thì thêm break; còn muốn tắt hết thì bỏ break;
              }
          }*/
        for (int i = 0; i < sfxPool.Count; i++)
            if (sfxPool[i].isPlaying && sfxPool[i].clip == clip)
                sfxPool[i].Stop();
    }
    public void PlaySFXOneShot(string clipName)
    {
        var clip = FindClipByName(clipName, uiSounds);
        if (clip != null)
            PlaySFX(clip);
    }
    #endregion

    #region DIALOGUE CONTROL
    // ===== DIALOGUE CONTROL =====
    public void PlayDialogue(AudioClip clip, float speed = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] Dialogue clip is null");
            return;
        }

        dialogueSource.clip = clip;
        dialogueSource.pitch = speed;
        dialogueSource.Play();
        Debug.Log("[AudioManager] 💬 Playing dialogue: " + clip.name);
    }

    public void StopDialogue()
    {
        dialogueSource.Stop();
    }

    public bool IsDialoguePlaying() => dialogueSource.isPlaying;

    public float GetDialogueDuration() => dialogueSource.clip != null ? dialogueSource.clip.length : 0f;

    public float GetDialoguePlaybackPosition() => dialogueSource.time;
    #endregion

    #region AMBIENT CONTROL
    // ===== AMBIENT CONTROL =====
    public void PlayAmbient(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] Ambient clip is null");
            return;
        }

        if (ambientSource.isPlaying && ambientSource.clip == clip)
            return; // Already playing

        ambientSource.clip = clip;
        ambientSource.Play();
        Debug.Log("[AudioManager] 🌍 Playing ambient: " + clip.name);
    }

    public void StopAmbient()
    {
        ambientSource.Stop();
    }
    #endregion

    #region TYPEWRITER
    public void PlayTypewriter(AudioClip clip, float volume)
    {
        if (clip == null || typewriterSource == null) return;

        if (!typewriterSource.isPlaying)
        {
            typewriterSource.clip = clip;
            typewriterSource.volume = sfxVolume * volume * masterVolume;
            typewriterSource.Play();
        }
    }

    public void StopTypewriter()
    {
        if (typewriterSource != null && typewriterSource.isPlaying)
            typewriterSource.Stop();
    }
    #endregion

    #region VOLUME CONTROL
    // ===== VOLUME CONTROL =====
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
        SaveVolumeSettings();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume * masterVolume;
        SaveVolumeSettings();
    }

    public void SetSFXVolume(float volume)
    {
        /*sfxVolume = Mathf.Clamp01(volume);
        foreach (var source in sfxPool)
            source.volume = sfxVolume * masterVolume;
        SaveVolumeSettings();*/

        float clamped = Mathf.Clamp01(volume);
        if (Mathf.Approximately(sfxVolume, clamped)) return; // ✅ skip nếu không đổi
        sfxVolume = clamped;
        float final = sfxVolume * masterVolume;
        for (int i = 0; i < sfxPool.Count; i++)   // ✅ index loop nhanh hơn foreach trên List
            sfxPool[i].volume = final;
        SaveVolumeSettings();
    }

    public void SetDialogueVolume(float volume)
    {
        dialogueVolume = Mathf.Clamp01(volume);
        dialogueSource.volume = dialogueVolume * masterVolume;
        SaveVolumeSettings();
    }

    public void SetAmbientVolume(float volume)
    {
        ambientVolume = Mathf.Clamp01(volume);
        ambientSource.volume = ambientVolume * masterVolume;
        SaveVolumeSettings();
    }

    private void UpdateAllVolumes()
    {
        bgmSource.volume = bgmVolume * masterVolume;
        dialogueSource.volume = dialogueVolume * masterVolume;
        ambientSource.volume = ambientVolume * masterVolume;

        foreach (var source in sfxPool)
            source.volume = sfxVolume * masterVolume;
    }

    public void SetMute(bool muted)
    {
        isMuted = muted;
        AudioListener.pause = isMuted;
        SaveVolumeSettings();
        Debug.Log("[AudioManager] 🔇 Muted: " + isMuted);
    }

    public bool IsMuted() => isMuted;
    #endregion

    #region UTILITY METHODS
    // ===== UTILITY =====
    /* private AudioClip FindClipByName(string name, AudioClip[] clips)
     {
         foreach (var clip in clips)
         {
             if (clip.name == name)
                 return clip;
         }

         Debug.LogWarning("[AudioManager] Clip not found: " + name);
         return null;
     }*/
    private AudioClip FindClipByName(string name, AudioClip[] _unused = null)
    {
        if (_clipLookup != null && _clipLookup.TryGetValue(name, out var clip))
            return clip;
        Debug.LogWarning("[AudioManager] Clip not found: " + name);
        return null;
    }


    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("DialogueVolume", dialogueVolume);
        PlayerPrefs.SetFloat("AmbientVolume", ambientVolume);
        PlayerPrefs.SetInt("IsMuted", isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.7f);
        dialogueVolume = PlayerPrefs.GetFloat("DialogueVolume", 0.8f);
        ambientVolume = PlayerPrefs.GetFloat("AmbientVolume", 0.3f);
        isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

        UpdateAllVolumes();
    }
    #endregion

    private void BuildClipLookup()
    {
        _clipLookup = new Dictionary<string, AudioClip>(
            (uiSounds?.Length ?? 0) + (feedbackSounds?.Length ?? 0) + (shopThemes?.Length ?? 0)
        );
        void Register(AudioClip[] arr)
        {
            if (arr == null) return;
            foreach (var c in arr)
                if (c != null && !_clipLookup.ContainsKey(c.name))
                    _clipLookup[c.name] = c;
        }
        Register(uiSounds);
        Register(feedbackSounds);
        Register(shopThemes);
    }

    public float GetMasterVolume() => masterVolume;
    public float GetBGMVolume() => bgmVolume;
    public float GetSFXVolume() => sfxVolume;
    public float GetDialogueVolume() => dialogueVolume;
    public float GetAmbientVolume() => ambientVolume;
}

