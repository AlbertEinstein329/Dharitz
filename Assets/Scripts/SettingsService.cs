using System;
using UnityEngine;

/// <summary>
/// Unica fuente de verdad de los ajustes del jugador (sonido, musica, idioma, modo zurdo) sobre PlayerPrefs.
/// Menu, pausa y AudioManager leen y escriben aqui, asi que nunca se desincronizan.
///
/// Se conservan las claves que ya existian (Volume_BGM, Volume_SFX, Setting_MuteMusic, Setting_MuteSFX, Setting_Language,
/// Setting_LeftHanded), de modo que los ajustes guardados por versiones anteriores siguen valiendo sin migracion.
/// </summary>
public static class SettingsService
{
    public const string KeyMusicVolume = "Volume_BGM";
    public const string KeySfxVolume = "Volume_SFX";
    public const string KeyMusicMuted = "Setting_MuteMusic";
    public const string KeySfxMuted = "Setting_MuteSFX";
    public const string KeyLanguage = "Setting_Language";
    public const string KeyLeftHanded = "Setting_LeftHanded";

    public const float DefaultMusicVolume = 0.75f;
    public const float DefaultSfxVolume = 0.80f;

    /// <summary>Se dispara cada vez que cambia cualquier ajuste (tambien desde otra pantalla).</summary>
    public static event Action Changed;

    public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, DefaultMusicVolume));
    public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, DefaultSfxVolume));
    public static bool MusicMuted => PlayerPrefs.GetInt(KeyMusicMuted, 0) == 1;
    public static bool SfxMuted => PlayerPrefs.GetInt(KeySfxMuted, 0) == 1;
    public static int LanguageIndex => PlayerPrefs.GetInt(KeyLanguage, 0);
    public static bool LeftHanded => PlayerPrefs.GetInt(KeyLeftHanded, 0) == 1;

    /// <summary>Volumen que realmente debe sonar: 0 si esta silenciado.</summary>
    public static float EffectiveMusicVolume => MusicMuted ? 0f : MusicVolume;
    public static float EffectiveSfxVolume => SfxMuted ? 0f : SfxVolume;

    public static void SetMusicVolume(float value) => SetFloat(KeyMusicVolume, Mathf.Clamp01(value));
    public static void SetSfxVolume(float value) => SetFloat(KeySfxVolume, Mathf.Clamp01(value));
    public static void SetMusicMuted(bool muted) => SetInt(KeyMusicMuted, muted ? 1 : 0);
    public static void SetSfxMuted(bool muted) => SetInt(KeySfxMuted, muted ? 1 : 0);
    public static void SetLanguageIndex(int index) => SetInt(KeyLanguage, Mathf.Max(0, index));
    public static void SetLeftHanded(bool value) => SetInt(KeyLeftHanded, value ? 1 : 0);

    /// <summary>Vuelca los cambios a disco. Se llama al cerrar paneles, al pausar y al salir (no en cada paso de un slider).</summary>
    public static void Save() => PlayerPrefs.Save();

    private static void SetFloat(string key, float value)
    {
        if (Mathf.Approximately(PlayerPrefs.GetFloat(key, float.NaN), value)) return;
        PlayerPrefs.SetFloat(key, value);
        Changed?.Invoke();
    }

    private static void SetInt(string key, int value)
    {
        if (PlayerPrefs.GetInt(key, int.MinValue) == value) return;
        PlayerPrefs.SetInt(key, value);
        Changed?.Invoke();
    }
}
