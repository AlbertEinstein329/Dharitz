using System.Collections.Generic;

namespace MyGame.Core
{
    public static class CoreMoveValidator
    {
        public static bool IsValidMoveDestination(MatchStateDTO state, int playerIndex, int originX, int originY, int targetX, int targetY, PatternDefDTO variantPattern)
        {
            if (!state.PlayerProfiles.ContainsKey(playerIndex)) return false;

            var boardState = state.PlayerBoards[playerIndex];
            int cols = boardState.Cols;
            int rows = boardState.Rows;

            int originIndex = boardState.GetIndex(originY, originX);
            int targetIndex = boardState.GetIndex(targetY, targetX);

            // 1. REGLAS FUNDAMENTALES DEL ESPACIO-TIEMPO
            if (boardState.Cells[targetIndex].IsOccupied) return false;
            if (!boardState.Cells[originIndex].IsOccupied) return false;

            var movingDie = boardState.Cells[originIndex];
            var player = state.PlayerProfiles[playerIndex];

            // 2. FOTOGRAFÍA ESTADÍSTICA EN 1D
            int groupCount = 0;
            int totalOccupied = 0;

            for (int i = 0; i < boardState.Cells.Length; i++)
            {
                if (boardState.Cells[i].IsOccupied)
                {
                    totalOccupied++;
                    if (boardState.Cells[i].GroupId == movingDie.GroupId)
                    {
                        groupCount++;
                    }
                }
            }

            bool isBoardEmpty = (totalOccupied <= 1);
            if (isBoardEmpty) return true;

            bool isSoloDie = (groupCount <= 1);
            bool touchesOwnGroup = false;
            bool touchesAnyDie = false;

            // 3. ESCÁNER ESPACIAL 1D (Radar 3x3)
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int i = 0; i < 8; i++)
            {
                int nx = targetX + dx[i];
                int ny = targetY + dy[i];

                if (nx >= 0 && nx < cols && ny >= 0 && ny < rows)
                {
                    int neighborIndex = boardState.GetIndex(ny, nx);

                    // FANTASMA: Ignoramos la celda de donde viene el dado
                    if (neighborIndex == originIndex) continue;

                    var neighbor = boardState.Cells[neighborIndex];

                    if (neighbor.IsOccupied)
                    {
                        if (neighbor.GroupId == movingDie.GroupId) touchesOwnGroup = true;

                        bool isOrthogonal = (dx[i] == 0 || dy[i] == 0);
                        if (isOrthogonal)
                        {
                            touchesAnyDie = true;
                            // Choque de color ortogonal de distinto grupo = ilegal
                            if (neighbor.Color == movingDie.Color && neighbor.GroupId != movingDie.GroupId) return false;
                        }
                        else
                        {
                            // En diagonales, solo reconocemos toques válidos si son del mismo grupo
                            if (neighbor.GroupId == movingDie.GroupId) touchesAnyDie = true;
                        }
                    }
                }
            }

            if (!isSoloDie && !touchesOwnGroup) return false;
            if (!touchesAnyDie) return false;

            // 4. INYECCIÓN TOPOLÓGICA (Mutación In-Place Temporal)
            boardState.Cells[originIndex].IsOccupied = false;

            bool removedFromGroup = false;
            GridPos ghostPos = new GridPos(originX, originY);
            if (player.ActiveGroups.ContainsKey(movingDie.Color))
            {
                removedFromGroup = player.ActiveGroups[movingDie.Color].OccupiedCells.Remove(ghostPos);
            }

            // Ejecutamos la matemática pesada engañando al sistema
            bool survivalValid = CoreTopologyCalculator.ValidateSurvival(
                boardState, targetY, targetX, movingDie.Color, movingDie.GroupId, movingDie.Value, player, variantPattern, variantPattern
            );

            // RESTAURACIÓN INMEDIATA DEL ESTADO (Obligatorio)
            boardState.Cells[originIndex].IsOccupied = true;
            if (removedFromGroup)
            {
                player.ActiveGroups[movingDie.Color].OccupiedCells.Add(ghostPos);
            }

            return survivalValid;
        }

        // ====================================================================
        // REEMPLAZO DE ALTO RENDIMIENTO PARA 'BoardLogic.WouldLeaveSplitIslands'
        // ====================================================================
        public static bool MaintainsCohesion(MatchStateDTO state, int playerIndex, int originX, int originY, int targetX, int targetY)
        {
            var boardState = state.PlayerBoards[playerIndex];
            int cols = boardState.Cols;
            int rows = boardState.Rows;

            int originIndex = boardState.GetIndex(originY, originX);
            int targetIndex = boardState.GetIndex(targetY, targetX);

            int totalOccupied = 0;
            for (int i = 0; i < boardState.Cells.Length; i++)
            {
                if (boardState.Cells[i].IsOccupied && i != originIndex) totalOccupied++;
            }

            totalOccupied++; // Contabilizamos la nueva posición destino
            if (totalOccupied <= 1) return true;

            Queue<int> queue = new Queue<int>();
            HashSet<int> visited = new HashSet<int>();

            queue.Enqueue(targetIndex);
            visited.Add(targetIndex);

            int[] dx = { 0, 0, -1, 1, 1, 1, -1, -1 };
            int[] dy = { -1, 1, 0, 0, -1, 1, -1, 1 };

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int cx = current % cols;
                int cy = current / cols;

                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];

                    if (nx >= 0 && nx < cols && ny >= 0 && ny < rows)
                    {
                        int neighborIndex = boardState.GetIndex(ny, nx);

                        bool isOccupiedSimulated = (neighborIndex == targetIndex) || (boardState.Cells[neighborIndex].IsOccupied && neighborIndex != originIndex);

                        if (isOccupiedSimulated && !visited.Contains(neighborIndex))
                        {
                            visited.Add(neighborIndex);
                            queue.Enqueue(neighborIndex);
                        }
                    }
                }
            }

            return visited.Count == totalOccupied;
        }
    }
}