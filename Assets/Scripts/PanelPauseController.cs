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
        public Button PlayAgainButton;
        public Button MainMenuButton;

        public override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // En una partida online reiniciar la escena no tiene sentido: solo quedan reanudar y salir (que cierra la sesion)
            bool online = MyGame.Networking.OnlineMatchFlow.IsOnline;

            // 1. Vinculación de botones de navegación normales
            if (pauseButton != null) pauseButton.onClick.AddListener(OpenPauseMenu);
            if (resumeButton != null) resumeButton.onClick.AddListener(ClosePauseMenu);
            if (restartButton != null)
            {
                if (online) restartButton.gameObject.SetActive(false);
                else restartButton.onClick.AddListener(RestartGame);
            }
            // Acceso al panel de ajustes unico (idioma, volumenes) desde la pausa
            OnlineUiKit kit = OnlineUiKit.Instance;
            if (kit != null)
            {
                kit.CreateButton(transform, OnlineUiKit.ButtonStyle.Neutral, "AJUSTES", new Vector2(0.5f, 0.45f), new Vector2(420, 120), () => SettingsPanel.Open(null));
            }
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(ReturnToMainMenu);
            if (PlayAgainButton != null)
            {
                if (online)
                {
                    // Online: otra busqueda del mismo tipo (partida rapida) o revancha con el mismo rival (por IP)
                    PlayAgainButton.gameObject.AddComponent<MyGame.Networking.RematchButtonFeedback>();
                    PlayAgainButton.onClick.AddListener(() => MyGame.Networking.OnlineMatchFlow.PlayAgain(PlayAgainButton));
                }
                else PlayAgainButton.onClick.AddListener(RestartGame);
            }
            if (MainMenuButton != null) MainMenuButton.onClick.AddListener(ReturnToMainMenu);

            // El sonido «Click» de estos botones lo añade UiButtonSfx (respeta volumen y mute); no se cablea aquí.

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

        /// <summary>El menu de pausa esta abierto.</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        /// <summary>Boton Atras de Android: cierra la pausa si esta abierta y, si no, la abre.</summary>
        public void ToggleFromBackButton()
        {
            if (IsOpen) ClosePauseMenu();
            else OpenPauseMenu();
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
            // Online: salir de verdad (cierra la sesion y la conexion antes de volver al menu)
            if (MyGame.Networking.OnlineMatchFlow.IsOnline)
            {
                DG.Tweening.DOTween.KillAll();
                MyGame.Networking.OnlineMatchFlow.LeaveToMenu();
                return;
            }

            Time.timeScale = 1f;
            DG.Tweening.DOTween.KillAll();
            SceneManager.LoadScene(1);
        }
    }
}