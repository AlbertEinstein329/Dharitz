using System.Collections.Generic;
using UnityEngine;

public class GridInteractionManager : MonoBehaviour
{
    public static GridInteractionManager Instance { get; private set; }

    public enum InteractionState
    {
        Normal,
        MoveMode_WaitingForDie,
        MoveMode_WaitingForDestination
    }

    [Header("Dependencias de Arquitectura")]
    [SerializeField] private BoardLogic boardLogic;
    [SerializeField] private CommandManager commandManager;
    [SerializeField] private MoveActionButton moveActionButton;

    [Header("Estado Interno (Solo lectura)")]
    [SerializeField] private InteractionState currentState = InteractionState.Normal;
    private Vector2Int currentSelectedDieCoordinate = new Vector2Int(-1, -1);
    public bool IsMoveModeActive => currentState != InteractionState.Normal;

    [Header("Gestión de Comodines Multijugador")]
    [SerializeField] private int maxMoveUses = 1;

    // SOLUCIÓN AL BOTÓN COMPARTIDO: Cada jugador tiene su propio contador indexado
    private Dictionary<int, int> playerMoveUses = new Dictionary<int, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// SINCRO DE TURNO CRÍTICA: Debes llamar a este método desde tu GameManager o TurnManager
    /// en el código exacto donde cambies el turno de los jugadores.
    /// </summary>
    public void OnTurnChanged(int newPlayerIndex)
    {
        if (!playerMoveUses.ContainsKey(newPlayerIndex))
        {
            playerMoveUses[newPlayerIndex] = 0;
        }

        // Evaluamos si el nuevo jugador tiene usos disponibles y actualizamos el botón
        bool hasUsesLeft = playerMoveUses[newPlayerIndex] < maxMoveUses;
        if (moveActionButton != null)
        {
            moveActionButton.SetInteractable(hasUsesLeft);
        }

        // Forzamos la salida del modo mover por si el jugador anterior dejó el toggle encendido
        ResetMoveMode();
    }

    public void ToggleMoveMode(bool isActive)
    {
        if (isActive)
        {
            int pIndex = GameManager.Instance.CurrentPlayerIndex;
            if (!playerMoveUses.ContainsKey(pIndex)) playerMoveUses[pIndex] = 0;

            if (playerMoveUses[pIndex] >= maxMoveUses)
            {
                Debug.LogWarning("[UX] Ya has agotado tus usos de Move para esta partida.");
                if (moveActionButton != null) moveActionButton.LockToggle();
                return;
            }

            currentState = InteractionState.MoveMode_WaitingForDie;
            ClearSelection();
            Debug.Log($"[UX] Modo Mover Activado para Jugador {pIndex}. Selecciona un dado.");
        }
        else
        {
            ResetMoveMode();
        }
    }

    public void OnGridCellClicked(Vector2Int clickedCoordinate)
    {
        switch (currentState)
        {
            case InteractionState.MoveMode_WaitingForDie:
                TrySelectDieForMove(clickedCoordinate);
                break;

            case InteractionState.MoveMode_WaitingForDestination:
                if (boardLogic.occupiedCells.Contains(clickedCoordinate))
                {
                    TrySelectDieForMove(clickedCoordinate);
                }
                else
                {
                    TryExecuteMove(clickedCoordinate);
                }
                break;
        }
    }

    private void TrySelectDieForMove(Vector2Int coordinate)
    {
        if (boardLogic != null && boardLogic.occupiedCells.Contains(coordinate))
        {
            ClearBoardHighlights();
            currentSelectedDieCoordinate = coordinate;
            currentState = InteractionState.MoveMode_WaitingForDestination;
            HighlightValidMoveCells(coordinate);
        }
        else
        {
            Debug.LogWarning("[UX] Clic inválido. Selecciona un DADO.");
        }
    }

