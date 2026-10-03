using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class PanelPauseController : BasePanel
    {
        public static PanelPauseController Instance { get; private set; }

        [Header("Menu Buttons")]
        public Button pauseButton;
        public Button resumeButton;
        public Button optionsButton;
        public Button restartButton;
        public Button backToMenuButton;
        public Toggle toggleMusicButton;
        public Toggle toggleSfxButton;

        public override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // 1. Vinculación de botones de navegación normales
            if (pauseButton != null) pauseButton.onClick.AddListener(OpenPauseMenu);
            if (resumeButton != null) resumeButton.onClick.AddListener(ClosePauseMenu);
            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(ReturnToMainMenu);

            // Vinculamos sonido Click genérico a los botones
            if (pauseButton != null) pauseButton.onClick.AddListener(() => AudioManager.Instance.PlaySFX("Click"));
            if (resumeButton != null) resumeButton.onClick.AddListener(() => AudioManager.Instance.PlaySFX("Click"));
            if (restartButton != null) restartButton.onClick.AddListener(() => AudioManager.Instance.PlaySFX("Click"));
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(() => AudioManager.Instance.PlaySFX("Click"));

            // 2. CONFIGURACIÓN REACTIVA DE TOGGLES ANTI-DESINCRONIZACIÓN
            if (AudioManager.Instance != null)
            {
                if (toggleMusicButton != null)
                {
                    // Forzamos al Toggle visual a reflejar el estado real del componente sin disparar eventos
                    toggleMusicButton.SetIsOnWithoutNotify(!AudioManager.Instance.IsMusicMuted);
                    // El estado del Toggle (isOn = true) significa "Sonando", por ende pasamos el valor invertido al Mute
                    toggleMusicButton.onValueChanged.AddListener((isOn) => AudioManager.Instance.SetMusicMute(!isOn));
                }

                if (toggleSfxButton != null)
                {
                    toggleSfxButton.SetIsOnWithoutNotify(!AudioManager.Instance.IsSFXMuted);
                    toggleSfxButton.onValueChanged.AddListener((isOn) => AudioManager.Instance.SetSFXMute(!isOn));
                }
            }
        }

        private void OpenPauseMenu()
        {
            Show();
            if (AudioManager.Instance != null)
            {
                if (toggleMusicButton != null) toggleMusicButton.SetIsOnWithoutNotify(!AudioManager.Instance.IsMusicMuted);
                if (toggleSfxButton != null) toggleSfxButton.SetIsOnWithoutNotify(!AudioManager.Instance.IsSFXMuted);
            }
        }

        private void ClosePauseMenu()
        {
            Hide();
            Time.timeScale = 1f;
        }

        private void RestartGame()
        {
            Time.timeScale = 1f;
            DG.Tweening.DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            DG.Tweening.DOTween.KillAll();
            SceneManager.LoadScene(1);
        }
    }
}