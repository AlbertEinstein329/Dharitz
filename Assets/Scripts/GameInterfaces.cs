using UnityEngine;

// Provides read-only turn information to UI or board cells.
public interface ITurnProvider
{
    int CurrentPlayerIndex { get; }
    bool HasDrawn { get; }
    DieColor CurrentDrawnColor { get; }
    PlayerData GetCurrentPlayer();
}

// Handles the execution of a placement intent (e.g., the 2-second buffer).
public interface IPlacementExecutor
{
    void BeginPlacement(int row, int col);
}

// Validates if a specific board cell can receive a specific die.
// Validates if a specific board cell can receive a specific die.
public interface IGridValidator
{
    bool CanBotPlaceHere(int playerIndex, int r, int c, DieColor color, int groupId, int targetSize);

    // NUEVO: Le damos permiso a la interfaz de usar el validador principal
    bool IsValidPlacement(int pIndex, int r, int c, DieColor color, int currentGroupId, int number);
}