using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct SoundEntry
{
    public string soundName;
    public AudioClip clip;
}

/// <summary>
/// Gestor de audio unico y persistente. Se crea solo desde Resources/AudioManager.prefab antes de cargar la primera
/// escena, asi existe igual si se arranca desde Login, el menu o el tablero (ya no hay copias por escena).
/// Los volumenes y el mute salen de <see cref="SettingsService"/> y se aplican al AudioMixer (grupos Music y SFX);
/// si el prefab no tiene mixer asignado, se aplican directamente a los AudioSource.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string PrefabResource = "AudioManager";
    private const string MusicParameter = "MusicVolume";
    private const string SfxParameter = "SFXVolume";
    private const float SilenceDb = -80f;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Mixer (Music / SFX)")]
    [Tooltip("MainMixer con los parametros expuestos MusicVolume y SFXVolume.")]
    [SerializeField] private AudioMixer mixer;

    [Header("Background Music")]
    [Tooltip("Arrastra aquí el archivo .mp3 o .wav de la música de fondo principal.")]
    [SerializeField] private AudioClip backgroundMusicClip;

    [Header("Sound Library")]
    [SerializeField] private SoundEntry[] sfxLibrary;

    private Dictionary<string, AudioClip> sfxDictionary;

    // Propiedades públicas para que los Toggles de la UI consulten el estado real del sistema
    public bool IsMusicMuted => SettingsService.MusicMuted;
    public bool IsSFXMuted => SettingsService.SfxMuted;

    /// <summary>Crea el AudioManager desde Resources si ninguna escena lo ha aportado todavia.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<GameObject>(PrefabResource);
        if (prefab == null)
        {
            Debug.LogWarning($"[AudioManager] No existe Resources/{PrefabResource}.prefab; se usara el de la escena si lo hay.");
            return;
        }

        Instantiate(prefab).name = "[AudioManager]";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDictionary();
        RouteSourcesToMixer();
        SettingsService.Changed += ApplySettings;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySettings();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SettingsService.Changed -= ApplySettings;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        if (Instance != this) return;

        StartCoroutine(AttachClickSoundNextFrame()); // primera escena

        // Inicializa y reproduce la música de fondo si hay un clip asignado
        if (musicSource != null && backgroundMusicClip != null && !musicSource.isPlaying)
        {
            musicSource.clip = backgroundMusicClip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(AttachClickSoundNextFrame());

    // Un fotograma despues, para incluir tambien los botones que las escenas crean en su Start (p. ej. el HUD online)
    private IEnumerator AttachClickSoundNextFrame()
    {
        yield return new WaitForEndOfFrame();
        UiButtonSfx.AttachToAll();
    }

    // Los ajustes se guardan al pausar la app o salir (en el movil el proceso puede morir sin pasar por OnDestroy)
    private void OnApplicationPause(bool paused) { if (paused) SettingsService.Save(); }
    private void OnApplicationQuit() { SettingsService.Save(); }

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

    private void RouteSourcesToMixer()
    {
        if (mixer == null) return;

        if (musicSource != null && musicSource.outputAudioMixerGroup == null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("Music");
            if (groups.Length > 0) musicSource.outputAudioMixerGroup = groups[0];
        }

        if (sfxSource != null && sfxSource.outputAudioMixerGroup == null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
            if (groups.Length > 0) sfxSource.outputAudioMixerGroup = groups[0];
        }
    }

    /// <summary>Aplica volumen y mute actuales al mixer (o a los AudioSource si no hay mixer).</summary>
    private void ApplySettings()
    {
        float music = SettingsService.EffectiveMusicVolume;
        float sfx = SettingsService.EffectiveSfxVolume;

        if (mixer != null)
        {
            mixer.SetFloat(MusicParameter, ToDecibels(music));
            mixer.SetFloat(SfxParameter, ToDecibels(sfx));
        }
        else
        {
            if (musicSource != null) musicSource.volume = music;
            if (sfxSource != null) sfxSource.volume = sfx;
        }
    }

    /// <summary>Volumen lineal 0..1 a decibelios del mixer (curva logaritmica; 0 = silencio total).</summary>
    public static float ToDecibels(float linear)
    {
        return linear <= 0.0001f ? SilenceDb : Mathf.Clamp(Mathf.Log10(linear) * 20f, SilenceDb, 0f);
    }

    public void PlaySFX(string soundName)
    {
        if (sfxSource == null || SettingsService.SfxMuted || SettingsService.SfxVolume <= 0f) return;

        if (sfxDictionary.TryGetValue(soundName, out AudioClip clip) && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void SetMusicMute(bool muteState) => SettingsService.SetMusicMuted(muteState);
    public void SetSFXMute(bool muteState) => SettingsService.SetSfxMuted(muteState);
}