    private GridManager.DieData[,] CloneBoardLogic(GridManager.DieData[,] original, int rows, int cols)
    {
        var clone = new GridManager.DieData[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++) clone[r, c] = original[r, c];
        }
        return clone;
    }

    private bool IsValidMoveDestination(Vector2Int targetPos)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.gridManager == null) return false;

        int playerIndex = gm.CurrentPlayerIndex;
        var dieToMove = gm.gridManager.allBoardsLogic[playerIndex][currentSelectedDieCoordinate.y, currentSelectedDieCoordinate.x];
        if (dieToMove == null) return false;

        PlayerData player = gm.GetCurrentPlayer();
        int countInGroup = 0;

        foreach (var die in gm.gridManager.allBoardsLogic[playerIndex])
        {
            if (die != null && die.groupId == dieToMove.groupId) countInGroup++;
        }

        bool isSoloDie = (countInGroup <= 1);

        var simulatedLogic = CloneBoardLogic(gm.gridManager.allBoardsLogic[playerIndex], 10, 8);
        simulatedLogic[currentSelectedDieCoordinate.y, currentSelectedDieCoordinate.x] = null;

        GroupData groupToSimulate = null;
        if (player.activeGroups.TryGetValue(dieToMove.color, out groupToSimulate) && groupToSimulate != null)
        {
            groupToSimulate.occupiedCells.Remove(new Vector2Int(currentSelectedDieCoordinate.y, currentSelectedDieCoordinate.x));
            groupToSimulate.occupiedCells.Add(new Vector2Int(targetPos.y, targetPos.x));
        }

        bool isLegal = false;

        if (boardLogic.occupiedCells.Count <= 1)
        {
            isLegal = true;
        }
        else if (isSoloDie)
        {
            bool touchesAnyDie = false;
            bool colorClash = false;

            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };

            foreach (var dir in directions)
            {
                Vector2Int neighbor = targetPos + dir;
                if (neighbor.x >= 0 && neighbor.x < 8 && neighbor.y >= 0 && neighbor.y < 10)
                {
                    var neighborDie = simulatedLogic[neighbor.y, neighbor.x];
                    if (neighborDie != null)
                    {
                        touchesAnyDie = true;
                        if (dir.x == 0 || dir.y == 0)
                        {
                            if (neighborDie.color == dieToMove.color && neighborDie.groupId != dieToMove.groupId)
                            {
                                colorClash = true;
                            }
                        }
                    }
                }
            }
            isLegal = touchesAnyDie && !colorClash;
        }
        else
        {
            isLegal = PlacementValidator.IsValidPlacement(
                simulatedLogic, 10, 8, targetPos.y, targetPos.x,
                dieToMove.color, dieToMove.groupId, dieToMove.value,
                player, gm.currentVariant
            );
        }

        if (groupToSimulate != null)
        {
            groupToSimulate.occupiedCells.Remove(new Vector2Int(targetPos.y, targetPos.x));
            groupToSimulate.occupiedCells.Add(new Vector2Int(currentSelectedDieCoordinate.y, currentSelectedDieCoordinate.x));
        }

        return isLegal;
    }

    private void TryExecuteMove(Vector2Int targetCoordinate)
    {
        if (!HasValidSelection())
        {
            ResetMoveMode();
            return;
        }

        var logicMatrix = GameManager.Instance.gridManager.allBoardsLogic[GameManager.Instance.CurrentPlayerIndex];
        bool isTargetEmpty = logicMatrix[targetCoordinate.y, targetCoordinate.x] == null;

        bool maintainsCohesion = !boardLogic.WouldLeaveSplitIslands(currentSelectedDieCoordinate, targetCoordinate);
        bool isValidDestination = IsValidMoveDestination(targetCoordinate);

        if (boardLogic != null && isTargetEmpty && maintainsCohesion && isValidDestination)
        {
            if (commandManager != null)
            {
                int oldR = currentSelectedDieCoordinate.y;
                int oldC = currentSelectedDieCoordinate.x;
                int newR = targetCoordinate.y;
                int newC = targetCoordinate.x;

                GameManager gm = GameManager.Instance;
                if (gm == null) return;

                int currentPlayerIndex = gm.CurrentPlayerIndex;

                IGridCommand moveCommand = new MoveCommand(gm, currentPlayerIndex, oldR, oldC, newR, newC);
                commandManager.ExecuteCommand(moveCommand);
                Debug.Log($"[Movimiento] Dado movido con éxito de {currentSelectedDieCoordinate} a {targetCoordinate}.");
            }

            // CONSUMO MULTIJUGADOR AISLADO
            int pIndex = GameManager.Instance.CurrentPlayerIndex;
            playerMoveUses[pIndex]++;

            ResetMoveMode();

            if (playerMoveUses[pIndex] >= maxMoveUses)
            {
                if (moveActionButton != null) moveActionButton.LockToggle();
            }

            ClearSelection();

            if (GameManager.Instance != null && GameManager.Instance.placementOrchestrator != null)
            {
                GameManager.Instance.placementOrchestrator.RefreshPlacementHighlights();
            }
        }
        else
        {
            Debug.LogWarning($"[Movimiento] Inválido. Vacía: {isTargetEmpty} | Rompe tablero: {!maintainsCohesion} | Destino Legal: {isValidDestination}");
        }
    }

    /// <summary>
    /// Devuelve un uso del comodín al jugador si realiza un Undo exitoso.
    /// </summary>
    public void RefundMoveUse(int playerIndex)
    {
        if (playerMoveUses.ContainsKey(playerIndex) && playerMoveUses[playerIndex] > 0)
        {
            playerMoveUses[playerIndex]--;

            // Si vuelve a ser elegible para usarlo y es su turno actual, liberamos el botón
            if (GameManager.Instance.CurrentPlayerIndex == playerIndex)
            {
                if (moveActionButton != null)
                {
                    moveActionButton.SetInteractable(true);
                }
            }
        }
    }

    public void ResetMoveMode()
    {
        currentState = InteractionState.Normal;
        ClearSelection();
        if (moveActionButton != null) moveActionButton.ResetButtonState();
    }

    private void ClearSelection()
    {
        currentSelectedDieCoordinate = new Vector2Int(-1, -1);
        ClearBoardHighlights();
    }

    private bool HasValidSelection()
    {
        return currentSelectedDieCoordinate.x != -1 && currentSelectedDieCoordinate.y != -1;
    }

    public void HighlightValidMoveCells(Vector2Int originCoordinate)
    {
        if (boardLogic == null) return;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.gridManager == null) return;

        int playerIndex = gm.CurrentPlayerIndex;

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                Vector2Int checkPos = new Vector2Int(c, r);
                if (!boardLogic.occupiedCells.Contains(checkPos) && IsValidMoveDestination(checkPos))
                {
                    CellComponent cell = gm.gridManager.allCellsVisual[playerIndex][r, c];
                    if (cell != null) cell.SetHighlight(true);
                }
            }
        }
    }

    public void ClearBoardHighlights()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.gridManager == null) return;

        int playerIndex = gm.CurrentPlayerIndex;

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                CellComponent cell = gm.gridManager.allCellsVisual[playerIndex][r, c];
                if (cell != null) cell.SetHighlight(false);
            }
        }
    }

    public void LockUIForTransition()
    {
        ResetMoveMode(); // Apaga el estado interno y limpia selecciones

        if (moveActionButton != null)
        {
            // Bloquea el botón de la UI físicamente
            moveActionButton.SetInteractable(false);
        }
    }

}