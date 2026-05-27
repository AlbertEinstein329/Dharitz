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
    [HideInInspector] public VariantData currentVariant;
    [HideInInspector] public int numPlayers;
    public int maxDicePerPlayer = 52;
    public List<PlayerData> players = new List<PlayerData>();

    [Header("UI Elements")]
    public Button drawButton;
    public Button reDrawButton;
    public TMPro.TextMeshProUGUI reDrawText;
    public bool isGameOver = false;

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
            for (int i = 0; i < numPlayers; i++)
            {
                PlayerSetup setup = currentSession.players[i];
                players.Add(new PlayerData(i, setup.playerName, setup.isBot, setup.avatarId));
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
        Debug.Log("Fin de la bolsa. Calculando resultados...");
        isGameOver = true;
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
}