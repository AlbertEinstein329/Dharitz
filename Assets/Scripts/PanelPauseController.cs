using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class PanelPauseController : BasePanel
    {
        public static PanelPauseController Instance { get; private set; }

        [Header("Pause Menu Buttons (Asigna los botones aquí)")]

        [Tooltip("Botón para abrir el menú de pausa")]
        public Button pauseButton;

        [Tooltip("Botón para cerrar el menú de pausa y volver al juego")]
        public Button resumeButton;
        
        [Tooltip("Botón para abrir las opciones avanzadas (opcional)")]
        public Button optionsButton;
        
        [Tooltip("Botón para reiniciar el nivel actual")]
        public Button restartButton;
        
        [Tooltip("Botón para volver al menú principal")]
        public Button backToMenuButton;
        
        [Tooltip("Botón para alternar la vista entre los tableros de los jugadores")]
        public Button changeBoardButton;
        
        [Tooltip("Botón para encender/apagar la música")]
        public Toggle toggleMusicButton;
        
        [Tooltip("Botón para encender/apagar los efectos de sonido (SFX)")]
        public Toggle toggleSfxButton;

        public override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(() => Show());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => Hide());
            if (optionsButton != null) optionsButton.onClick.AddListener(() => OptionsPanel.Instance.Show(this));
            
            if (restartButton != null) restartButton.onClick.AddListener(() => RestartGame());
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(() => ReturnToMainMenu());
            
            if (changeBoardButton != null) changeBoardButton.onClick.AddListener(() => ChangeBoard());
            
            if (toggleMusicButton != null) toggleMusicButton.onValueChanged.AddListener((value) => ToggleMusic());
            if (toggleSfxButton != null) toggleSfxButton.onValueChanged.AddListener((value) => ToggleSFX());
        }

        private void RestartGame()
        {
            DG.Tweening.DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            global::GameManager.Instance = null;
            SceneManager.LoadScene(1);
        }

        private void ChangeBoard()
        {
            if (global::GameManager.Instance != null && global::GameManager.Instance.gridManager != null)
            {
                global::GameManager.Instance.gridManager.ViewNextBoard();
            }
        }

        private void ToggleMusic()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleMusic();
                // Aquí podrías agregar lógica para cambiar la imagen del botón (ej. ícono tachado)
            }
        }

        private void ToggleSFX()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleSFX();
                // Aquí podrías agregar lógica para cambiar la imagen del botón (ej. ícono tachado)
            }
        }
    }
}