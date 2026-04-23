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
        if (drawDieClip != null) sfxSource.PlayOneShot(drawDieClip);
    }

    public void PlayPlaceSound()
    {
        if (placeDieClip != null) sfxSource.PlayOneShot(placeDieClip);
    }

    public void PlayTickSound()
    {
        if (rollTickClip != null) sfxSource.PlayOneShot(rollTickClip);
    }
}