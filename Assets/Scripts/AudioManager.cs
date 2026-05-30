using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SoundEntry
{
    public string soundName;
    public AudioClip clip;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Background Music")]
    [Tooltip("Arrastra aquí el archivo .mp3 o .wav de la música de fondo principal.")]
    [SerializeField] private AudioClip backgroundMusicClip;

    [Header("Sound Library")]
    [SerializeField] private SoundEntry[] sfxLibrary;

    private Dictionary<string, AudioClip> sfxDictionary;

    // Propiedades públicas para que los Toggles de la UI consulten el estado real del sistema
    public bool IsMusicMuted => musicSource != null ? musicSource.mute : false;
    public bool IsSFXMuted => sfxSource != null ? sfxSource.mute : false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDictionary();
            LoadAudioSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Inicializa y reproduce la música de fondo si hay un clip asignado
        if (musicSource != null && backgroundMusicClip != null && !musicSource.isPlaying)
        {
            musicSource.clip = backgroundMusicClip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    private void InitializeDictionary()
    {
        sfxDictionary = new Dictionary<string, AudioClip>();
        foreach (var entry in sfxLibrary)
        {
            if (!sfxDictionary.ContainsKey(entry.soundName))
            {
                sfxDictionary.Add(entry.soundName, entry.clip);
            }
        }
    }

    private void LoadAudioSettings()
    {
        // 1 = Silenciado, 0 = Activo
        bool musicMuteState = PlayerPrefs.GetInt("Setting_MuteMusic", 0) == 1;
        bool sfxMuteState = PlayerPrefs.GetInt("Setting_MuteSFX", 0) == 1;

        if (musicSource != null) musicSource.mute = musicMuteState;
        if (sfxSource != null) sfxSource.mute = sfxMuteState;
    }

    public void PlaySFX(string soundName)
    {
        // Comprobación estricta del estado del Source
        if (sfxSource == null || sfxSource.mute) return;

        if (sfxDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void SetMusicMute(bool muteState)
    {
        if (musicSource == null) return;
        musicSource.mute = muteState;
        PlayerPrefs.SetInt("Setting_MuteMusic", muteState ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSFXMute(bool muteState)
    {
        if (sfxSource == null) return;
        sfxSource.mute = muteState;
        PlayerPrefs.SetInt("Setting_MuteSFX", muteState ? 1 : 0);
        PlayerPrefs.Save();
    }
}