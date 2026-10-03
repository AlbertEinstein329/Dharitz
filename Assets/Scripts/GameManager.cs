using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MyGame.Core; // Acceso a la Capa 0

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

    // Capa 1: Wrappers visuales
    public List<PlayerData> players = new List<PlayerData>();
    [HideInInspector] public bool isRestarting = false;

    [Header("UI Elements")]
    public Button drawButton;
    public Button reDrawButton;
    public TMPro.TextMeshProUGUI reDrawText;
    public bool isGameOver = false;

    // Sub-Systems
    public DiceManager diceManager { get; private set; }
    public TurnManager turnManager { get; private set; }
    public PlacementOrchestrator placementOrchestrator { get; private set; }

    // ==========================================
    // ANCLA DE LA CAPA 0 (ESTADO AUTORITATIVO)
    // ==========================================
    public MatchStateDTO ServerState { get; private set; }

    // En partida online el servidor (NetworkGameManager) es la autoridad y esta escena solo pinta snapshots
    public bool IsOnlineMatch => currentSession != null && currentSession.isOnlineMatch;

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

        // Semilla determinista: en partida online, el servidor la provee.
        // En modo local, usamos DateTime para variabilidad natural.
        int matchSeed = (int)System.DateTime.Now.Ticks;
        Random.InitState(matchSeed);

        diceManager = new DiceManager(this);
        turnManager = new TurnManager(this);
        placementOrchestrator = new PlacementOrchestrator(this);

        // 1. INICIALIZACIÓN ESTRICTA DE LA MÁQUINA DE ESTADOS (Capa 0)
        ServerState = new MyGame.Core.MatchStateDTO();
        ServerState.CurrentPhase = MyGame.Core.MatchPhase.WaitingForPlayers;

        // F0.5 / F1.8: RNG determinista único — solo el GameManager lo posee
        ServerState.InitializeRNG(matchSeed);

        if (currentSession != null)
        {
            currentVariant = currentSession.selectedVariant;
            numPlayers = currentSession.playerCount;

            // F0.4: Inyectar configuración de variante y puntaje en el estado maestro
            if (currentVariant != null)
                ServerState.VariantConfig = currentVariant.ToDTO();
            // ScoringConfig ya se inicializa con Default() en el constructor

            players = new List<PlayerData>();

            // Online: un hueco por jugador esperado; nombres y estado reales llegan en el primer snapshot
            int playerSlots = IsOnlineMatch ? numPlayers : currentSession.players.Count;
            for (int i = 0; i < playerSlots; i++)
            {
                PlayerSetup setup = IsOnlineMatch
                    ? new PlayerSetup { playerName = $"Jugador {i + 1}" }
                    : currentSession.players[i];
                PlayerData newPlayer = new PlayerData(i, setup.playerName, setup.isBot, setup.botDifficulty);
                newPlayer.avatarId = setup.avatarId;

                players.Add(newPlayer);

                // Mapeo inicial hacia el Servidor
                MyGame.Core.PlayerDataDTO profileDTO = newPlayer.ToDTO();
                if (SaveManager.Instance != null && SaveManager.Instance.CurrentProfile != null)
                {
                    profileDTO.TotalCoins = SaveManager.Instance.CurrentProfile.totalCoins;
                }
                ServerState.PlayerProfiles[i] = profileDTO;

                // F1.6: Inicializar tablero del jugador en el estado maestro
                ServerState.PlayerBoards[i] = new MyGame.Core.BoardStateDTO(gridManager != null ? gridManager.rows : 8,
                                                                              gridManager != null ? gridManager.cols : 10);
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
        if (IsOnlineMatch)
        {
            // No se reparte nada en local: se espera al snapshot inicial del servidor.
            // Bolsa vacía (no nula) para que contadores y DiceManager.diceBag no fallen antes del primer snapshot.
            ServerState.DiceBag = new List<MyGame.Core.DieColor>();
            UIManager.Instance.SetDrawInputLock(true);
            if (drawButton != null) drawButton.interactable = false;
            if (reDrawButton != null) reDrawButton.interactable = false;
            Debug.Log($"[GameManager] Partida online: esperando a {numPlayers} jugadores...");
            return;
        }

        // 2. ARRANQUE LÓGICO (Fase de resolución cruzada)
        ServerState.CurrentPhase = MyGame.Core.MatchPhase.PlayerTurn;
        ServerState.HasDrawn = false;
        diceManager.InitializeBag();

        // F1.1: La bolsa ya se inicializa dentro de DiceManager usando ServerState.DiceBag directamente.
        // El DiceBag del ServerState se llena en DiceManager.InitializeBag().

        turnManager.StartTurn();
    }

    void InitializeFallbackPlayers()
    {
        players.Clear();
        for (int i = 0; i < numPlayers; i++)
        {
            PlayerData fallbackPlayer = new PlayerData(i, $"Jugador Fallback {i + 1}", false, 0);
            players.Add(fallbackPlayer);
            ServerState.PlayerProfiles[i] = fallbackPlayer.ToDTO();
        }
    }

    // Reemplaza el estado por el autoritativo (servidor) o por el espejo recibido (cliente)
    public void AdoptServerState(MatchStateDTO state)
    {
        ServerState = state;
    }

    public void DrawDie()
    {
        if (IsOnlineMatch)
        {
            if (MyGame.Networking.NetworkGameManager.Instance != null)
                MyGame.Networking.NetworkGameManager.Instance.RequestDrawDieServerRpc();
            return;
        }

        diceManager.DrawDie();
    }

    public void UseReDraw()
    {
        if (IsOnlineMatch)
        {
            if (MyGame.Networking.NetworkGameManager.Instance != null)
                MyGame.Networking.NetworkGameManager.Instance.RequestReDrawServerRpc();
            return;
        }

        diceManager.UseReDraw();
    }

    public void EnableDrawButton()
    {
        if (drawButton != null) drawButton.interactable = true;
    }

    public void BeginPlacement(int row, int col)
    {
        // Online, la jugada ya pasó la validación visual del cliente; el servidor decide y responde con un snapshot
        if (IsOnlineMatch)
        {
            var network = MyGame.Networking.NetworkGameManager.Instance;
            if (network != null && network.LocalPlayerIndex == turnManager.CurrentPlayerIndex)
                network.RequestPlaceDieServerRpc(row, col);
            return;
        }

        placementOrchestrator.BeginPlacement(row, col);
    }

    public bool AreAllPlayersFinished()
    {
        // El cliente visual ya no decide. Solo leemos el perfil local clonado del servidor.
        foreach (PlayerData p in players)
        {
            if (p.placedDice < maxDicePerPlayer) return false;
        }
        return true;
    }

    public void EndMatch()
    {
        isGameOver = true;

        if (drawButton != null)
        {
            drawButton.interactable = false;
            TMPro.TextMeshProUGUI drawButtonText = drawButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (drawButtonText != null)
            {
                drawButtonText.text = "MATCH END";
            }
        }

        if (isRestarting || isGameOver) return;

        Debug.Log("Fin de la bolsa. Calculando resultados...");

        if (reDrawButton != null) reDrawButton.interactable = false;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Victory");

        int viewedPlayer = gridManager.CurrentlyViewedPlayer;
        UIManager.Instance.ShowFinalResults(viewedPlayer);
    }

    public IEnumerator EndGameSequence()
    {
        isGameOver = true;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerData p = players[i];
            // La multa fue calculada matemáticamente en CoreSessionProcessor
            yield return StartCoroutine(gridManager.AnimateGapPenaltiesFlow(i, null));
            UIManager.Instance.UpdateScore(p.score);
            yield return new WaitForSeconds(1.0f);
        }

        PlayerData jugadorLocal = players[0];

        int monedasGanadas = Mathf.Max(0, jugadorLocal.score / 10);
        int nivelActual = currentSession != null ? 1 : 1;
        int estrellasObtenidas = jugadorLocal.score >= 1000 ? 1 : 0;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.AddCoins(monedasGanadas);
            SaveManager.Instance.UpdateLevelProgress(nivelActual, jugadorLocal.score, estrellasObtenidas);
        }

        if (CloudSaveManager.Instance != null)
        {
            _ = CloudSaveManager.Instance.SaveMetaProgress();
        }

        int viewedPlayer = gridManager.CurrentlyViewedPlayer;
        UIManager.Instance.ShowFinalResults(viewedPlayer);
    }
}