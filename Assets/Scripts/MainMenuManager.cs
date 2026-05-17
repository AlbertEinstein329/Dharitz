using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviour
{
    [Header("Referencias a Datos")]
    public SessionConfig sessionConfig; // Arrastra aquí tu 'DatosDeSesionActual'
    public List<VariantData> availableVariants; // Arrastra aquí tus 3 variantes

    [Header("Paneles UI")]
    public GameObject mainPanel;
    public GameObject freePlayPanel;

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

        ShowMainPanel();
    }

    // --- NAVEGACIÓN ---
    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        freePlayPanel.SetActive(false);
    }

    public void ShowFreePlayPanel()
    {
        mainPanel.SetActive(false);
        freePlayPanel.SetActive(true);
    }

    // --- CONFIGURACIÓN DE PARTIDA ---
    // Conecta estos a botones de números (1, 2, 3, 4) o a un Dropdown
    public void SetPlayerCount(int numero)
    {
        sessionConfig.playerCount = Mathf.Clamp(numero, 1, 4);
        Debug.Log($"Jugadores configurados a: {sessionConfig.playerCount}");
    }

    // Conecta esto a un Dropdown de Variantes (0 = Var 1, 1 = Var 2, etc.)
    public void SetVariant(int indiceDropdown)
    {
        if (indiceDropdown >= 0 && indiceDropdown < availableVariants.Count)
        {
            sessionConfig.selectedVariant = availableVariants[indiceDropdown];
            Debug.Log($"Variante seleccionada: {sessionConfig.selectedVariant.variantName}");
        }
    }

    // --- CONFIGURACIÓN DE BOTS (USANDO TOGGLES) ---

    // Estos métodos reciben el 'bool' directamente del Toggle de Unity
    public void SetPlayer1Bot(bool isBot) { UpdateBotState(0, isBot); }
    public void SetPlayer2Bot(bool isBot) { UpdateBotState(1, isBot); }
    public void SetPlayer3Bot(bool isBot) { UpdateBotState(2, isBot); }
    public void SetPlayer4Bot(bool isBot) { UpdateBotState(3, isBot); }

    // El método central que modifica el maletín de sesión
    private void UpdateBotState(int playerIndex, bool isBot)
    {
        if (playerIndex < sessionConfig.players.Count)
        {
            sessionConfig.players[playerIndex].isBot = isBot;
            Debug.Log($"Jugador {playerIndex + 1} configurado como Bot: {isBot}");
        }
    }

    // --- INICIAR JUEGO ---
    public void StartFreePlay()
    {
        sessionConfig.isCampaignMode = false;
        // Asume que tu escena de juego es la número 2 en los Build Settings
        SceneManager.LoadScene(2);
    }

    public void StartCampaign()
    {
        // En futuras versiones cargaremos el mapa aquí
        Debug.Log("Modo campaña aún en desarrollo.");
    }
}