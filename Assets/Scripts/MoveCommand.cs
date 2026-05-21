using UnityEngine;

public class MoveCommand : IGridCommand
{
    private GameManager gm;
    private int playerIndex;
    private int oldR, oldC;
    private int newR, newC;

    private DieColor color;
    private int groupId;
    private int dieValue;

    private bool executedSuccessfully = false;
    private bool patternAwardedByThisMove = false;
    private int pointsAwarded = 0;

    public MoveCommand(GameManager gm, int playerIndex, int oldR, int oldC, int newR, int newC)
    {
        this.gm = gm;
        this.playerIndex = playerIndex;
        this.oldR = oldR;
        this.oldC = oldC;
        this.newR = newR;
        this.newC = newC;
    }

    public void Execute()
    {
        PlayerData p = gm.players[playerIndex];
        GridManager.DieData[,] logic = gm.gridManager.allBoardsLogic[playerIndex];

        GridManager.DieData dieToMove = logic[oldR, oldC];
        if (dieToMove == null) return;

        color = dieToMove.color;
        groupId = dieToMove.groupId;
        dieValue = dieToMove.value;

        // 1. Ejecución Física
        gm.gridManager.RemoveDie(playerIndex, oldR, oldC);
        gm.gridManager.CommitDieToLogic(playerIndex, newR, newC, color, groupId, dieValue);

        // 2. ACTUALIZACIÓN DE MEMORIA (Alineado con PlacementOrchestrator: R, C)
        if (p.activeGroups.TryGetValue(color, out GroupData group) && group != null)
        {
            group.occupiedCells.Remove(new Vector2Int(oldR, oldC));
            group.occupiedCells.Add(new Vector2Int(newR, newC));

            // 3. EVALUACIÓN DE PATRÓN Y ECONOMÍA
            if (group.occupiedCells.Count == group.targetSize)
            {
                VariantData variant = gm.currentSession != null ? gm.currentSession.selectedVariant : gm.currentVariant;
                PatternData pattern = variant.GetPattern(group.targetSize);

                if (PatternValidator.CheckPattern(group.occupiedCells, pattern))
                {
                    patternAwardedByThisMove = true;
                    pointsAwarded = ScoreManager.Instance.GetPatternBonus(group.targetSize);

                    p.score += pointsAwarded;
                    p.patternCounts[group.targetSize]++;

                    Vector3 posMundo = gm.gridManager.GetWorldPosition(playerIndex, newR, newC);
                    PopUpManager.Instance.ShowPopUp(posMundo + Vector3.down * 1f, $"PERFECT! +{pointsAwarded}", Color.cyan);

                    gm.gridManager.MarkGroupAsCompleted(playerIndex, group.occupiedCells);
                    UIManager.Instance.UpdateScore(p.score);
                }
            }
        }

        executedSuccessfully = true;
    }

    public void Undo()
    {
        if (!executedSuccessfully) return;

        PlayerData p = gm.players[playerIndex];

        gm.gridManager.RemoveDie(playerIndex, newR, newC);
        gm.gridManager.CommitDieToLogic(playerIndex, oldR, oldC, color, groupId, dieValue);

        if (p.activeGroups.TryGetValue(color, out GroupData group) && group != null)
        {
            group.occupiedCells.Remove(new Vector2Int(newR, newC));
            group.occupiedCells.Add(new Vector2Int(oldR, oldC));

            if (patternAwardedByThisMove)
            {
                p.score -= pointsAwarded;
                p.patternCounts[group.targetSize]--;
                UIManager.Instance.UpdateScore(p.score);
            }
        }
    }
}