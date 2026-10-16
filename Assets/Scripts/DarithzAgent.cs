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
                // Cambio 3: Descarte usando reDraw reducido a -0.02f
                AddReward(-0.02f);
                RequestDecision();
                return;
            }
            else
            {
                // Cambio 4: Pasar turno (sin jugada posible) reducido a -0.10f
                AddReward(-0.10f);
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
            // Cambio 5: Movimiento inválido (eliminación) reducido a -1.0f
            AddReward(-1.0f);
            if (gm.players.Count == 1) gm.isGameOver = true;
            EndEpisode();
            return;
        }

        int previousDiceCount = agentData.placedDice;

        // =======================================================
        // 1. FOTO ANTES DE LA JUGADA
        // =======================================================
        int structureScoreBefore = CalculateCurrentStructureScore();
        float patternScoreBefore = CalculateTotalPatternScore();

        // El juego procesa físicamente la colocación del dado
        gm.placementOrchestrator.ConfirmAndProcessScore(targetRow, targetCol);

        // =======================================================
        // 2. FOTO DESPUÉS DE LA JUGADA
        // =======================================================
        int structureScoreAfter = CalculateCurrentStructureScore();
        float patternScoreAfter = CalculateTotalPatternScore();

        // Diferencias matemáticas
        int pointsGained = structureScoreAfter - structureScoreBefore;
        float patternRewardGained = patternScoreAfter - patternScoreBefore;

        gm.turnManager.EndTurn();
        CheckGameOver();

        if (agentData.placedDice > previousDiceCount)
        {
            // Cambio 1: Muy pequeña recompensa base por poner el dado reducida a +0.01f
            float premioBase = 0.01f;

            // Cambio 2: Recompensas normalizadas
            float premioEstructura = pointsGained / 12800f;

            // (patternRewardGained ya viene normalizado por /12800f desde CalculateTotalPatternScore)
            AddReward(premioBase + premioEstructura + patternRewardGained);
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

            // 🔥 APLICAMOS EL CASTIGO POR HUECOS TOPOLÓGICOS (Ajustado)
            float gapPenalty = CalculateGapPenaltyForIA();
            AddReward(gapPenalty);

            // Cambio 7 y 8: Recompensa terminal (+1.5f victoria, -1.0f eliminación)
            AddReward(agentData.isEliminated ? -1.0f : 1.5f);
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

                    // Cambio 6: Penalización por gaps reducida para estabilidad
                    if (gapSize == 1) totalPenaltyPoints += 40;
                    else if (gapSize == 2) totalPenaltyPoints += 60;
                    else if (gapSize >= 3) totalPenaltyPoints += (gapSize * 80);
                }
            }
        }

        // Cambio 6: Retornar como negativo dividido por 1000f
        return -(totalPenaltyPoints / 1000f);
    }

    // 3. Calcula el puntaje total de los patrones
    private float CalculateTotalPatternScore()
    {
        var logic = gridManager.allBoardsLogic[targetPlayerIndex];
        int rows = gridManager.rows;
        int cols = gridManager.cols;
        bool[,] visited = new bool[rows, cols];

        float totalPatternScore = 0f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var die = logic[r, c];
                // Evaluamos dados que no hayamos visitado aún
                if (die != null && !visited[r, c])
                {
                    int patternSize = 0;
                    DieColor color = die.color;
                    int groupId = die.groupId; // Usamos el ID para asegurar que es el mismo patrón

                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(r, c));
                    visited[r, c] = true;

                    // BFS para medir el tamaño de este patrón
                    while (queue.Count > 0)
                    {
                        Vector2Int curr = queue.Dequeue();
                        patternSize++;

                        int[] dr = { -1, 1, 0, 0 };
                        int[] dc = { 0, 0, -1, 1 };

                        for (int i = 0; i < 4; i++)
                        {
                            int nr = curr.x + dr[i];
                            int nc = curr.y + dc[i];

                            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                            {
                                var neighbor = logic[nr, nc];
                                if (neighbor != null && !visited[nr, nc] && neighbor.color == color && neighbor.groupId == groupId)
                                {
                                    visited[nr, nc] = true;
                                    queue.Enqueue(new Vector2Int(nr, nc));
                                }
                            }
                        }
                    }

                    // Cambio 2: Fórmula de patrón escalada a puntos reales
                    float scoreForThisPattern = patternSize switch
                    {
                        1 => 100f,
                        2 => 100f,
                        3 => 200f,
                        4 => 300f,
                        5 => 400f,
                        6 => 500f,
                        _ => 500f
                    };

                    totalPatternScore += scoreForThisPattern;
                }
            }
        }

        // Cambio 2: Normalizamos el total entre 12800f
        return totalPatternScore / 12800f;
    }

    // Nuevo método para construir el tag compuesto
    public static string GetTagForDie(DieColor color, int value)
    {
        return $"Die{color}{value}";
    }

    // Actualización del método de inyección
    public void InjectSensorData(DieColor assignedColor, int dieValue)
    {
        gameObject.layer = LayerMask.NameToLayer("MLAgents");

        string exactTag = GetTagForDie(assignedColor, dieValue);
        gameObject.tag = exactTag;

        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.size = new Vector3(0.9f, 0.9f, 0.9f);
        }
    }
}