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
    [SerializeField] private CommandManager commandManager;
    [SerializeField] private MoveActionButton moveActionButton;

    [Header("Estado Interno (Solo lectura)")]
    [SerializeField] private InteractionState currentState = InteractionState.Normal;
    private Vector2Int currentSelectedDieCoordinate = new Vector2Int(-1, -1);
    public bool IsMoveModeActive => currentState != InteractionState.Normal;

    [Header("Gestión de Comodines Multijugador")]
    [SerializeField] private int maxMoveUses = 1;
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

    public void OnTurnChanged(int newPlayerIndex)
    {
        if (!playerMoveUses.ContainsKey(newPlayerIndex)) playerMoveUses[newPlayerIndex] = 0;

        bool hasUsesLeft = playerMoveUses[newPlayerIndex] < maxMoveUses;
        if (moveActionButton != null) moveActionButton.SetInteractable(hasUsesLeft);

        ResetMoveMode();
    }

    public void ToggleMoveMode(bool isActive)
    {
        // Online, Move todavía no pasa por el servidor: se deshabilita para no desincronizar
        if (isActive && GameManager.Instance != null && GameManager.Instance.IsOnlineMatch) return;

        if (isActive)
        {
            int pIndex = GameManager.Instance.turnManager.CurrentPlayerIndex;
            if (!playerMoveUses.ContainsKey(pIndex)) playerMoveUses[pIndex] = 0;

            if (playerMoveUses[pIndex] >= maxMoveUses)
            {
                Debug.LogWarning("[UX] Usos de Move agotados para este turno.");
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
        var serverBoard = GameManager.Instance.ServerState.PlayerBoards[pIndex];
        int flatIndex = serverBoard.GetIndex(clickedCoordinate.y, clickedCoordinate.x);
        bool isOccupied = serverBoard.Cells[flatIndex].IsOccupied;

        switch (currentState)
        {
            case InteractionState.MoveMode_WaitingForDie:
                if (isOccupied) TrySelectDieForMove(clickedCoordinate);
                break;

            case InteractionState.MoveMode_WaitingForDestination:
                if (isOccupied) TrySelectDieForMove(clickedCoordinate);
                else TryExecuteMove(clickedCoordinate);
                break;
        }
    }

    private void TrySelectDieForMove(Vector2Int coordinate)
    {
        int pIndex = GameManager.Instance.turnManager.CurrentPlayerIndex;
        var serverBoard = GameManager.Instance.ServerState.PlayerBoards[pIndex];
        int flatIndex = serverBoard.GetIndex(coordinate.y, coordinate.x);

        if (serverBoard.Cells[flatIndex].IsOccupied)
        {
            ClearBoardHighlights();
            currentSelectedDieCoordinate = coordinate;
            currentState = InteractionState.MoveMode_WaitingForDestination;
            HighlightValidMoveCells(coordinate);
        }
    }

    // EXTRAE EL PATRÓN PARA LA PREDICCIÓN MATEMÁTICA
    private MyGame.Core.PatternDefDTO GetPatternForMovingDie(int playerIndex, int originX, int originY)
    {
        GameManager gm = GameManager.Instance;
        var serverBoard = gm.ServerState.PlayerBoards[playerIndex];
        var playerDTO = gm.ServerState.PlayerProfiles[playerIndex];

        int flatIndex = serverBoard.GetIndex(originY, originX);
        var movingDie = serverBoard.Cells[flatIndex];

        if (playerDTO.ActiveGroups.ContainsKey(movingDie.Color))
        {
            int targetSize = playerDTO.ActiveGroups[movingDie.Color].TargetSize;
            VariantData variant = gm.currentSession != null ? gm.currentSession.selectedVariant : gm.currentVariant;
            PatternData pattern = variant.GetPattern(targetSize);
            return pattern != null ? pattern.ToDTO() : null;
        }
        return null;
    }

    private void TryExecuteMove(Vector2Int targetCoordinate)
    {
        if (currentSelectedDieCoordinate.x == -1) { ResetMoveMode(); return; }

        GameManager gm = GameManager.Instance;
        int pIndex = gm.turnManager.CurrentPlayerIndex;

        // 1. Predicción: Solicitamos el DTO de Patrón
        var patternDTO = GetPatternForMovingDie(pIndex, currentSelectedDieCoordinate.x, currentSelectedDieCoordinate.y);

        // 2. Predicción: Verificación de Supervivencia Topológica (CS7036 Resuelto)
        bool isValidDestination = MyGame.Core.CoreMoveValidator.IsValidMoveDestination(
            gm.ServerState, pIndex, currentSelectedDieCoordinate.x, currentSelectedDieCoordinate.y, targetCoordinate.x, targetCoordinate.y, patternDTO
        );

        // 3. Predicción: Verificación de Cohesión 1D (Sustituye al viejo WouldLeaveSplitIslands)
        bool maintainsCohesion = MyGame.Core.CoreMoveValidator.MaintainsCohesion(
            gm.ServerState, pIndex, currentSelectedDieCoordinate.x, currentSelectedDieCoordinate.y, targetCoordinate.x, targetCoordinate.y
        );

        if (isValidDestination && maintainsCohesion)
        {
            // El comando delegará la autoridad absoluta al CoreMoveProcessor
            IGridCommand moveCommand = new MoveCommand(gm, pIndex, currentSelectedDieCoordinate.y, currentSelectedDieCoordinate.x, targetCoordinate.y, targetCoordinate.x);
            commandManager.ExecuteCommand(moveCommand);

            playerMoveUses[pIndex]++;
            ResetMoveMode();

            if (playerMoveUses[pIndex] >= maxMoveUses && moveActionButton != null)
            {
                moveActionButton.LockToggle();
            }

            ClearSelection();
            if (gm.placementOrchestrator != null) gm.placementOrchestrator.RefreshPlacementHighlights();
        }
        else
        {
            Debug.LogWarning("[Network/Core] Movimiento predictivo del cliente rechazado (Ruta inválida o fragmentación de isla).");
        }
    }

    public void HighlightValidMoveCells(Vector2Int originCoordinate)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.gridManager == null) return;

        int pIndex = gm.turnManager.CurrentPlayerIndex;
        var serverBoard = gm.ServerState.PlayerBoards[pIndex];

        var patternDTO = GetPatternForMovingDie(pIndex, originCoordinate.x, originCoordinate.y);

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int flatIndex = serverBoard.GetIndex(r, c);

                // CS7036 resuelto e inyección de cohesión visual pura en O(1) + BFS.
                if (!serverBoard.Cells[flatIndex].IsOccupied &&
                    MyGame.Core.CoreMoveValidator.IsValidMoveDestination(gm.ServerState, pIndex, originCoordinate.x, originCoordinate.y, c, r, patternDTO) &&
                    MyGame.Core.CoreMoveValidator.MaintainsCohesion(gm.ServerState, pIndex, originCoordinate.x, originCoordinate.y, c, r))
                {
                    CellComponent cell = gm.gridManager.allCellsVisual[pIndex][r, c];
                    if (cell != null) cell.SetHighlight(true);
                }
            }
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

    public void ClearBoardHighlights()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.gridManager == null) return;

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