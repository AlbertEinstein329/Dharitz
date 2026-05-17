using UnityEngine;

/// <summary>
/// Handles all sound effects (SFX) in the game. 
/// Designed to be called easily without holding heavy references.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip drawDieClip;
    [SerializeField] private AudioClip placeDieClip;
    [SerializeField] private AudioClip rollTickClip; // Sonido rápido para la animación

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlayDrawSound()
    {
        if (sfxSource.mute) return;
        if (drawDieClip != null) sfxSource.PlayOneShot(drawDieClip);
    }

    public void PlayPlaceSound()
    {
        if (sfxSource.mute) return;
        if (placeDieClip != null) sfxSource.PlayOneShot(placeDieClip);
    }

    public void PlayTickSound()
    {
        if (sfxSource.mute) return;
        if (rollTickClip != null) sfxSource.PlayOneShot(rollTickClip);
    }

    public void ToggleMusic()
    {
        if (musicSource != null)
        {
            musicSource.mute = !musicSource.mute;
        }
    }

    public void ToggleSFX()
    {
        if (sfxSource != null)
        {
            sfxSource.mute = !sfxSource.mute;
        }
    }

    public bool IsMusicMuted => musicSource != null && musicSource.mute;
    public bool IsSfxMuted => sfxSource != null && sfxSource.mute;
}