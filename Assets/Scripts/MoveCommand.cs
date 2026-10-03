using UnityEngine;
using System.Collections.Generic;

public class MoveCommand : IGridCommand
{
    private GameManager gm;
    private int playerIndex;
    private int oldR, oldC;
    private int newR, newC;

    private MyGame.Core.MoveExecutionResult executionResult;

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
        // 1. Extracción de dependencias locales para el DTO
        VariantData variant = gm.currentSession != null ? gm.currentSession.selectedVariant : gm.currentVariant;
        var playerDTO = gm.ServerState.PlayerProfiles[playerIndex];

        int targetSize = 0;
        var logic = gm.gridManager.allBoardsLogic[playerIndex];
        if (logic[oldR, oldC] != null && playerDTO.ActiveGroups.ContainsKey((MyGame.Core.DieColor)logic[oldR, oldC].color))
        {
            targetSize = playerDTO.ActiveGroups[(MyGame.Core.DieColor)logic[oldR, oldC].color].TargetSize;
        }

        PatternData pattern = variant.GetPattern(targetSize);
        MyGame.Core.PatternDefDTO patternDTO = pattern != null ? pattern.ToDTO() : null;

        // 2. EJECUCIÓN AUTORITATIVA (El Servidor aprueba y muta los DTOs)
        executionResult = MyGame.Core.CoreMoveProcessor.ProcessMove(
            gm.ServerState, playerIndex, oldC, oldR, newC, newR, patternDTO
        );

        if (!executionResult.IsValid)
        {
            Debug.LogError("[Seguridad] Ejecución de movimiento interceptada y rechazada por Capa 0.");
            return;
        }

        // 3. SINCRONIZACIÓN ESCLAVA DE LA CAPA VISUAL
        GridManager.DieData dieToMove = logic[oldR, oldC];
        if (dieToMove == null) return;

        DieColor color = dieToMove.color;
        int groupId = dieToMove.groupId;
        int dieValue = dieToMove.value;
        PlayerData p = gm.players[playerIndex];

        gm.gridManager.RemoveDie(playerIndex, oldR, oldC);
        gm.gridManager.CommitDieToLogic(playerIndex, newR, newC, color, groupId, dieValue);

        if (p.activeGroups.TryGetValue(color, out GroupData group) && group != null)
        {
            group.occupiedCells.Remove(new Vector2Int(oldC, oldR));
            group.occupiedCells.Add(new Vector2Int(newC, newR));
        }

        // 4. ANIMACIONES Y UI BASADAS EN EL VEREDICTO DEL SERVIDOR
        if (executionResult.PatternCompleted)
        {
            p.score = gm.ServerState.PlayerProfiles[playerIndex].Score; // Sincronización dictaminada por servidor
            p.patternCounts[group.targetSize]++;

            Vector3 posMundo = gm.gridManager.GetWorldPosition(playerIndex, newR, newC);
            PopUpManager.Instance.ShowPopUp(posMundo + Vector3.down * 1f, $"PERFECT! +{executionResult.PointsAwarded}", Color.cyan);

            List<Vector2Int> visualCells = new List<Vector2Int>();
            foreach (var cell in executionResult.CompletedCells)
                visualCells.Add(new Vector2Int(cell.X, cell.Y));

            gm.gridManager.MarkGroupAsCompleted(playerIndex, visualCells);
            UIManager.Instance.UpdateScore(p.score);
        }
    }

    public void Undo()
    {
        // Si el movimiento original nunca fue válido, no hay nada que revertir
        if (!executionResult.IsValid) return;

        GridManager.DieData logicDie = gm.gridManager.allBoardsLogic[playerIndex][newR, newC];
        if (logicDie == null) return;

        DieColor color = logicDie.color;
        int groupId = logicDie.groupId;
        int dieValue = logicDie.value;
        PlayerData p = gm.players[playerIndex];

        // 1. EL SERVIDOR RESTAURA LA VERDAD ABSOLUTA PRIMERO
        MyGame.Core.CoreMoveProcessor.UndoMove(
            gm.ServerState, playerIndex, oldC, oldR, newC, newR, executionResult
        );

        // 2. EL CLIENTE OBEDECE Y REVIERTE LA MEMORIA VISUAL
        gm.gridManager.RemoveDie(playerIndex, newR, newC);
        gm.gridManager.CommitDieToLogic(playerIndex, oldR, oldC, color, groupId, dieValue);

        if (p.activeGroups.TryGetValue(color, out GroupData group) && group != null)
        {
            group.occupiedCells.Remove(new Vector2Int(newC, newR));
            group.occupiedCells.Add(new Vector2Int(oldC, oldR));

            if (executionResult.PatternCompleted)
            {
                // Copiamos el puntaje exacto calculado por la Capa 0
                p.score = gm.ServerState.PlayerProfiles[playerIndex].Score;
                p.patternCounts[group.targetSize]--;

                UIManager.Instance.UpdateScore(p.score);
            }
        }
    }
}