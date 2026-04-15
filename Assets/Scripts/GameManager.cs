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
    public GameObject botonCancelar;
    public Button drawButton;
    public Button reDrawButton;
    public UnityEngine.UI.Text TextMeshProUGUI;
    public DieColor currentDrawnColor;
    public bool hasDrawn = false;
    public bool isGameOver = false;
    public GroupData activeGroup = null;
    private Coroutine rutinaConfirmacion; // Variable para controlar la cuenta atrás

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

    // --- NUEVO SISTEMA DE COLOCACIÓN CON BÚFER DE 2 SEGUNDOS ---

    // Este método reemplaza a RegisterPlacement. Ahora CellComponent debe llamar a este.
    public void IniciarColocacion(int r, int c)
    {
        GroupData group = players[currentPlayerIndex].activeGroups[currentDrawnColor];

        // 1. Mostrar visualmente el dado fantasma
        gridManager.ColocarDadoVisualTemporal(currentPlayerIndex, r, c, currentDrawnColor, group.targetSize);

        // --- UX SENIOR: Apagamos el botón de Re-Draw inmediatamente ---
        if (reDrawButton != null) reDrawButton.interactable = false;

        // Mostramos el botón de cancelar
        if (botonCancelar != null) botonCancelar.SetActive(true);

        // Iniciar cuenta regresiva de 2 segundos
        if (rutinaConfirmacion != null) StopCoroutine(rutinaConfirmacion);
        rutinaConfirmacion = StartCoroutine(RutinaConfirmarJugada(r, c));

        Debug.Log("Jugada pendiente... Tienes 2 segundos para cancelar.");
    }

    // Botón de cancelar / deshacer antes de los 2 segundos
    public void CancelarColocacion()
    {
        if (rutinaConfirmacion != null)
        {
            StopCoroutine(rutinaConfirmacion);

            // Borramos el fantasma
            gridManager.RemoverDadoVisualTemporal();

            // Ocultamos el botón de cancelar
            if (botonCancelar != null) botonCancelar.SetActive(false);

            // --- UX SENIOR: Le devolvemos el botón de Re-Draw (si aún le quedan usos) ---
            PlayerData p = players[currentPlayerIndex];
            if (reDrawButton != null) reDrawButton.interactable = (p.reDraws > 0);

            Debug.Log("Jugada cancelada. Elige otra celda o haz un Re-Draw.");
        }
    }

    private System.Collections.IEnumerator RutinaConfirmarJugada(int r, int c)
    {
        // ESPERA DE 2 SEGUNDOS
        yield return new WaitForSeconds(2.0f);

        // --- PASADOS LOS 2 SEGUNDOS: NO HAY VUELTA ATRÁS ---
        ConfirmarYProcesarPuntos(r, c);
    }

    private void ConfirmarYProcesarPuntos(int r, int c)
    {
        // 1. Ocultamos botones
        if (botonCancelar != null) botonCancelar.SetActive(false);
        if (reDrawButton != null) reDrawButton.interactable = false;

        PlayerData p = players[currentPlayerIndex];
        GroupData group = p.activeGroups[currentDrawnColor];

        // 2. Colocación física y lógica
        gridManager.RemoverDadoVisualTemporal();
        gridManager.FijarDadoEnLogica(currentPlayerIndex, r, c, currentDrawnColor, group.id, group.targetSize);

        group.occupiedCells.Add(new Vector2Int(r, c));
        p.dadosColocados++;
        gridManager.LimpiarResaltados(currentPlayerIndex);

        Vector3 posMundo = gridManager.ObtenerPosicionMundo(currentPlayerIndex, r, c);

        // --- 3. INICIA EL CÁLCULO MATEMÁTICO ---

        // A. Bono Base
        PopUpManager.Instance.MostrarPopUp(posMundo, "+50", Color.white);
        p.score += 50;

        // B. Conexiones Extra (Variantes)
        PatternData patronActual = sesionActual.varianteSeleccionada.ObtenerPatron(group.targetSize);
        if (patronActual != null && patronActual.reglaEspecial == PatternData.ReglaEspecial.ContactoDiagonalExtra)
        {
            int conexionesNuevas = gridManager.EscanearConexionesDiagonalesNuevas(currentPlayerIndex, r, c, currentDrawnColor, group.id);
            if (conexionesNuevas > 0)
            {
                int bono = conexionesNuevas * 100;
                p.score += bono;
                PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.up * 0.5f, $"+{bono}", Color.magenta);
            }
        }

        // C. Combos de Estructura (Vuelve el texto COMBO!)
        int puntosCombo = gridManager.EvaluarYCobrarCombosEnTiempoReal(currentPlayerIndex);
        if (puntosCombo > 0)
        {
            p.score += puntosCombo;
            PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.up * 1f, $"COMBO! +{puntosCombo}", Color.yellow);
        }

        // D. PENALIZACIÓN DE UNOS (Frío y directo: solo el número negativo)
        int unosMalColocados = gridManager.ObtenerPenalizacionesPorUnos(currentPlayerIndex);
        int multaTotalActual = unosMalColocados * 100;
        int diferenciaMulta = multaTotalActual - p.penalizacionUnosAcumulada;

        if (diferenciaMulta > 0)
        {
            p.score -= diferenciaMulta;
            p.penalizacionUnosAcumulada = multaTotalActual;
            PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.down * 0.5f, $"-{diferenciaMulta}", Color.red);
        }

        // E. Cierre de Patrón Perfecto (Texto PERFECT!)
        if (group.isClosed)
        {
            if (PatternValidator.CheckPattern(group.occupiedCells, patronActual))
            {
                p.conteoPatrones[group.targetSize]++;
                int bonoPatron = ScoreManager.Instance.GetPatternBonus(group.targetSize);
                p.score += bonoPatron;
                PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.down * 1f, $"PERFECT! +{bonoPatron}", Color.cyan);
            }
        }

        // --- 4. ACTUALIZACIÓN VISUAL ---
        UIManager.Instance.ActualizarManoUI(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize);
        UIManager.Instance.ActualizarScore(p.score);

        hasDrawn = false;

        // 5. Gestión de Turno
        if (diceBag.Count == 0 && !hasDrawn)
        {
            TerminarPartida();
        }
        else
        {
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