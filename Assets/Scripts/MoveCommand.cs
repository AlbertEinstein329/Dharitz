using UnityEngine;

public class MoveCommand : IGridCommand
{
    private GameManager gm;
    private int playerIndex;
    private int oldR, oldC;
    private int newR, newC;

    private DieColor color;
    private int groupId;
    private int targetSize;
    private int dieValue;

    private bool executedSuccessfully = false;

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
        GridManager.DieData[,] logic = gm.gridManager.GetBoardLogic(playerIndex); // Necesitamos exponer esto en GridManager

        GridManager.DieData dieToMove = logic[oldR, oldC];
        if (dieToMove == null) return;

        color = dieToMove.color;
        groupId = dieToMove.groupId;
        dieValue = dieToMove.value;
        
        GroupData group = p.activeGroups[color];
        if (group == null) return;
        targetSize = group.targetSize;

        // 1. Verificar límites de uso
        if (p.currentMoveUses <= 0)
        {
            Debug.LogWarning("Move limits reached.");
            return;
        }

        // 2. Move Edge Verification (Verificar que extraer no parta el grupo)
        if (!TopologyCalculator.IsValidMoveExtraction(logic, gm.gridManager.rows, gm.gridManager.cols, oldR, oldC, groupId, p))
        {
            Debug.LogWarning("Invalid Move: Extracción particionaría el grupo.");
            return;
        }

        // 3. Simulación Atómica (Quitar temporalmente)
        logic[oldR, oldC] = null;
        group.occupiedCells.Remove(new Vector2Int(oldR, oldC));

        // 4. Validar en la nueva posición
        bool isValidNewPos = PlacementValidator.IsValidPlacement(logic, gm.gridManager.rows, gm.gridManager.cols, newR, newC, color, groupId, dieValue, p, gm.currentSession.selectedVariant);

        if (!isValidNewPos)
        {
            // Revertir simulación
            logic[oldR, oldC] = dieToMove;
            group.occupiedCells.Add(new Vector2Int(oldR, oldC));
            Debug.LogWarning("Invalid Move: Nueva posición inválida o rompe supervivencia.");
            return;
        }

        // 5. Aplicar Movimiento Definitivo
        // Aquí no descontamos `p.placedDice` porque es un movimiento, la cantidad de dados es la misma.
        // Pero podríamos tener que re-calcular el Score si la nueva posición afecta contactos o combos.
        // Para simplificar según el documento, "updates all logic arrays and visually animates the transition."
        
        gm.gridManager.RemoveDie(playerIndex, oldR, oldC); // Quita el visual viejo
        gm.gridManager.CommitDieToLogic(playerIndex, newR, newC, color, groupId, dieValue); // Crea el nuevo
        
        group.occupiedCells.Add(new Vector2Int(newR, newC));
        p.currentMoveUses--;

        executedSuccessfully = true;
        Debug.Log("Move Command ejecutado con éxito.");
    }

    public void Undo()
    {
        if (!executedSuccessfully) return;

        PlayerData p = gm.players[playerIndex];
        GridManager.DieData[,] logic = gm.gridManager.GetBoardLogic(playerIndex);
        GroupData group = p.activeGroups[color];

        // Mover de vuelta de newR,newC a oldR,oldC
        gm.gridManager.RemoveDie(playerIndex, newR, newC);
        group.occupiedCells.Remove(new Vector2Int(newR, newC));

        gm.gridManager.CommitDieToLogic(playerIndex, oldR, oldC, color, groupId, dieValue);
        group.occupiedCells.Add(new Vector2Int(oldR, oldC));

        p.currentMoveUses++; // Le devolvemos el uso
    }
}
