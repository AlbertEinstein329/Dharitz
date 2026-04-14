using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PlayerData;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Referencias Externas")]
    public GridManager gridManager;

    [Header("Configuracion de Partida")]
    public SessionConfig sesionActual;
    [HideInInspector] public VariantData varianteActual;
    [HideInInspector] public int numPlayers;
    public List<DieColor> diceBag = new List<DieColor>();
    public List<PlayerData> players = new List<PlayerData>();

    [Header("Estado del Turno")]
    public int currentPlayerIndex = 0;

    [Header("Referencias de UI")]
    public Button drawButton;
    public Button reDrawButton; // <-- NUEVO
    public UnityEngine.UI.Text TextMeshProUGUI;
    public DieColor currentDrawnColor;
    public bool hasDrawn = false;
    public bool isGameOver = false;
    public GroupData activeGroup = null;

    void Awake()
    {
        // 1. EL ÚNICO Y VERDADERO SINGLETON
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // Si ya existe OTRA instancia que no soy yo, me destruyo.
            Destroy(gameObject);
            return; // ¡Crucial! Evita que el resto del código se ejecute antes de morir.
        }

        // --- LECTURA DEL MALETÍN DE SESIÓN ---
        if (sesionActual != null)
        {
            varianteActual = sesionActual.varianteSeleccionada;
            numPlayers = sesionActual.numeroDeJugadores;

            // Reconstruimos la lista de jugadores basada en la sesión
            players = new List<PlayerData>();
            for (int i = 0; i < numPlayers; i++)
            {
                PlayerSetup setup = sesionActual.jugadores[i];
                players.Add(new PlayerData(i, setup.nombre, setup.esBot, setup.dificultadBot));
            }
        }
        else
        {
            Debug.LogError("Falta el SessionConfig. Cargando configuraciones por defecto a prueba de fallos.");
        }

        // Esto asegura que los números sean diferentes cada vez que abres el juego
        Random.InitState((int)System.DateTime.Now.Ticks);

        // Como ya pasamos el control de seguridad del Singleton con éxito, inicializamos:
        InitializePlayers();
    }

    void Start()
    {
        InitializeBag();

        // ¡CRÍTICO! Arrancamos el ciclo de turnos al iniciar la partida
        StartTurn();
    }

    void InitializePlayers()
    {
        players.Clear();
        for (int i = 0; i < numPlayers; i++)
        {
            // Mantenemos la bandera isBot, la cual servirá para identificar qué jugador será controlado por ML-Agents
            bool isBot = (i == 1);
            players.Add(new PlayerData(i + 1, $"Jugador {i + 1}", isBot, 0));
        }
    }

    public void StartTurn()
    {
        PlayerData currentPlayer = players[currentPlayerIndex];

        if (currentPlayer.isBot)
        {
            // Bloqueamos el botón de extraer para que el humano no interactúe en turno del agente
            if (drawButton != null) drawButton.interactable = false;

            // --- ESPACIO PREPARADO PARA ML-AGENTS ---
            // Aquí enlazaremos la solicitud de decisión del agente neuronal.
            // Ejemplo futuro: mlAgentComponent.RequestDecision();
            Debug.Log($"Turno de ML-Agent ({currentPlayer.name}). Esperando red neuronal...");
        }
        else
        {
            // Es turno del humano, habilitamos el botón
            HabilitarBotonExtraer();
        }

        if (reDrawButton != null) reDrawButton.interactable = false;

    }

    public void InitializeBag()
    {
        diceBag.Clear();
        int dicePerColor = 13 * numPlayers;

        for (int i = 0; i < dicePerColor; i++)
        {
            diceBag.Add(DieColor.Rojo);
            diceBag.Add(DieColor.Azul);
            diceBag.Add(DieColor.Blanco);
            diceBag.Add(DieColor.Negro);
        }

        ShuffleBag();
        Debug.Log($"Bolsa creada con {diceBag.Count} dados para {numPlayers} jugadores.");
        UIManager.Instance.ActualizarContadorBolsa(diceBag.Count);
    }

    private void ShuffleBag()
    {
        for (int i = 0; i < diceBag.Count; i++)
        {
            DieColor temp = diceBag[i];
            int randomIndex = Random.Range(i, diceBag.Count);
            diceBag[i] = diceBag[randomIndex];
            diceBag[randomIndex] = temp;
        }
    }

    public void DrawDie()
    {
        if (diceBag == null || diceBag.Count == 0)
        {
            Debug.Log("Bolsa vacía. ¡Es tu último movimiento!");
            if (drawButton != null) drawButton.interactable = false;
            return;
        }

        if (hasDrawn) return;
        if (players == null || players.Count == 0) return;

        currentDrawnColor = diceBag[0];
        diceBag.RemoveAt(0);
        hasDrawn = true;

        UIManager.Instance.ActualizarContadorBolsa(diceBag.Count);
        if (drawButton != null) drawButton.interactable = false;

        PlayerData currentPlayer = players[currentPlayerIndex];

        if (!currentPlayer.activeGroups.ContainsKey(currentDrawnColor))
        {
            currentPlayer.activeGroups[currentDrawnColor] = null;
        }

        GroupData group = currentPlayer.activeGroups[currentDrawnColor];

        if (group == null || group.isClosed)
        {
            int rolledNumber = Random.Range(1, 7);
            group = new GroupData
            {
                id = Random.Range(10000, 99999),
                color = currentDrawnColor,
                targetSize = rolledNumber
            };
            currentPlayer.activeGroups[currentDrawnColor] = group;
        }

        UIManager.Instance.ActualizarManoUI(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize);

        // Si es un humano quien sacó el dado, resaltamos sus opciones
        if (!currentPlayer.isBot)
        {
            gridManager.MostrarMovimientosValidos(currentPlayerIndex, group.color, group.id, group.targetSize);

            // Evaluamos si puede usar el Re-Draw
            if (reDrawButton != null)
            {
                reDrawButton.interactable = (currentPlayer.reDraws > 0);
            }
            if (TextMeshProUGUI != null)
            {
                TextMeshProUGUI.text = $"Re-Draw ({currentPlayer.reDraws})";
            }
        }
    }

    public void HabilitarBotonExtraer()
    {
        if (drawButton != null) drawButton.interactable = true;
    }

    public void RegisterPlacement(int r, int c)
    {
        PlayerData currentPlayer = players[currentPlayerIndex];
        GroupData group = currentPlayer.activeGroups[currentDrawnColor];

        if (group == null) return;

        group.occupiedCells.Add(new Vector2Int(r, c));
        currentPlayer.dadosColocados++;

        // Limpiamos los resaltados en cuanto el jugador hace su jugada
        gridManager.LimpiarResaltados(currentPlayerIndex);

        if (group.isClosed)
        {
            // 1. Obtenemos las reglas del patrón exacto desde nuestra Variante Actual
            PatternData patronAsignado = varianteActual.ObtenerPatron(group.targetSize);

            if (patronAsignado != null)
            {
                // 2. Evaluamos la forma pasando las reglas geométricas
                if (PatternValidator.CheckPattern(group.occupiedCells, patronAsignado))
                {
                    // REGISTRO DEL ÉXITO:
                    currentPlayer.conteoPatrones[group.targetSize]++;

                    int bono = ScoreManager.Instance.GetPatternBonus(group.targetSize);
                    currentPlayer.score += bono;

                    Debug.Log($"¡Patrón {group.targetSize} completado correctamente bajo las reglas de {varianteActual.nombreVariante}!");
                }
                else
                {
                    Debug.Log($"Grupo de {group.targetSize} cerrado, pero la forma no coincide con el patrón.");
                }
            }
        }

        UIManager.Instance.ActualizarManoUI(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize);
        hasDrawn = false;

        if (reDrawButton != null) reDrawButton.interactable = false;

        if (diceBag.Count == 0 && !hasDrawn)
        {
            TerminarPartida();
        }
        else
        {
            // Iniciamos la corrutina de retraso en lugar de saltar instantáneamente
            StartCoroutine(PausaCambioTurno());
        }
    }

    private IEnumerator PausaCambioTurno()
    {
        // Esperamos 1 segundos
        yield return new WaitForSeconds(1);

        // Ejecutamos el cambio de turno
        EndTurn();
    }

    public void EndTurn()
    {
        hasDrawn = false;
        currentPlayerIndex = (currentPlayerIndex + 1) % numPlayers;

        Debug.Log($"Turno finalizado. Ahora es el turno del Jugador {currentPlayerIndex + 1}");

        // SOLUCIÓN 3: Cambiamos la cámara al tablero de quien le toca
        if (gridManager != null)
        {
            gridManager.SwitchViewTo(currentPlayerIndex);
        }

        // ¡Le avisamos al juego que inició el turno para que el bot pueda jugar!
        StartTurn();
    }

    private void TerminarPartida()
    {
        Debug.Log("Fin de la bolsa. Calculando resultados...");

        // 1. Marcamos que el juego terminó
        isGameOver = true;

        // 2. Le decimos a la UI que muestre los resultados del jugador que estamos viendo ahora mismo
        int viewedPlayer = gridManager.currentlyViewedPlayer;
        UIManager.Instance.MostrarResultadosFinales(viewedPlayer);
    }

    public void ReiniciarJuego()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void UsarReDraw()
    {
        PlayerData p = players[currentPlayerIndex];

        // Validaciones de seguridad
        if (!hasDrawn || p.reDraws <= 0 || p.isBot) return;

        // 1. Limpiar la memoria del grupo si estaba recién creado (vacío)
        if (p.activeGroups.ContainsKey(currentDrawnColor))
        {
            GroupData group = p.activeGroups[currentDrawnColor];
            if (group != null && group.occupiedCells.Count == 0)
            {
                p.activeGroups[currentDrawnColor] = null; // Destruimos el grupo para forzar un re-roll numérico luego
            }
        }

        // 2. Devolver el dado a la bolsa en una posición ALEATORIA
        int randomIndex = Random.Range(0, diceBag.Count + 1);
        diceBag.Insert(randomIndex, currentDrawnColor);

        // 3. Consumir el uso y resetear estado
        p.reDraws--;
        hasDrawn = false;

        // Apagamos las luces del tablero que se habían encendido para el dado anterior
        gridManager.LimpiarResaltados(currentPlayerIndex);

        // 4. Volver a extraer un dado nuevo automáticamente
        DrawDie();
    }

    // --- NAVEGACIÓN ---
    public void VolverAlMenuPrincipal()
    {
        // BUENA PRÁCTICA SENIOR: Si en el futuro implementas un botón de "Pausa" 
        // que ponga Time.timeScale = 0f, DEBES restaurarlo a 1f antes de cambiar 
        // de escena, o tu Menú Principal cargará completamente congelado.
        Time.timeScale = 1f;

        // Limpiamos referencias estáticas si es necesario (el Singleton se destruirá solo, pero es buena práctica)
        Instance = null;

        // Cargamos la Escena 0 (Menú Principal)
        SceneManager.LoadScene(0);
    }


}