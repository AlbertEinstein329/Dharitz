using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Lean.Gui;

public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }
    private List<int> availableAvatarIds = new List<int>();

    [Header("Referencias a Datos")]
    public SessionConfig sessionConfig;
    public List<VariantData> availableVariants;

    [Header("Paneles UI (Navegación Canvas)")]
    public GameObject mainPanel;
    public GameObject freePlayPanel;
    public GameObject settingsPanel;
    public GameObject onlinePanel;

    [Header("Componentes Modo Libre Dinámico")]
    [SerializeField] private TMP_Dropdown variantDropdown;
    [SerializeField] private Transform rowsContainer; // El objeto con Vertical Layout Group
    [SerializeField] private GameObject playerRowPrefab; // Tu prefab con 'UIPlayerRowController'
    [SerializeField] private LeanButton addPlayerButton; // El botón físico (+) de Avatar con un Más

    [Header("Componentes Ajustes Persistentes")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle leftHandedToggle;
    [SerializeField] private TMP_Dropdown languageDropdown;

    private List<GameObject> activeVisualRows = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (sessionConfig != null)
        {
            sessionConfig.ResetSession();
            if (availableVariants.Count > 0)
                sessionConfig.selectedVariant = availableVariants[0];
        }

        LoadSettings();
        InitializeDropdownVariants();

        // Listeners limpios de Ajustes por código
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (leftHandedToggle != null) leftHandedToggle.onValueChanged.AddListener(SetLeftHandedMode);
        if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(SetLanguage);

        // Listener del botón dinámico (+)
        if (addPlayerButton != null) addPlayerButton.OnClick.AddListener(AddPlayerDynamic);

        ShowMainPanel();
    }

    // ==========================================
    // --- MÓDULO DE NAVEGACIÓN Y ANIMACIONES ---
    // ==========================================
    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        freePlayPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (onlinePanel != null) onlinePanel.SetActive(false);
    }

    public void ShowSettingsPanel()
    {
        mainPanel.SetActive(false);
        freePlayPanel.SetActive(false);
        settingsPanel.SetActive(true);
        if (onlinePanel != null) onlinePanel.SetActive(false);
        LoadSettings();
    }

    public void ShowOnlinePanel()
    {
        mainPanel.SetActive(false);
        freePlayPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (onlinePanel != null) onlinePanel.SetActive(true);
    }

    public async void StartMatchmaking2P()
    {
        if (MyGame.Networking.MultiplayerMatchmaker.Instance != null)
        {
            await MyGame.Networking.MultiplayerMatchmaker.Instance.StartMatchmakingAsync(MyGame.Networking.MatchMode.TwoPlayers);
        }
    }

    public async void StartMatchmaking4P()
    {
        if (MyGame.Networking.MultiplayerMatchmaker.Instance != null)
        {
            await MyGame.Networking.MultiplayerMatchmaker.Instance.StartMatchmakingAsync(MyGame.Networking.MatchMode.FourPlayers);
        }
    }

    public void CancelMatchmaking()
    {
        if (MyGame.Networking.MultiplayerMatchmaker.Instance != null)
        {
            MyGame.Networking.MultiplayerMatchmaker.Instance.CancelMatchmaking();
        }
        ShowMainPanel();
    }

    public void ShowFreePlayPanel()
    {
        mainPanel.SetActive(false);
        freePlayPanel.SetActive(true);
        settingsPanel.SetActive(false);

        // 1. Inicializamos la bolsa con todos los IDs de avatar disponibles (del 0 al 9)
        ResetAvailableAvatarsPool();

        if (sessionConfig != null)
        {
            sessionConfig.players.Clear();

            int initialAvatar = DrawRandomAvatarId();
            PlayerSetup p1 = new PlayerSetup { playerName = "Jugador 1", isBot = false, avatarId = initialAvatar };
            sessionConfig.players.Add(p1);

            // SINCRONIZACIÓN
            sessionConfig.playerCount = sessionConfig.players.Count;
        }

        // Ahora mandamos a pintar la interfaz en base a nuestra lista limpia de 1 solo jugador
        BuildDynamicPlayersInterface();
    }

    // --- MÉTODOS AUXILIARES PARA LA BOLSA DE AVATARES ---

    private void ResetAvailableAvatarsPool()
    {
        availableAvatarIds.Clear();
        // Asumiendo que tienes 10 avatares (IDs del 0 al 9)
        for (int i = 0; i < 10; i++)
        {
            availableAvatarIds.Add(i);
        }
    }

    /// <summary>
    /// Saca un ID de avatar al azar de la bolsa y lo elimina para que no se repita
    /// </summary>
    public int DrawRandomAvatarId()
    {
        if (availableAvatarIds.Count == 0) return 0; // Respaldo seguro si se vacía

        int randomIndex = Random.Range(0, availableAvatarIds.Count);
        int selectedAvatarId = availableAvatarIds[randomIndex];

        // Lo removemos de la bolsa para que el siguiente jugador no pueda sacarlo
        availableAvatarIds.RemoveAt(randomIndex);
        return selectedAvatarId;
    }

    /// <summary>
    /// Devuelve un ID de avatar a la bolsa cuando un jugador es eliminado
    /// </summary>
    public void ReturnAvatarIdToPool(int id)
    {
        if (!availableAvatarIds.Contains(id))
        {
            availableAvatarIds.Add(id);
        }
    }


    // ==========================================
    // --- CONSTRUCCIÓN DINÁMICA DE JUGADORES ---
    // ==========================================
    private void BuildDynamicPlayersInterface()
    {
        // 1. Limpieza de elementos visuales previos
        foreach (GameObject row in activeVisualRows) Destroy(row);
        activeVisualRows.Clear();

        if (sessionConfig == null) return;

        // Aseguramos que por lo menos exista el Jugador 1 en los datos
        if (sessionConfig.players.Count == 0)
        {
            PlayerSetup p1 = new PlayerSetup { playerName = "Jugador 1", isBot = false, avatarId = 0 };
            sessionConfig.players.Add(p1);
        }

        // 2. Instanciamos las ranuras físicas en el contenedor vertical
        for (int i = 0; i < sessionConfig.players.Count; i++)
        {
            SpawnPlayerRowVisual(i, sessionConfig.players[i].playerName);
        }

        UpdateAddButtonVisibility();
    }

    private void SpawnPlayerRowVisual(int index, string defaultName)
    {
        GameObject newRow = Instantiate(playerRowPrefab, rowsContainer);
        activeVisualRows.Add(newRow);

        UIPlayerRowController controller = newRow.GetComponent<UIPlayerRowController>();
        if (controller != null)
        {
            controller.SetupRow(index, defaultName);
        }

        // Forzamos al botón (+) a moverse automáticamente al fondo del Vertical Layout Group
        addPlayerButton.transform.SetAsLastSibling();
    }

    public void AddPlayerDynamic()
    {
        if (sessionConfig == null) return;

        int currentCount = sessionConfig.players.Count;

        if (currentCount < 4)
        {
            addPlayerButton.transform.DOPunchScale(new Vector3(-0.1f, -0.1f, 0f), 0.15f, 5, 0.5f);

            // CORRECCIÓN: Robamos un avatar único que no esté repetido
            int uniqueAvatarId = DrawRandomAvatarId();

            PlayerSetup newPlayer = new PlayerSetup { playerName = $"Jugador {currentCount + 1}", isBot = false, avatarId = uniqueAvatarId };
            sessionConfig.players.Add(newPlayer);

            // SINCRONIZACIÓN
            sessionConfig.playerCount = sessionConfig.players.Count;

            SpawnPlayerRowVisual(currentCount, newPlayer.playerName);
            UpdateAddButtonVisibility();
        }
    }

    /// <summary>
    /// Elimina un jugador específico de la pantalla y de los datos de forma quirúrgica
    /// </summary>
    public void RemovePlayerDynamic(int indexToRemove)
    {
        if (sessionConfig == null || indexToRemove == 0) return;

        if (indexToRemove < sessionConfig.players.Count)
        {

            // CORRECCIÓN: Antes de borrar al jugador de los datos, devolvemos su avatar a la bolsa
            int avatarToReturn = sessionConfig.players[indexToRemove].avatarId;
            ReturnAvatarIdToPool(avatarToReturn);

            // 1. Lo eliminamos del maletín de datos de la sesión
            sessionConfig.players.RemoveAt(indexToRemove);

            // SINCRONIZACIÓN
            sessionConfig.playerCount = sessionConfig.players.Count;

            // 2. Lo eliminamos quirúrgicamente de la lista visual sin borrar a los demás
            GameObject rowVisualToDelete = activeVisualRows[indexToRemove];
            activeVisualRows.RemoveAt(indexToRemove);
            Destroy(rowVisualToDelete); // Destrucción física en el motor de Unity

            // 3. PASO CLAVE: Reajustamos los índices de las ranuras que sobrevivieron
            // Esto evita que los IDs de los jugadores se queden desalineados
            for (int i = 0; i < activeVisualRows.Count; i++)
            {
                UIPlayerRowController controller = activeVisualRows[i].GetComponent<UIPlayerRowController>();
                if (controller != null)
                {
                    // Le avisamos a la ranura cuál es su nueva posición física (fila 1, 2, 3...)
                    // Pasamos false para indicarle al script que NO ejecute la animación de entrada de DOTween otra vez
                    controller.RefreshIndex(i);
                }
            }

            // 4. Evaluamos si el botón (+) debe reaparecer (si bajamos de 4 jugadores)
            UpdateAddButtonVisibility();
        }
    }

    private void UpdateAddButtonVisibility()
    {
        if (sessionConfig == null) return;

        // Desaparece por completo al llegar al límite físico de 4 competidores
        bool canAddMore = sessionConfig.players.Count < 4;

        // Animamos su desaparición de forma suave en vez de un corte seco
        addPlayerButton.transform.DOScale(canAddMore ? Vector3.one : Vector3.zero, 0.25f)
            .OnComplete(() => addPlayerButton.gameObject.SetActive(canAddMore));
    }

    // ==========================================
    // --- MODIFICADORES DE DATOS EN TIEMPO REAL ---
    // ==========================================
    private void InitializeDropdownVariants()
    {
        if (variantDropdown == null || availableVariants == null) return;

        // Intentamos recuperar el sprite del "Item Background" del Template (puede estar inactivo)
        Sprite itemBackgroundSprite = null;
        Image templateImage = variantDropdown.template.GetComponentInChildren<Image>(true);
        if (templateImage != null)
            itemBackgroundSprite = templateImage.sprite;

        // Limpiamos opciones existentes
        variantDropdown.options.Clear();

        // Creamos e inyectamos los nuevos textos dinámicos respetando el molde original
        foreach (var variant in availableVariants)
        {
            if (variant != null)
            {
                TMP_Dropdown.OptionData newOption = new TMP_Dropdown.OptionData();
                newOption.text = variant.variantName;
                // Si la variante tiene un icono, lo usamos; si no, usamos el background del template (por compatibilidad)
                if (variant.iconSprite != null)
                    newOption.image = variant.iconSprite;
                else if (itemBackgroundSprite != null)
                    newOption.image = itemBackgroundSprite;

                variantDropdown.options.Add(newOption);
            }
        }

        // Limpieza y asignación del listener
        variantDropdown.onValueChanged.RemoveAllListeners();
        variantDropdown.onValueChanged.AddListener(SetVariant);

        // Asignamos la imagen y color inicial al caption del dropdown (imagen principal que se ve cuando no está desplegado)
        if (availableVariants.Count > 0 && variantDropdown.captionImage != null)
        {
            var first = availableVariants[0];
            variantDropdown.captionImage.sprite = first.iconSprite != null ? first.iconSprite : itemBackgroundSprite;
            variantDropdown.captionImage.color = first.highlightColor;
            variantDropdown.captionImage.enabled = variantDropdown.captionImage.sprite != null;
        }

        // Forzamos la actualización visual sin romper el Template gráfico
        variantDropdown.value = 0;
        variantDropdown.RefreshShownValue();
    }

    public void SetVariant(int index)
    {
        if (sessionConfig != null && index >= 0 && index < availableVariants.Count)
        {
            sessionConfig.selectedVariant = availableVariants[index];
            Debug.Log($"Variante seteada: {sessionConfig.selectedVariant.variantName}");

            // Actualizamos la imagen principal (caption) del dropdown y su color de fondo
            if (variantDropdown != null && variantDropdown.captionImage != null)
            {
                var spriteToSet = availableVariants[index].iconSprite;
                variantDropdown.captionImage.sprite = spriteToSet;
                variantDropdown.captionImage.color = availableVariants[index].highlightColor;
                variantDropdown.captionImage.enabled = spriteToSet != null;
            }

            // Si quieres cambiar otra UI principal (p. ej. un background del panel), pon aquí la lógica:
            // mainPanel.GetComponent<Image>()?.color = availableVariants[index].highlightColor;
        }
    }

    public void UpdatePlayerName(int index, string newName)
    {
        if (sessionConfig != null && index < sessionConfig.players.Count)
            sessionConfig.players[index].playerName = newName;
    }

    public void UpdatePlayerAvatar(int index, int avatarId)
    {
        if (sessionConfig != null && index < sessionConfig.players.Count)
            sessionConfig.players[index].avatarId = avatarId;
    }

    public void UpdatePlayerBotState(int index, bool isBot)
    {
        if (sessionConfig != null && index < sessionConfig.players.Count)
            sessionConfig.players[index].isBot = isBot;
    }

    // ==========================================
    // --- MODULO SETTINGS (PERSISTENCIA) ---
    // ==========================================
    private void SetBGMVolume(float value) { PlayerPrefs.SetFloat("Volume_BGM", value); }
    private void SetSFXVolume(float value) { PlayerPrefs.SetFloat("Volume_SFX", value); }
    private void SetLeftHandedMode(bool isOn) { PlayerPrefs.SetInt("Setting_LeftHanded", isOn ? 1 : 0); }
    private void SetLanguage(int index) { PlayerPrefs.SetInt("Setting_Language", index); }

    private void LoadSettings()
    {
        if (bgmSlider != null) bgmSlider.value = PlayerPrefs.GetFloat("Volume_BGM", 0.75f);
        if (sfxSlider != null) sfxSlider.value = PlayerPrefs.GetFloat("Volume_SFX", 0.80f);
        if (leftHandedToggle != null) leftHandedToggle.isOn = PlayerPrefs.GetInt("Setting_LeftHanded", 0) == 1;
        if (languageDropdown != null) languageDropdown.value = PlayerPrefs.GetInt("Setting_Language", 0);
    }

    public void StartFreePlay()
    {
        if (sessionConfig == null || sessionConfig.players.Count == 0) return;
        sessionConfig.isCampaignMode = false;
        SceneManager.LoadScene(2);
    }
}