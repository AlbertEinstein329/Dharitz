using System.Collections.Generic;

namespace MyGame.Core
{
    /// <summary>
    /// Punto de restauracion del turno: copia profunda del perfil y del tablero de un jugador
    /// justo antes de una accion reversible (colocar un dado o mover uno).
    /// </summary>
    public sealed class TurnCheckpoint
    {
        public bool IsPlacement;
        public PlayerDataDTO Profile;
        public BoardStateDTO Board;
        public DieColor? HandColor;
        public int? HandValue;
    }

    /// <summary>
    /// Acciones del turno online posteriores a colocar: Mover, Deshacer y consumir la mano.
    /// Logica pura de la Capa 0. El turno solo termina al confirmarlo (o al agotarse el tiempo),
    /// asi que el jugador puede reversar sus acciones mientras tanto con una pila LIFO de puntos de restauracion.
    ///
    /// Convencion de coordenadas de este archivo: (fila, columna), igual que PlaceDieCommand
    /// y que GroupDataDTO.OccupiedCells (X = fila, Y = columna).
    /// </summary>
    public static class CoreTurnActions
    {
        public static TurnCheckpoint Capture(MatchStateDTO state, int playerIndex, bool isPlacement)
        {
            return new TurnCheckpoint
            {
                IsPlacement = isPlacement,
                Profile = CloneProfile(state.PlayerProfiles[playerIndex]),
                Board = CloneBoard(state.PlayerBoards[playerIndex]),
                HandColor = state.CurrentDrawnColor,
                HandValue = state.CurrentDrawnValue
            };
        }

        // Vacia la mano tras colocar el dado (el turno sigue abierto hasta confirmar)
        public static void ConsumeHand(MatchStateDTO state)
        {
            state.HasDrawn = false;
            state.CurrentDrawnColor = null;
            state.CurrentDrawnValue = null;
        }

        /// <summary>
        /// Revierte la ultima accion del turno. Gasta un uso de Undo; si revierte un Move, devuelve el uso de Move.
        /// Revertir una colocacion devuelve el dado a la mano.
        /// </summary>
        public static bool TryUndo(MatchStateDTO state, int playerIndex, List<TurnCheckpoint> history)
        {
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return false;
            if (state.CurrentPlayerIndex != playerIndex) return false;
            if (history.Count == 0) return false;

            PlayerDataDTO current = state.PlayerProfiles[playerIndex];
            if (current.CurrentUndoUses <= 0) return false;

            TurnCheckpoint checkpoint = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);

            PlayerDataDTO restored = checkpoint.Profile;
            restored.CurrentUndoUses = current.CurrentUndoUses - 1;
            restored.CurrentMoveUses = checkpoint.IsPlacement ? current.CurrentMoveUses : current.CurrentMoveUses + 1;
            restored.ReDraws = current.ReDraws;
            restored.TotalCoins = current.TotalCoins;

            state.PlayerProfiles[playerIndex] = restored;
            state.PlayerBoards[playerIndex] = checkpoint.Board;

            if (checkpoint.IsPlacement)
            {
                state.HasDrawn = true;
                state.CurrentDrawnColor = checkpoint.HandColor;
                state.CurrentDrawnValue = checkpoint.HandValue;
            }

            return true;
        }

        /// <summary>
        /// Mueve un dado propio a otra casilla vacia (comodin Move). Reversible con TryUndo.
        /// </summary>
        public static MoveExecutionResult TryMove(MatchStateDTO state, int playerIndex, int fromRow, int fromCol, int toRow, int toCol, PatternDefDTO variantPattern, List<TurnCheckpoint> history)
        {
            var invalid = new MoveExecutionResult { IsValid = false };

            if (state.CurrentPhase != MatchPhase.PlayerTurn) return invalid;
            if (state.CurrentPlayerIndex != playerIndex) return invalid;
            if (!state.PlayerProfiles.TryGetValue(playerIndex, out PlayerDataDTO profile)) return invalid;
            if (!state.PlayerBoards.TryGetValue(playerIndex, out BoardStateDTO board)) return invalid;
            if (profile.CurrentMoveUses <= 0) return invalid;

            if (!InBounds(board, fromRow, fromCol) || !InBounds(board, toRow, toCol)) return invalid;
            if (fromRow == toRow && fromCol == toCol) return invalid;

            TurnCheckpoint checkpoint = Capture(state, playerIndex, false);

            // CoreMoveProcessor usa (X = columna, Y = fila)
            MoveExecutionResult result = CoreMoveProcessor.ProcessMove(state, playerIndex, fromCol, fromRow, toCol, toRow, variantPattern);
            if (!result.IsValid) return result;

            profile.CurrentMoveUses--;
            history.Add(checkpoint);
            return result;
        }

        private static bool InBounds(BoardStateDTO board, int row, int col)
        {
            return row >= 0 && row < board.Rows && col >= 0 && col < board.Cols;
        }

        private static BoardStateDTO CloneBoard(BoardStateDTO source)
        {
            var clone = new BoardStateDTO(source.Rows, source.Cols);
            System.Array.Copy(source.Cells, clone.Cells, source.Cells.Length);
            return clone;
        }

        private static PlayerDataDTO CloneProfile(PlayerDataDTO source)
        {
            var clone = new PlayerDataDTO(source.Id, source.Name)
            {
                Score = source.Score,
                PlacedDice = source.PlacedDice,
                IsEliminated = source.IsEliminated,
                PatternCounts = (int[])source.PatternCounts.Clone(),
                AccumulatedStructurePoints = source.AccumulatedStructurePoints,
                AccumulatedOnesPenalty = source.AccumulatedOnesPenalty,
                ReDraws = source.ReDraws,
                CurrentUndoUses = source.CurrentUndoUses,
                CurrentMoveUses = source.CurrentMoveUses,
                TotalCoins = source.TotalCoins
            };

            foreach (var kvp in source.ActiveGroups)
            {
                GroupDataDTO group = kvp.Value;
                if (group == null) continue;

                var groupClone = new GroupDataDTO(group.Id, group.Color, group.TargetSize);
                foreach (GridPos cell in group.OccupiedCells) groupClone.OccupiedCells.Add(cell);
                clone.ActiveGroups[kvp.Key] = groupClone;
            }

            return clone;
        }
    }
}
