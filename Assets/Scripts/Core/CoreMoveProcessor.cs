using System.Collections.Generic;

namespace MyGame.Core
{
    public struct MoveExecutionResult
    {
        public bool IsValid;
        public bool PatternCompleted;
        public int PointsAwarded;
        public List<GridPos> CompletedCells;
    }

    public static class CoreMoveProcessor
    {
        public static MoveExecutionResult ProcessMove(MatchStateDTO state, int playerIndex, int originX, int originY, int targetX, int targetY, PatternDefDTO variantPattern)
        {
            // 1. Firewall de Arquitectura: Verificación de seguridad estricta
            if (!CoreMoveValidator.IsValidMoveDestination(state, playerIndex, originX, originY, targetX, targetY, variantPattern))
            {
                return new MoveExecutionResult { IsValid = false };
            }

            if (!CoreMoveValidator.MaintainsCohesion(state, playerIndex, originX, originY, targetX, targetY))
            {
                return new MoveExecutionResult { IsValid = false };
            }

            var player = state.PlayerProfiles[playerIndex];
            var board = state.PlayerBoards[playerIndex];

            int originIndex = board.GetIndex(originY, originX);
            int targetIndex = board.GetIndex(targetY, targetX);

            var movingDie = board.Cells[originIndex];
            var originPos = new GridPos(originX, originY);
            var targetPos = new GridPos(targetX, targetY);

            // 2. Mutación del Estado Puro (Zero MonoBehaviour Dependency)
            board.Cells[originIndex] = new CellStateDTO { IsOccupied = false };
            board.Cells[targetIndex] = movingDie;

            MoveExecutionResult result = new MoveExecutionResult { IsValid = true, CompletedCells = new List<GridPos>() };

            // 3. Recálculo de Grupos y Patrones en Memoria
            if (player.ActiveGroups.TryGetValue(movingDie.Color, out GroupDataDTO group))
            {
                group.OccupiedCells.Remove(originPos);
                group.OccupiedCells.Add(targetPos);

                if (group.OccupiedCells.Count == group.TargetSize)
                {
                    // La topología ya fue garantizada por el CoreMoveValidator
                    result.PatternCompleted = true;

                    // Economía procesada internamente
                    result.PointsAwarded = 100; // Extraer del diccionario de sesión de CoreEconomyProcessor 
                    player.Score += result.PointsAwarded;

                    // CORRECCIÓN CS1061: Acceso de alto rendimiento al Array Primitivo
                    // Protegemos el servidor validando los límites de memoria del array
                    if (group.TargetSize >= 0 && group.TargetSize < player.PatternCounts.Length)
                    {
                        player.PatternCounts[group.TargetSize]++;
                    }

                    result.CompletedCells.AddRange(group.OccupiedCells);
                }
            }

            return result;
        }

        public static void UndoMove(MatchStateDTO state, int playerIndex, int originX, int originY, int targetX, int targetY, MoveExecutionResult previousResult)
        {
            var player = state.PlayerProfiles[playerIndex];
            var board = state.PlayerBoards[playerIndex];

            // 1. RECONSTRUCCIÓN DE ÍNDICES INVERSOS
            int originIndex = board.GetIndex(originY, originX);
            int targetIndex = board.GetIndex(targetY, targetX);

            // El dado actualmente reside en el targetIndex
            var movingDie = board.Cells[targetIndex];
            var originPos = new GridPos(originX, originY);
            var targetPos = new GridPos(targetX, targetY);

            // 2. MUTACIÓN INVERSA DEL ESTADO PURO
            board.Cells[originIndex] = movingDie;
            board.Cells[targetIndex] = new CellStateDTO { IsOccupied = false };

            // 3. DECONSTRUCCIÓN DE TOPOLOGÍA Y ECONOMÍA
            if (player.ActiveGroups.TryGetValue(movingDie.Color, out GroupDataDTO group))
            {
                // Restauramos la huella del grupo
                group.OccupiedCells.Remove(targetPos);
                group.OccupiedCells.Add(originPos);

                // Si este movimiento otorgó puntos, los confiscamos implacablemente
                if (previousResult.PatternCompleted)
                {
                    player.Score -= previousResult.PointsAwarded;

                    // Acceso seguro al array primitivo (CS1061 mitigado)
                    if (group.TargetSize >= 0 && group.TargetSize < player.PatternCounts.Length)
                    {
                        player.PatternCounts[group.TargetSize]--;
                    }
                }
            }
        }

    }

}