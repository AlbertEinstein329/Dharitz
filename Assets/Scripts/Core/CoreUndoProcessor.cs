using System;
using System.Linq;

namespace MyGame.Core
{
    [Serializable]
    public struct UndoDieCommand
    {
        public int PlayerId;
        public GridPos TargetCell;
        public DieColor Color;
        public int GroupId;
        public int Number;
    }

    public static class CoreUndoProcessor
    {
        public static bool ProcessUndoIntent(MatchStateDTO state, UndoDieCommand command, int rowBonus, int colBonus, int intersectionBonus, float[] rowMults, float[] colMults)
        {
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return false;
            if (state.CurrentPlayerIndex != command.PlayerId) return false;

            BoardStateDTO board = state.PlayerBoards[command.PlayerId];
            PlayerDataDTO player = state.PlayerProfiles[command.PlayerId];

            int targetIndex = board.GetIndex(command.TargetCell.X, command.TargetCell.Y);
            if (!board.Cells[targetIndex].IsOccupied) return false;

            if (player.CurrentUndoUses <= 0) return false; // El servidor rechaza si no hay usos legítimos

            // Descuento autoritativo en la Capa 0
            player.CurrentUndoUses--;

            // 1. Limpieza Topológica
            board.Cells[targetIndex] = new CellStateDTO { IsOccupied = false };
            player.PlacedDice--;

            // 2. Limpieza de Memoria de Grupos
            if (player.ActiveGroups.ContainsKey(command.Color))
            {
                player.ActiveGroups[command.Color].OccupiedCells.Remove(command.TargetCell);
            }

            // 3. Reconstrucción Matemática de Combos
            player.AccumulatedStructurePoints = 0;
            player.Score -= 50;

            CoreScoreCalculator.EvaluateAndApplyCombos(
                board.Cells, board.Rows, board.Cols, player,
                rowBonus, colBonus, intersectionBonus, rowMults, colMults
            );

            return true;
        }
    }
}