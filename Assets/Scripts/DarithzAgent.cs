using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DarithzAgent : Agent
{
    private GameManager gm;
    private GridManager gridManager;

    [Header("Agent Settings")]
    [SerializeField] private int targetPlayerIndex = 0;

    // Meta base de esta generación
    private const int MaxTargetDice = 52;

    private const int BoardCols = 10;
    private const int BoardRows = 8;
    private const int TotalCells = BoardCols * BoardRows;
    private const int PassTurnAction = 80;

    public override void Initialize()
    {
        gm = GameManager.Instance ?? FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("CRÍTICO: GameManager no encontrado. Destruyendo IA.");
            Destroy(gameObject);
            return;
        }
        gridManager = gm.gridManager;

        if (gm.players != null && targetPlayerIndex < gm.players.Count)
        {
            gm.players[targetPlayerIndex].agentComponent = this;
            gm.players[targetPlayerIndex].isBot = true;
        }
    }

    public override void OnEpisodeBegin()
    {
        gm.ResetGame();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(gm.turnManager.HasDrawn ? (float)((int)gm.turnManager.CurrentDrawnColor + 1) : 0f);
        sensor.AddObservation((float)gm.turnManager.CurrentDrawnValue);
        sensor.AddObservation(gm.turnManager.HasDrawn ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (gm.isGameOver || gm.players[targetPlayerIndex].isEliminated)
        {
            EndEpisode();
            return;
        }

        if (gm.turnManager.CurrentPlayerIndex != targetPlayerIndex) return;

        if (!gm.turnManager.HasDrawn)
        {
            gm.diceManager.DrawDie();
            RequestDecision();
            return;
        }

        int actionIndex = actions.DiscreteActions[0];
        PlayerData agentData = gm.players[targetPlayerIndex];

        // LÓGICA DE PASAR TURNO
        if (actionIndex == PassTurnAction)
        {
            if (agentData.reDraws > 0)
            {
                agentData.reDraws--;
                gm.diceManager.UseReDraw();
                AddReward(-0.05f); // Micro-castigo por descartar
                RequestDecision();
                return;
            }
            else
            {
                // 🔥 EL SUICIDIO TÁCTICO: Un castigo devastador (-5.0 = -5000 puntos virtuales)
                // Le enseña que rendirse es la PEOR opción posible en el universo.
                AddReward(-5.0f);
                gm.turnManager.EndTurn();
                CheckGameOver();
                return;
            }
        }

        int targetRow = actionIndex / BoardCols;
        int targetCol = actionIndex % BoardCols;

        DieColor currentColor = gm.turnManager.CurrentDrawnColor;
        int targetSize = 0;
        int currentGroupId = 0;

        if (agentData.activeGroups.TryGetValue(currentColor, out GroupData group) && group != null)
        {
            targetSize = group.targetSize;
            currentGroupId = group.id;
        }

        bool isValid = gridManager.IsValidPlacement(targetPlayerIndex, targetRow, targetCol, currentColor, currentGroupId, targetSize);

        if (!isValid)
        {
            agentData.isEliminated = true;
            AddReward(-2.0f);
            if (gm.players.Count == 1) gm.isGameOver = true;
            EndEpisode();
            return;
        }

        int previousDiceCount = agentData.placedDice;

        // 1. Tomamos "foto" a la estructura antes de poner el dado
        int structureScoreBefore = CalculateCurrentStructureScore();

        gm.placementOrchestrator.ConfirmAndProcessScore(targetRow, targetCol);

        // 2. Tomamos "foto" a la estructura después de poner el dado
        int structureScoreAfter = CalculateCurrentStructureScore();
        int pointsGained = structureScoreAfter - structureScoreBefore;

        gm.turnManager.EndTurn();
        CheckGameOver();

        if (agentData.placedDice > previousDiceCount)
        {
            // Avaricia base
            float progress = Mathf.Clamp01((float)agentData.placedDice / MaxTargetDice);
            float premioBase = Mathf.Pow(progress, 2f) * 1.0f;

            // Premio de estructura alineado (Si cerró una fila de 120 pts, gana +0.12f extra)
            float premioEstructura = pointsGained / 1000f;

            AddReward(premioBase + premioEstructura);
        }
    }

    private void CheckGameOver()
    {
        if (gm.diceManager.GetTotalDiceLeft() == 0 || gm.AreAllPlayersFinished())
        {
            gm.isGameOver = true;
        }

        if (gm.isGameOver)
        {
            PlayerData agentData = gm.players[targetPlayerIndex];

            // 🔥 APLICAMOS EL CASTIGO POR HUECOS TOPOLÓGICOS
            float gapPenalty = CalculateGapPenaltyForIA();
            AddReward(gapPenalty);
            // Si el tablero quedó lleno de huecos de 3 celdas, recibirá un castigo masivo (ej: -6.0f)

            AddReward(agentData.isEliminated ? -2.0f : 1.0f);
            EndEpisode();
        }
    }

    // =========================================================
    // MÁSCARA DE ACCIONES: API CORRECTA Y LÓGICA REPARADA
    // =========================================================
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (gm.turnManager.CurrentPlayerIndex != targetPlayerIndex || !gm.turnManager.HasDrawn)
        {
            for (int i = 0; i < TotalCells; i++) actionMask.SetActionEnabled(0, i, false);
            actionMask.SetActionEnabled(0, PassTurnAction, true);
            return;
        }

        PlayerData p = gm.players[targetPlayerIndex];
        DieColor currentColor = gm.turnManager.CurrentDrawnColor;

        int targetSize = 0;
        int currentGroupId = 0;

        if (p.activeGroups.TryGetValue(currentColor, out GroupData group) && group != null)
        {
            targetSize = group.targetSize;
            currentGroupId = group.id;
        }

        bool hasValidMove = false;

        // ELIMINAMOS EL BUG: Siempre evaluamos el tablero, no importa si targetSize es 0
        // Dejamos que GridManager decida si se puede empezar un grupo nuevo.
        for (int i = 0; i < TotalCells; i++)
        {
            int row = i / BoardCols;
            int col = i % BoardCols;

            bool isValid = gridManager.IsValidPlacement(targetPlayerIndex, row, col, currentColor, currentGroupId, targetSize);
            actionMask.SetActionEnabled(0, i, isValid);

            if (isValid)
            {
                hasValidMove = true;
            }
        }

        // TÁCTICA AGRESIVA: Bloqueamos "Pasar Turno" obligatoriamente si hay un movimiento válido.
        actionMask.SetActionEnabled(0, PassTurnAction, !hasValidMove);
    }

    // =======================================================
    // 🧠 MÓDULO DE VISIÓN INTERNA DE LA IA (Alineación de Recompensas)
    // =======================================================

    // 1. Calcula el puntaje de estructura (Filas y Columnas) del tablero actual
    private int CalculateCurrentStructureScore()
    {
        var logic = gridManager.allBoardsLogic[targetPlayerIndex];
        int rows = gridManager.rows;
        int cols = gridManager.cols;
        int currentScore = 0;

        // Evaluar Filas Completas
        for (int r = 0; r < rows; r++)
        {
            bool isFull = true;
            for (int c = 0; c < cols; c++) { if (logic[r, c] == null) { isFull = false; break; } }
            if (isFull) currentScore += ScoreManager.ROW_COMPLETE_BONUS; // 120
        }

        // Evaluar Columnas Completas
        for (int c = 0; c < cols; c++)
        {
            bool isFull = true;
            for (int r = 0; r < rows; r++) { if (logic[r, c] == null) { isFull = false; break; } }
            if (isFull) currentScore += ScoreManager.COL_COMPLETE_BONUS; // 170
        }

        return currentScore;
    }

    // 2. Escanea el tablero con un algoritmo BFS para encontrar huecos y penalizarlos
    private float CalculateGapPenaltyForIA()
    {
        var logic = gridManager.allBoardsLogic[targetPlayerIndex];
        int rows = gridManager.rows;
        int cols = gridManager.cols;
        bool[,] visited = new bool[rows, cols];

        int totalPenaltyPoints = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // Si la celda está vacía y no la hemos visitado, encontramos un nuevo hueco
                if (logic[r, c] == null && !visited[r, c])
                {
                    int gapSize = 0;
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(r, c));
                    visited[r, c] = true;

                    // BFS para medir el tamaño exacto del hueco conectado
                    while (queue.Count > 0)
                    {
                        Vector2Int curr = queue.Dequeue();
                        gapSize++;

                        int[] dr = { -1, 1, 0, 0 };
                        int[] dc = { 0, 0, -1, 1 };

                        for (int i = 0; i < 4; i++)
                        {
                            int nr = curr.x + dr[i];
                            int nc = curr.y + dc[i];

                            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                            {
                                if (logic[nr, nc] == null && !visited[nr, nc])
                                {
                                    visited[nr, nc] = true;
                                    queue.Enqueue(new Vector2Int(nr, nc));
                                }
                            }
                        }
                    }

                    // Aplicamos TU regla de penalización exacta
                    if (gapSize == 1) totalPenaltyPoints += (gapSize * -500);
                    else if (gapSize == 2) totalPenaltyPoints += (gapSize * -700);
                    else if (gapSize >= 3) totalPenaltyPoints += (gapSize * -1000);
                }
            }
        }

        // Devolvemos la penalización escalada para la Red Neuronal (dividido entre 1000)
        return totalPenaltyPoints / 1000f;
    }
}