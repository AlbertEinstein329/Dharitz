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

    // AISLAMIENTO: Cada jugador rastrea sus propios usos del comodín Move
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

    // Se ejecuta de forma segura al cambiar de turno
    public void OnTurnChanged(int newPlayerIndex)
    {
        if (!playerMoveUses.ContainsKey(newPlayerIndex))
        {
            playerMoveUses[newPlayerIndex] = 0;
        }

        bool hasUsesLeft = playerMoveUses[newPlayerIndex] < maxMoveUses;
        if (moveActionButton != null)
        {
            moveActionButton.SetInteractable(hasUsesLeft);
        }
        ResetMoveMode();
    }

    public void ToggleMoveMode(bool isActive)
    {
        if (isActive)
        {
            int pIndex = GameManager.Instance.turnManager.CurrentPlayerIndex;
            if (!playerMoveUses.ContainsKey(pIndex)) playerMoveUses[pIndex] = 0;

            if (playerMoveUses[pIndex] >= maxMoveUses)
            {
                Debug.LogWarning("[UX] Ya has agotado tus usos de Move para este turno.");
                if (moveActionButton != null) moveActionButton.LockToggle();
                return;
            }

            currentState = InteractionState.MoveMode_WaitingForDie;
            ClearSelection();
        }
        else
        {
            ResetMoveMode();
        }
    }

    public void OnGridCellClicked(Vector2Int clickedCoordinate)
    {
        int pIndex = GameManager.Instance.turnManager.CurrentPlayerIndex;
        var logic = GameManager.Instance.gridManager.allBoardsLogic[pIndex];
        bool isOccupied = logic[clickedCoordinate.y, clickedCoordinate.x] != null;

        switch (currentState)
        {
            case InteractionState.MoveMode_WaitingForDie:
                if (isOccupied) TrySelectDieForMove(clickedCoordinate);
                break;

            case InteractionState.MoveMode_WaitingForDestination:
                if (isOccupied)
                    TrySelectDieForMove(clickedCoordinate);
                else
                    TryExecuteMove(clickedCoordinate);
                break;
        }
    }

    private void TrySelectDieForMove(Vector2Int coordinate)
    {
        int pIndex = GameManager.Instance.turnManager.CurrentPlayerIndex;
        var logic = GameManager.Instance.gridManager.allBoardsLogic[pIndex];

        if (logic[coordinate.y, coordinate.x] != null)
        {
            ClearBoardHighlights();
            currentSelectedDieCoordinate = coordinate;
            currentState = InteractionState.MoveMode_WaitingForDestination;
            HighlightValidMoveCells(coordinate);
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

        int playerIndex = gm.turnManager.CurrentPlayerIndex;
        var dieToMove = gm.gridManager.allBoardsLogic[playerIndex][currentSelectedDieCoordinate.y, currentSelectedDieCoordinate.x];
        if (dieToMove == null) return false;

        PlayerData player = gm.turnManager.GetCurrentPlayer();
        int countInGroup = 0;
        int totalOccupied = 0;

        foreach (var die in gm.gridManager.allBoardsLogic[playerIndex])
        {
            if (die != null)
            {
                totalOccupied++;
                if (die.groupId == dieToMove.groupId) countInGroup++;
            }
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

        // NUEVO: Verificamos el conteo real, no el boardLogic
        if (totalOccupied <= 1) isLegal = true;
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
                                colorClash = true;
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
        if (currentSelectedDieCoordinate.x == -1) { ResetMoveMode(); return; }

        GameManager gm = GameManager.Instance;
        int pIndex = gm.turnManager.CurrentPlayerIndex;
        var logicMatrix = gm.gridManager.allBoardsLogic[pIndex];

        bool isTargetEmpty = logicMatrix[targetCoordinate.y, targetCoordinate.x] == null;
        bool maintainsCohesion = !boardLogic.WouldLeaveSplitIslands(currentSelectedDieCoordinate, targetCoordinate);
        bool isValidDestination = IsValidMoveDestination(targetCoordinate);

        if (isTargetEmpty && maintainsCohesion && isValidDestination)
        {
            int oldR = currentSelectedDieCoordinate.y;
            int oldC = currentSelectedDieCoordinate.x;
            int newR = targetCoordinate.y;
            int newC = targetCoordinate.x;

            IGridCommand moveCommand = new MoveCommand(gm, pIndex, oldR, oldC, newR, newC);
            commandManager.ExecuteCommand(moveCommand);

            playerMoveUses[pIndex]++;
            ResetMoveMode();

            if (playerMoveUses[pIndex] >= maxMoveUses)
            {
                if (moveActionButton != null) moveActionButton.LockToggle();
            }

            ClearSelection();
            if (gm.placementOrchestrator != null) gm.placementOrchestrator.RefreshPlacementHighlights();
        }
    }

    public void RefundMoveUse(int playerIndex)
    {
        if (playerMoveUses.ContainsKey(playerIndex) && playerMoveUses[playerIndex] > 0)
        {
            playerMoveUses[playerIndex]--;
            if (GameManager.Instance.turnManager.CurrentPlayerIndex == playerIndex && moveActionButton != null)
            {
                moveActionButton.SetInteractable(true);
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

    public void LockUIForTransition()
    {
        ResetMoveMode();
        if (moveActionButton != null) moveActionButton.SetInteractable(false);
    }

    public void HighlightValidMoveCells(Vector2Int originCoordinate)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.gridManager == null) return;

        // CORRECCIÓN: Buscamos dinámicamente las celdas visuales del jugador del turno actual
        int playerIndex = gm.turnManager.CurrentPlayerIndex;
        var logic = gm.gridManager.allBoardsLogic[playerIndex];

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                Vector2Int checkPos = new Vector2Int(c, r);
                if (logic[r, c] == null && IsValidMoveDestination(checkPos))
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

        // CORRECCIÓN MULTIJUGADOR: Limpiamos los brillos del tablero correcto
        int playerIndex = gm.turnManager.CurrentPlayerIndex;

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                CellComponent cell = gm.gridManager.allCellsVisual[playerIndex][r, c];
                if (cell != null) cell.SetHighlight(false);
            }
        }
    }
}