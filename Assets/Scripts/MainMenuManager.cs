using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    [Header("Referencias a Datos")]
    public SessionConfig sessionConfig; // Arrastra aquí tu 'DatosDeSesionActual'
    public List<VariantData> availableVariants; // Arrastra aquí tus 3 variantes

    [Header("Paneles UI (Navegación)")]
    public GameObject mainPanel;
    public GameObject freePlayPanel;
    public GameObject settingsPanel; // NUEVO: Panel de ajustes integrado

    [Header("Componentes de UI de Ajustes")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle leftHandedToggle;
    [SerializeField] private TMP_Dropdown languageDropdown;

    private void Awake()
    {
        // Añadimos patrón Singleton por si la Tienda o sub-sistemas necesitan referenciar el menú
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Limpiamos los datos de partidas anteriores al abrir el juego
        if (sessionConfig != null)
        {
            sessionConfig.ResetSession();
            // Por defecto asignamos la primera variante
            if (availableVariants.Count > 0)
                sessionConfig.selectedVariant = availableVariants[0];
        }

        // NUEVO: Inicializar configuraciones persistentes
        LoadSettings();

        // NUEVO: Asignamos listeners de los ajustes por código para evitar errores en el inspector
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (leftHandedToggle != null) leftHandedToggle.onValueChanged.AddListener(SetLeftHandedMode);
        if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(SetLanguage);

        ShowMainPanel();
    }

    // ==========================================
    // --- MÓDULO DE NAVEGACIÓN CENTRALIZADO ---
    // ==========================================
    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        freePlayPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    public void ShowFreePlayPanel()
    {
        mainPanel.SetActive(false);
        freePlayPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    public void ShowSettingsPanel()
    {
        mainPanel.SetActive(false);
        freePlayPanel.SetActive(false);
        settingsPanel.SetActive(true);
        LoadSettings(); // Sincronizamos componentes visuales al abrir
    }

    // ==========================================
    // --- LÓGICA DE CONFIGURACIÓN Y PERSISTENCIA ---
    // ==========================================
    private void SetBGMVolume(float value)
    {
        PlayerPrefs.SetFloat("Volume_BGM", value);
        // AudioManager.Instance.UpdateBGMVolume(value); // Próximamente
    }

    private void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat("Volume_SFX", value);
        // AudioManager.Instance.UpdateSFXVolume(value); // Próximamente
    }

    private void SetLeftHandedMode(bool isOn)
    {
        PlayerPrefs.SetInt("Setting_LeftHanded", isOn ? 1 : 0);
        Debug.Log($"[Ajustes] Modo Zurdo: {isOn}");
    }

    private void SetLanguage(int index)
    {
        PlayerPrefs.SetInt("Setting_Language", index);
        // LocalizationManager.Instance.ChangeLanguage(index); // Próximamente
    }

    private void LoadSettings()
    {
        if (bgmSlider != null) bgmSlider.value = PlayerPrefs.GetFloat("Volume_BGM", 0.75f);
        if (sfxSlider != null) sfxSlider.value = PlayerPrefs.GetFloat("Volume_SFX", 0.80f);

        if (leftHandedToggle != null)
        {
            int leftHanded = PlayerPrefs.GetInt("Setting_LeftHanded", 0);
            leftHandedToggle.SetIsOnWithoutNotify(leftHanded == 1);
        }

        if (languageDropdown != null)
        {
            int language = PlayerPrefs.GetInt("Setting_Language", 0);
            languageDropdown.SetValueWithoutNotify(language);
        }
    }

    // ==========================================
    // --- CONFIGURACIÓN DE PARTIDA (MODO LIBRE) ---
    // ==========================================

    /// <summary>
    /// Añade un nuevo jugador a la sesión actual (Máximo 4)
    /// </summary>
    public void AddPlayerDynamic()
    {
        if (sessionConfig == null) return;

        int currentCount = sessionConfig.players.Count;

        if (currentCount < 4)
        {
            // CORRECCIÓN: Usamos PlayerSetup (configuración) y no PlayerData (in-game)
            PlayerSetup newPlayer = new PlayerSetup();
            newPlayer.playerName = $"Jugador {currentCount + 1}";
            newPlayer.isBot = false;
            newPlayer.avatarId = 0; // Ahora Unity ya reconocerá esto gracias al Paso 1

            sessionConfig.players.Add(newPlayer);
            Debug.Log($"[Modo Libre] Jugador {sessionConfig.players.Count} añadido.");
        }
    }

    /// <summary>
    /// Elimina al último jugador añadido (Mínimo 1)
    /// </summary>
    public void RemoveLastPlayerDynamic()
    {
        if (sessionConfig == null) return;

        int currentCount = sessionConfig.players.Count;
        if (currentCount > 1)
        {
            sessionConfig.players.RemoveAt(currentCount - 1);
            Debug.Log($"[Modo Libre] Último jugador eliminado. Total: {sessionConfig.players.Count}");
        }
    }

    public void SetVariant(int indiceDropdown)
    {
        if (sessionConfig == null) return;

        if (indiceDropdown >= 0 && indiceDropdown < availableVariants.Count)
        {
            sessionConfig.selectedVariant = availableVariants[indiceDropdown];
            Debug.Log($"Variante seleccionada desde Dropdown: {sessionConfig.selectedVariant.variantName}");
        }
    }

    // --- MODIFICADORES INDIVIDUALES EN TIEMPO REAL ---

    public void UpdatePlayerName(int playerIndex, string newName)
    {
        if (sessionConfig != null && playerIndex < sessionConfig.players.Count)
        {
            sessionConfig.players[playerIndex].playerName = newName;
        }
    }

    public void UpdatePlayerAvatar(int playerIndex, int avatarId)
    {
        if (sessionConfig != null && playerIndex < sessionConfig.players.Count)
        {
            sessionConfig.players[playerIndex].avatarId = avatarId;
        }
    }
    public void UpdatePlayerBotState(int playerIndex, bool isBot)
    {
        if (sessionConfig != null && playerIndex < sessionConfig.players.Count)
        {
            sessionConfig.players[playerIndex].isBot = isBot;
            Debug.Log($"Jugador {playerIndex + 1} cambiado a Bot = {isBot}");
        }
    }

    // ==========================================
    // --- INICIAR JUEGO ---
    // ==========================================
    public void StartFreePlay()
    {
        if (sessionConfig == null || sessionConfig.players.Count == 0)
        {
            Debug.LogError("No se puede iniciar: No hay jugadores configurados.");
            return;
        }

        sessionConfig.isCampaignMode = false;
        SceneManager.LoadScene(2);
    }
}