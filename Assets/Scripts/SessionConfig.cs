using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ConfiguracionDeSesion", menuName = "Dharitz/Configuracion de Sesion")]
public class SessionConfig : ScriptableObject
{
    [Header("Modo de Juego")]
    public bool esModoCampana = false;

    [Header("Reglas Seleccionadas")]
    public VariantData varianteSeleccionada;

    [Header("Configuración de Jugadores")]
    public int numeroDeJugadores = 1;
    public List<PlayerSetup> jugadores = new List<PlayerSetup>();

    // Inicializa la lista por defecto
    public void ResetearSesion()
    {
        esModoCampana = false;
        numeroDeJugadores = 1;
        jugadores.Clear();
        // Por defecto preparamos 4 espacios
        for (int i = 0; i < 4; i++)
        {
            jugadores.Add(new PlayerSetup
            {
                nombre = $"Jugador {i + 1}",
                esBot = false,
                dificultadBot = 0
            });
        }
    }
}

[System.Serializable]
public class PlayerSetup
{
    public string nombre;
    public bool esBot;
    public int dificultadBot;
}