using UnityEngine;

public class UndoCommand : IGridCommand
{
    private GameManager gm;
    private int playerIndex;
    private int r, c;
    private DieColor color;
    private int groupId;
    private int targetSize;

    // Previous State Mementos
    private int prevScore;
    private int prevPlacedDice;
    private int prevAccumulatedStructurePoints;
    private int[] prevPatternCounts;

    public UndoCommand(GameManager gm, int playerIndex, int r, int c, DieColor color, int groupId, int targetSize)
    {
        this.gm = gm;
        this.playerIndex = playerIndex;
        this.r = r;
        this.c = c;
        this.color = color;
        this.groupId = groupId;
        this.targetSize = targetSize;

        PlayerData p = gm.players[playerIndex];
        this.prevScore = p.score;
        this.prevPlacedDice = p.placedDice;
        this.prevAccumulatedStructurePoints = p.accumulatedStructurePoints;
        this.prevPatternCounts = (int[])p.patternCounts.Clone();
    }

    public void Execute()
    {
        // Execute here does the actual placement if we were to replay it,
        // but since this command is created *after* the placement is confirmed by PlacementOrchestrator,
        // we leave this empty. The CommandManager will just push it to the stack.
    }

    public void Undo()
    {
        PlayerData p = gm.players[playerIndex];
        
        if (!p.activeGroups.ContainsKey(color)) return;
        GroupData group = p.activeGroups[color];

        // 1. Restore Player primitives
        p.score = prevScore;
        p.placedDice = prevPlacedDice;
        p.accumulatedStructurePoints = prevAccumulatedStructurePoints;
        p.patternCounts = prevPatternCounts;

        // 2. Remove from group's occupied cells
        group.occupiedCells.Remove(new Vector2Int(r, c));

        // 3. Remove from logic and visual grid
        gm.gridManager.RemoveDie(playerIndex, r, c);

        // 4. Update UI to reflect the restored state
        UIManager.Instance.UpdateScore(p.score);
        UIManager.Instance.UpdateProgressText(color, targetSize, group.occupiedCells.Count, targetSize);
        
        // Return the die to the active hand space and unlock
        UIManager.Instance.ClearDieUI(); // Hides current if needed, but we need it back?
        // Wait, ClearDieUI clears the UI. If we undo, the player should be able to place the SAME die again.
        // We will call RestoreDieToHand in UIManager
        UIManager.Instance.RestoreDieToHand(color, targetSize);

        gm.turnManager.HasDrawn = true; // Player is back to the state of having drawn but not placed

        Debug.Log("Undo executed: Reverted last placement.");
    }
}
