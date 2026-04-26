using DG.Tweening;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PlayerData;

public class GameManager : MonoBehaviour, ITurnProvider, IPlacementExecutor
{
    public static GameManager Instance;

    [Header("Referencias Externas")]
    public GridManager gridManager;

    [Header("Configuracion de Partida")]
    public SessionConfig sesionActual;
    [HideInInspector] public VariantData varianteActual;
    [HideInInspector] public int numPlayers;
    public int maxDicePerPlayer = 52;
    public List<DieColor> diceBag = new List<DieColor>();
    public List<PlayerData> players = new List<PlayerData>();

    [Header("Estado del Turno")]
    public int currentPlayerIndex = 0;

    [Header("UI Elements")]
    public GameObject botonCancelar;
    public Button drawButton;
    public UnityEngine.UI.Button reDrawButton;
    public TMPro.TextMeshProUGUI reDrawText;
    public DieColor currentDrawnColor;
    public bool hasDrawn = false;
    public bool isGameOver = false;
    public GroupData activeGroup = null;
    private Coroutine rutinaConfirmacion; // Variable para controlar la cuenta atrás

    // --- ITurnProvider Implementation ---
    public int CurrentPlayerIndex => currentPlayerIndex; // Exposes as read-only property
    public bool HasDrawn => hasDrawn;
    public DieColor CurrentDrawnColor => currentDrawnColor;

