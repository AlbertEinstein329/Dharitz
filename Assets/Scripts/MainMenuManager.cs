using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviour
{
    [Header("Referencias a Datos")]
    public SessionConfig configuracionSesion; // Arrastra aquí tu 'DatosDeSesionActual'
    public List<VariantData> variantesDisponibles; // Arrastra aquí tus 3 variantes

    [Header("Paneles UI")]
    public GameObject panelPrincipal;
    public GameObject panelModoLibre;

    private void Start()
    {
        // Limpiamos los datos de partidas anteriores al abrir el juego
        if (configuracionSesion != null)
        {
            configuracionSesion.ResetearSesion();
            // Por defecto asignamos la primera variante
            if (variantesDisponibles.Count > 0)
                configuracionSesion.varianteSeleccionada = variantesDisponibles[0];
        }

        MostrarPanelPrincipal();
    }

    // --- NAVEGACIÓN ---
    public void MostrarPanelPrincipal()
    {
        panelPrincipal.SetActive(true);
        panelModoLibre.SetActive(false);
    }

    public void MostrarPanelModoLibre()
    {
        panelPrincipal.SetActive(false);
        panelModoLibre.SetActive(true);
    }

    // --- CONFIGURACIÓN DE PARTIDA ---
    // Conecta estos a botones de números (1, 2, 3, 4) o a un Dropdown
    public void SetNumeroDeJugadores(int numero)
    {
        configuracionSesion.numeroDeJugadores = Mathf.Clamp(numero, 1, 4);
        Debug.Log($"Jugadores configurados a: {configuracionSesion.numeroDeJugadores}");
    }

    // Conecta esto a un Dropdown de Variantes (0 = Var 1, 1 = Var 2, etc.)
    public void SetVariante(int indiceDropdown)
    {
        if (indiceDropdown >= 0 && indiceDropdown < variantesDisponibles.Count)
        {
            configuracionSesion.varianteSeleccionada = variantesDisponibles[indiceDropdown];
            Debug.Log($"Variante seleccionada: {configuracionSesion.varianteSeleccionada.nombreVariante}");
        }
    }

    // --- CONFIGURACIÓN DE BOTS (USANDO TOGGLES) ---

    // Estos métodos reciben el 'bool' directamente del Toggle de Unity
    public void SetPlayer1Bot(bool isBot) { ActualizarEstadoBot(0, isBot); }
    public void SetPlayer2Bot(bool isBot) { ActualizarEstadoBot(1, isBot); }
    public void SetPlayer3Bot(bool isBot) { ActualizarEstadoBot(2, isBot); }
    public void SetPlayer4Bot(bool isBot) { ActualizarEstadoBot(3, isBot); }

    // El método central que modifica el maletín de sesión
    private void ActualizarEstadoBot(int playerIndex, bool isBot)
    {
        if (playerIndex < configuracionSesion.jugadores.Count)
        {
            configuracionSesion.jugadores[playerIndex].esBot = isBot;
            Debug.Log($"Jugador {playerIndex + 1} configurado como Bot: {isBot}");
        }
    }

    // --- INICIAR JUEGO ---
    public void IniciarModoLibre()
    {
        configuracionSesion.esModoCampana = false;
        // Asume que tu escena de juego es la número 1 en los Build Settings
        SceneManager.LoadScene(1);
    }

    public void IniciarModoCampana()
    {
        // En futuras versiones cargaremos el mapa aquí
        Debug.Log("Modo campaña aún en desarrollo.");
    }
}