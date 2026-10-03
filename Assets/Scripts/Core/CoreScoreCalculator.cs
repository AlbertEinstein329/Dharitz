using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    public static class CoreScoreCalculator
    {
        public static int EvaluateAndApplyCombos(CellStateDTO[] grid, int rows, int cols, PlayerDataDTO player, int rowBonus, int colBonus, int intersectionBonus, float[] rowMultipliers, float[] colMultipliers)
        {
            int completedRows = 0;
            int completedCols = 0;
            int maxConsecutiveRows = 0;
            int maxConsecutiveCols = 0;
            int intersections = 0;

            bool[] rowsFull = new bool[rows];
            bool[] colsFull = new bool[cols];

            int currentConsecutive = 0;
            for (int r = 0; r < rows; r++)
            {
                rowsFull[r] = true;
                for (int c = 0; c < cols; c++)
                {
                    if (!grid[r * cols + c].IsOccupied) { rowsFull[r] = false; break; }
                }

                if (rowsFull[r])
                {
                    completedRows++;
                    currentConsecutive++;
                    maxConsecutiveRows = Math.Max(maxConsecutiveRows, currentConsecutive);
                }
                else currentConsecutive = 0;
            }

            currentConsecutive = 0;
            for (int c = 0; c < cols; c++)
            {
                colsFull[c] = true;
                for (int r = 0; r < rows; r++)
                {
                    if (!grid[r * cols + c].IsOccupied) { colsFull[c] = false; break; }
                }

                if (colsFull[c])
                {
                    completedCols++;
                    currentConsecutive++;
                    maxConsecutiveCols = Math.Max(maxConsecutiveCols, currentConsecutive);
                }
                else currentConsecutive = 0;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (rowsFull[r] && colsFull[c]) intersections++;
                }
            }

            if (completedRows == 0 && completedCols == 0) return 0;

            int rowBasePoints = completedRows * rowBonus;
            int colBasePoints = completedCols * colBonus;
            int intersectionPoints = intersections * intersectionBonus;

            float multRow = 0f;
            if (completedRows > 0)
            {
                int rIndex = Math.Min(maxConsecutiveRows - 1, rowMultipliers.Length - 1);
                multRow = rowMultipliers[rIndex];
            }

            float multCol = 0f;
            if (completedCols > 0)
            {
                int cIndex = Math.Min(maxConsecutiveCols - 1, colMultipliers.Length - 1);
                multCol = colMultipliers[cIndex];
            }

            float totalMultiplier = multRow + multCol;
            if (totalMultiplier == 0f) totalMultiplier = 1f;

            int lineScoreWithMultiplier = (int)Math.Floor((rowBasePoints + colBasePoints) * totalMultiplier);
            int currentTotalStructureScore = lineScoreWithMultiplier + intersectionPoints;
            int newPointsToEarn = currentTotalStructureScore - player.AccumulatedStructurePoints;

            player.AccumulatedStructurePoints = currentTotalStructureScore;

            return newPointsToEarn;
        }

        public static int Count3x3Contacts(CellStateDTO[] grid, int rows, int cols, int r, int c, int valorDado, out int contactosDiagonales)
        {
            int contactosTotales = 0;
            contactosDiagonales = 0;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;

                    int nr = r + i;
                    int nc = c + j;

                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        var vecino = grid[nr * cols + nc];
                        if (vecino.IsOccupied && vecino.Value == valorDado)
                        {
                            contactosTotales++;
                            if (i != 0 && j != 0) contactosDiagonales++;
                        }
                    }
                }
            }
            return contactosTotales;
        }

        public static int GetOrthogonalConnections(CellStateDTO[] grid, int rows, int cols, int r, int c, DieColor color, int groupId)
        {
            int conexiones = 0;
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };

            int centerIdx = r * cols + c;
            int centerValue = grid[centerIdx].Value;

            for (int d = 0; d < 4; d++)
            {
                int nr = r + dr[d];
                int nc = c + dc[d];
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    var vecino = grid[nr * cols + nc];
                    if (vecino.IsOccupied && vecino.Color == color && vecino.Value == centerValue && vecino.GroupId != groupId)
                    {
                        conexiones++;
                    }
                }
            }
            return conexiones;
        }

        public static int GetDiagonalConnections(CellStateDTO[] grid, int rows, int cols, int r, int c, DieColor color, int groupId)
        {
            int conexiones = 0;
            int[] dr = { -1, -1, 1, 1 };
            int[] dc = { -1, 1, -1, 1 };

            int centerIdx = r * cols + c;
            int centerValue = grid[centerIdx].Value;

            for (int d = 0; d < 4; d++)
            {
                int nr = r + dr[d];
                int nc = c + dc[d];
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    var vecino = grid[nr * cols + nc];
                    if (vecino.IsOccupied && vecino.Color == color && vecino.Value == centerValue && vecino.GroupId != groupId)
                    {
                        conexiones++;
                    }
                }
            }
            return conexiones;
        }

        public static int GetOnesPenalties(CellStateDTO[] grid, int rows, int cols)
        {
            int penalties = 0;
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (grid[idx].IsOccupied && grid[idx].Value == 1)
                    {
                        bool hasContact = false;
                        for (int d = 0; d < 4; d++)
                        {
                            int nr = r + dr[d];
                            int nc = c + dc[d];
                            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                            {
                                int nIdx = nr * cols + nc;
                                if (grid[nIdx].IsOccupied && grid[nIdx].Color == grid[idx].Color)
                                {
                                    hasContact = true;
                                    break;
                                }
                            }
                        }
                        if (hasContact) penalties++;
                    }
                }
            }
            return penalties;
        }
    }
}