    public PlayerData GetCurrentPlayer()
    {
        return players[currentPlayerIndex];
    }
    void Awake()
    {
        // 1. EL ÚNICO Y VERDADERO SINGLETON
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Esto asegura que los números sean diferentes cada vez que abres el juego
        Random.InitState((int)System.DateTime.Now.Ticks);

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
            numPlayers = 2; // Default seguro
            InitializeFallbackPlayers(); // Solo se llama si hubo un error crítico
        }
    }

    void Start()
    {
        InitializeBag();

        //Arrancamos el ciclo de turnos al iniciar la partida
        StartTurn();

    }





    void InitializeFallbackPlayers()
    {
        players.Clear();
        for (int i = 0; i < numPlayers; i++)
        {
            // Solo para pruebas si no hay menú. Por defecto, todos humanos.
            players.Add(new PlayerData(i, $"Jugador Fallback {i + 1}", false, 0));
        }
    }

    public void StartTurn()
    {
        PlayerData currentPlayer = players[currentPlayerIndex];

        UIManager.Instance.SetDrawInputLock(false);

        if (currentPlayer.isBot)
        {
            // Bloqueamos el botón de extraer para que el humano no interactúe en turno del agente
            UIManager.Instance.SetDrawInputLock(true);

            // --- ESPACIO PREPARADO PARA ML-AGENTS ---
            // Aquí enlazaremos la solicitud de decisión del agente neuronal.
            // Ejemplo futuro: mlAgentComponent.RequestDecision();
            Debug.Log($"Turno de ML-Agent ({currentPlayer.name}). Esperando red neuronal...");
        }
        else
        {
            // Es turno del humano, habilitamos el botón
            UIManager.Instance.SetDrawInputLock(false);
        }

        if (reDrawButton != null) reDrawButton.interactable = false;

    }

    public void InitializeBag()
    {
        diceBag.Clear();
        int dicePerColor = 13 * numPlayers;

        for (int i = 0; i < dicePerColor; i++)
        {
            diceBag.Add(DieColor.Red);
            diceBag.Add(DieColor.Blue);
            diceBag.Add(DieColor.White);
            diceBag.Add(DieColor.Black);
        }

        ShuffleBag();
        Debug.Log($"Bolsa creada con {diceBag.Count} dados para {numPlayers} jugadores.");

        // 1. Contamos cuántos dados quedan de cada color en la bolsa
        int redLeft = diceBag.Count(d => d == DieColor.Red);
        int blueLeft = diceBag.Count(d => d == DieColor.Blue);
        int whiteLeft = diceBag.Count(d => d == DieColor.White);
        int blackLeft = diceBag.Count(d => d == DieColor.Black);

        // 2. Enviamos los 4 valores al UIManager
        UIManager.Instance.UpdateDiceCounters(redLeft, blueLeft, whiteLeft, blackLeft);

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

        // Llamamos a la UI pasando los parámetros completos y abrimos el Callback (Lambda)
        UIManager.Instance.UpdateHandUI(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize, () =>
        {

            int redLeft = diceBag.Count(d => d == DieColor.Red);
            int blueLeft = diceBag.Count(d => d == DieColor.Blue);
            int whiteLeft = diceBag.Count(d => d == DieColor.White);
            int blackLeft = diceBag.Count(d => d == DieColor.Black);

            // 2. Enviamos los 4 valores al UIManager
            UIManager.Instance.UpdateDiceCounters(redLeft, blueLeft, whiteLeft, blackLeft);


            if (!currentPlayer.isBot)
            {
                // Mostramos opciones válidas
                gridManager.ShowValidMoves(currentPlayerIndex, currentDrawnColor, group.id, group.targetSize);

                // Habilitamos el uso del Re-Draw solo cuando el dado final es visible
                if (reDrawButton != null)
                {
                    reDrawButton.interactable = (currentPlayer.reDraws > 0);
                }

                // NOTA ARQUITECTÓNICA: Renombra tu variable "TextMeshProUGUI" a "reDrawText" 
                // Usar el nombre de la clase como nombre de variable rompe los estándares de C#
                if (reDrawText != null)
                {
                    reDrawText.text = $"Re-Draw ({currentPlayer.reDraws})";
                }
            }
            else
            {
                // [PREPARACIÓN PARA VERSIÓN 0.4 - ML AGENTS]
                // Si es el bot, aquí es donde le daríamos la señal de que ya puede jugar su turno.
            }
        });
    }

    public void HabilitarBotonExtraer()
    {
        if (drawButton != null) drawButton.interactable = true;
    }

    // --- IPlacementExecutor Implementation ---
    public void IniciarColocacion(int row, int col)
    {
        GroupData group = players[currentPlayerIndex].activeGroups[currentDrawnColor];

        gridManager.ColocarDadoVisualTemporal(currentPlayerIndex, row, col, currentDrawnColor, group.targetSize);

        if (reDrawButton != null) reDrawButton.interactable = false;
        if (botonCancelar != null) botonCancelar.SetActive(true);

        if (rutinaConfirmacion != null) StopCoroutine(rutinaConfirmacion);
        rutinaConfirmacion = StartCoroutine(RutinaConfirmarJugada(row, col));


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
        yield return new WaitForSeconds(0.1f);

        // --- PASADOS LOS 2 SEGUNDOS: NO HAY VUELTA ATRÁS ---
        ConfirmarYProcesarPuntos(r, c);

        UIManager.Instance.ClearDieUI();
    }

    public void ConfirmarYProcesarPuntos(int r, int c)
    {
        //Evita que el jugador saque otro dado rápido
        UIManager.Instance.SetDrawInputLock(true);

        // 1. Ocultamos botones
        if (botonCancelar != null) botonCancelar.SetActive(false);
        if (reDrawButton != null) reDrawButton.interactable = false;

        PlayerData p = players[currentPlayerIndex];
        GroupData group = p.activeGroups[currentDrawnColor];

        // 2. Colocación física y lógica
        gridManager.RemoverDadoVisualTemporal();
        gridManager.FijarDadoEnLogica(currentPlayerIndex, r, c, currentDrawnColor, group.id, group.targetSize);


        group.occupiedCells.Add(new Vector2Int(r, c));
        p.placedDice++;
        // Cuando el jugador hace clic en la celda y confirma la jugada:
        gridManager.LimpiarResaltados(currentPlayerIndex);

        Vector3 posMundo = gridManager.ObtenerPosicionMundo(currentPlayerIndex, r, c);


        // --- 3. INICIA EL CÁLCULO MATEMÁTICO EXCLUYENTE ---

        PatternData patronActual = sesionActual.varianteSeleccionada.ObtenerPatron(group.targetSize);

        // A. EVALUACIÓN DE PENALIZACIÓN VS BONO BASE
        int contactosDiagonales = 0;
        int contactosTotales = gridManager.ContarContactosEn3x3(currentPlayerIndex, r, c, group.targetSize, out contactosDiagonales);

        SpecialRule reglaActiva = (SpecialRule)(int)patronActual.reglaEspecial;

        // NUEVO: Verificamos si es el primerísimo dado en el tablero del jugador.
        // (Como p.dadosColocados ya se incrementó en el paso 2, si vale 1, es el primer dado).
        bool isFirstDie = (p.placedDice == 1);

        // Pasamos el nuevo parámetro 'isFirstDie' a la evaluación
        RuleEvaluationResult result = SpecialRuleEvaluator.EvaluatePlacement(reglaActiva, contactosTotales, contactosDiagonales, isFirstDie);

        if (result.ScoreDelta < 0)
        {
            // HUBO PENALIZACIÓN: Restamos la multa y NO damos puntos base
            p.score += result.ScoreDelta;
            PopUpManager.Instance.MostrarPopUp(posMundo, $"{result.ScoreDelta}", Color.red);
        }
        else
        {
            // JUGADA LIMPIA o EXCEPCIÓN (Primer dado): Damos los 50 puntos base + el bono del contacto (si lo hay)
            int puntosGanados = 50 + result.ScoreDelta;
            p.score += puntosGanados;
            PopUpManager.Instance.MostrarPopUp(posMundo, $"+{puntosGanados}", Color.white);
        }

        // B. Conexiones Extra (Variantes del dado 2 y 3)
        if (patronActual != null && reglaActiva == SpecialRule.ExtraDiagonalContact)
        {
            int conexionesNuevas = gridManager.EscanearConexionesDiagonalesNuevas(currentPlayerIndex, r, c, currentDrawnColor, group.id);
            if (conexionesNuevas > 0)
            {
                int bono = conexionesNuevas * 200;
                p.score += bono;
                PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.up * 0.5f, $"+{bono}", Color.magenta);
            }
        }

        // C. Combos de Estructura 
        int puntosCombo = gridManager.EvaluateAndApplyCombos(currentPlayerIndex);
        if (puntosCombo > 0)
        {
            p.score += puntosCombo;
            PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.up * 1f, $"COMBO! +{puntosCombo}", Color.yellow);
        }

        // D. Cierre de Patrón Perfecto
        if (group.isClosed)
        {
            if (result.IsPatternValid && PatternValidator.CheckPattern(group.occupiedCells, patronActual))
            {
                p.conteoPatrones[group.targetSize]++;
                int bonoPatron = ScoreManager.Instance.GetPatternBonus(group.targetSize);
                p.score += bonoPatron;
                PopUpManager.Instance.MostrarPopUp(posMundo + Vector3.down * 1f, $"PERFECT! +{bonoPatron}", Color.cyan);

                // NUEVO: Disparamos la iluminación y futuros efectos de partículas
                gridManager.HighlightCompletedPattern(currentPlayerIndex, group.occupiedCells);
            }
        }

        // --- 4. ACTUALIZACIÓN VISUAL ---

        // Al final de ConfirmarYProcesarPuntos...
        UIManager.Instance.UpdateProgressText(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize);

        // NUEVO: Verificamos si toda la mesa ya terminó
        if (AreAllPlayersFinished())
        {
            // Iniciamos el Director de Secuencia Final
            StartCoroutine(EndGameSequence());
        }
        else
        {
            // Si el juego sigue en curso (alguien no ha terminado), actualizamos el HUD normal
            UIManager.Instance.ActualizarScore(p.score);

            // [AQUÍ VA TU CÓDIGO DE PASAR DE TURNO AL SIGUIENTE JUGADOR]
            // CambiarTurno();
        }

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
        // Evaluamos dinámicamente la cantidad de jugadores para definir el retraso
        float tiempoDeEspera = (numPlayers == 1) ? 0f : 1.5f;

        // Solo pausamos si el tiempo es mayor a 0 para no perder ni un frame innecesario
        if (tiempoDeEspera > 0f)
        {
            yield return new WaitForSeconds(tiempoDeEspera);
        }

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

        // ¡El tablero ya está listo! Desbloqueamos la UI para que saque su dado.
        UIManager.Instance.SetDrawInputLock(false);

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

        // Destruye todas las animaciones activas en la escena actual 
        // antes de cargar la siguiente, evitando fugas de memoria.
        DOTween.KillAll();

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

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
        SceneManager.LoadScene(1);
    }

    /// <summary>
    /// Checks if all active players have reached the maximum number of placed dice.
    /// </summary>
    private bool AreAllPlayersFinished()
    {
        foreach (PlayerData p in players)
        {
            if (p.placedDice < maxDicePerPlayer) return false;
        }
        return true;
    }



    /// <summary>
    /// Master Coroutine that orchestrates the end-game feedback loop.
    /// Animates board 1, then board 2, etc., before showing the final results.
    /// </summary>
    private System.Collections.IEnumerator EndGameSequence()
    {
        // 1. Recorremos a cada jugador uno por uno
        for (int i = 0; i < players.Count; i++)
        {
            PlayerData p = players[i];

            // Calculamos la penalización matemáticamente y aplicamos al score de inmediato
            int gapPenalties = gridManager.CalculateGapPenalty(i);
            p.score += gapPenalties;

            // 2. Ejecutamos la animación de ESTE jugador y ESPERAMOS a que termine
            // Pasamos 'null' al callback porque el 'yield return' ya se encarga de la pausa
            yield return StartCoroutine(gridManager.AnimateGapPenaltiesFlow(i, null));

            // Actualizamos el HUD para que el jugador vea el golpe a sus puntos
            UIManager.Instance.ActualizarScore(p.score);

            // Pequeña pausa dramática antes de pasar al tablero del siguiente jugador
            yield return new WaitForSeconds(1.0f);
        }

        // Supongamos que el jugador 0 es el jugador principal (el dueño del teléfono)
        PlayerData jugadorLocal = players[0];

        // Ejemplo de lógica: Gana 1 moneda por cada 10 puntos que hizo
        int monedasGanadas = Mathf.Max(0, jugadorLocal.score / 10);
        int nuevoNivel = 1; // Aquí pondrías tu variable real de nivel de campaña

        // Llamamos al guardado en la nube. 
        // Usamos "_ =" para decirle a Unity: "Guarda esto en segundo plano, no congeles el juego esperando a que termine".
        _ = CloudSaveManager.Instance.GuardarProgresoMeta(nuevoNivel, monedasGanadas);

        // -----------------------------------------------------------

        // 3. Finalmente, le decimos al UIManager que dibuje la pantalla final
        UIManager.Instance.MostrarResultadosFinales(0);
    }





}