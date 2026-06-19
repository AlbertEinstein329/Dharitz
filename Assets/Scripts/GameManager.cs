using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour, ITurnProvider, IPlacementExecutor
{
    public static GameManager Instance;

    [Header("Referencias Externas")]
    public GridManager gridManager;

    [Header("Configuracion de Partida")]
    public SessionConfig currentSession;
    public VariantData currentVariant;
    [HideInInspector] public int numPlayers;
    public int maxDicePerPlayer = 52;
    public List<PlayerData> players = new List<PlayerData>();
    [HideInInspector] public bool isRestarting = false;

    [Header("UI Elements")]
    public Button drawButton;
    public Button reDrawButton;
    public TMPro.TextMeshProUGUI reDrawText;
    public bool isGameOver = false;

    [Header("ML-Agents Integration")]
    public bool isTrainingMode = false;
    public MLAgentsMetricsController metricsController;

    // Sub-Systems
    public DiceManager diceManager { get; private set; }
    public TurnManager turnManager { get; private set; }
    public PlacementOrchestrator placementOrchestrator { get; private set; }

    // --- ITurnProvider Implementation ---
    public int CurrentPlayerIndex => turnManager != null ? turnManager.CurrentPlayerIndex : 0;
    public bool HasDrawn => turnManager != null ? turnManager.HasDrawn : false;
    public DieColor CurrentDrawnColor => turnManager != null ? turnManager.CurrentDrawnColor : DieColor.Red;
    public PlayerData GetCurrentPlayer() => turnManager != null ? turnManager.GetCurrentPlayer() : null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Random.InitState((int)System.DateTime.Now.Ticks);

        diceManager = new DiceManager(this);
        turnManager = new TurnManager(this);
        placementOrchestrator = new PlacementOrchestrator(this);

        if (currentSession != null)
        {
            currentVariant = currentSession.selectedVariant;
            numPlayers = currentSession.playerCount;

            players = new List<PlayerData>();
            for (int i = 0; i < currentSession.players.Count; i++)
            {
                PlayerSetup setup = currentSession.players[i];
                PlayerData newPlayer = new PlayerData(i, setup.playerName, setup.isBot, setup.botDifficulty);

                
                newPlayer.avatarId = setup.avatarId;

                players.Add(newPlayer);
            }
        }
        else
        {
            Debug.LogError("Falta el SessionConfig. Cargando configuraciones por defecto a prueba de fallos.");
            numPlayers = 2; 
            InitializeFallbackPlayers(); 
        }
    }

    void Start()
    {
        UnityEngine.Time.timeScale = 1f;
        diceManager.InitializeBag();
        turnManager.StartTurn();
    }

    void InitializeFallbackPlayers()
    {
        players.Clear();
        for (int i = 0; i < numPlayers; i++)
        {
            players.Add(new PlayerData(i, $"Jugador Fallback {i + 1}", false, 0));
        }
    }


    // --- Facade Methods to handle UI Button Clicks ---
    public void DrawDie()
    {
        // 1. Execute the logical draw
        diceManager.DrawDie();

    }

    public void UseReDraw()
    {
        diceManager.UseReDraw();

    }

    public void EnableDrawButton()
    {
        if (drawButton != null) drawButton.interactable = true;
    }

    // --- IPlacementExecutor Implementation ---
    public void BeginPlacement(int row, int col)
    {
        placementOrchestrator.BeginPlacement(row, col);
    }

    // --- End Match Logic ---
    public bool AreAllPlayersFinished()
    {
        foreach (PlayerData p in players)
        {
            if (p.placedDice < maxDicePerPlayer) return false;
        }
        return true;
    }

    public void EndMatch()
    {
        if (isRestarting || isGameOver) return;
        isGameOver = true;

        // Inyección nativa hacia TensorBoard
        if (isTrainingMode && metricsController != null)
        {
            int remainingDices = diceManager.diceBag.Count;
            // Extrae el score del Player 1 (El Agente) de forma autoritaria.
            int finalScore = players.Count > 0 ? players[0].score : 0;

            // Deducción del fin de partida
            GameEndReason reason = remainingDices == 0 ? GameEndReason.BagDepleted : GameEndReason.BoardLocked;
            metricsController.RecordEpisodeTerminationMetrics(remainingDices, finalScore, reason);
        }

        if (drawButton != null) drawButton.interactable = false;
        if (reDrawButton != null) reDrawButton.interactable = false;

        // =========================================================
        // APAGADO FORZOSO DEL BOTÓN AL TERMINAR LA PARTIDA
        // =========================================================
        if (drawButton != null)
        {
            drawButton.interactable = false; // Lo bloqueamos para que no puedan hacer clic
            TMPro.TextMeshProUGUI drawButtonText = drawButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (drawButtonText != null)
            {
                drawButtonText.text = "MATCH END"; // Cambiamos el texto
            }
        }



        Debug.Log("Fin de la bolsa. Calculando resultados...");

        if (drawButton != null)
        {
            drawButton.interactable = false;
            TMPro.TextMeshProUGUI drawButtonText = drawButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (drawButtonText != null) drawButtonText.text = "<color=#FFA500>MATCH END</color>";
        }

        if (reDrawButton != null) reDrawButton.interactable = false;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Victory");

        int viewedPlayer = gridManager.CurrentlyViewedPlayer;
        UIManager.Instance.ShowFinalResults(viewedPlayer);

    }

    public IEnumerator EndGameSequence()
    {
        for (int i = 0; i < players.Count; i++)
        {
            PlayerData p = players[i];
            int gapPenalties = gridManager.CalculateGapPenalty(i);
            p.score += gapPenalties;

            yield return StartCoroutine(gridManager.AnimateGapPenaltiesFlow(i, null));
            UIManager.Instance.UpdateScore(p.score);

            yield return new WaitForSeconds(1.0f);
        }

        PlayerData jugadorLocal = players[0];
        int monedasGanadas = Mathf.Max(0, jugadorLocal.score / 10);
        
        int nivelActual = 1; // Aquí se usaría un valor real de SessionConfig en el futuro
        int estrellasObtenidas = jugadorLocal.score >= 1000 ? 1 : 0; // Lógica provisional de estrellas
        
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.AddCoins(monedasGanadas);
            SaveManager.Instance.UpdateLevelProgress(nivelActual, jugadorLocal.score, estrellasObtenidas);
        }

        if (CloudSaveManager.Instance != null)
        {
            _ = CloudSaveManager.Instance.SaveMetaProgress();
        }

        UIManager.Instance.ShowFinalResults(0);
    }

    public void ResetGame()
    {
        isGameOver = false;

        // 1. Resetear datos lógicos de cada jugador
        foreach (var player in players)
        {
            player.score = 0;
            player.placedDice = 0;
            player.isEliminated = false;
            player.reDraws = PlayerData.DEFAULT_REDRAWS;
            player.activeGroups.Clear();
            System.Array.Clear(player.patternCounts, 0, player.patternCounts.Length);

            // Deshabilitar por completo el uso de Undo para evitar exploits de la IA
            player.currentUndoUses = 0;
        }

        // 2. Limpiar historiales del CommandManager sin destruir instancias
        if (CommandManager.Instance != null)
        {
            for (int i = 0; i < numPlayers; i++)
            {
                CommandManager.Instance.ClearHistory(i);
            }
        }

        // 3. Reiniciar la bolsa de dados a su estado inicial
        diceManager.InitializeBag();

        // 4. Limpiar matrices del tablero
        gridManager.ResetGrid();

        if (turnManager != null)
        {
            turnManager.ResetTurnState(); // Delegación correcta de autoridad
        }

        // 5. Forzar el arranque del ciclo lógico del primer turno
        turnManager.StartTurn();
    }
}