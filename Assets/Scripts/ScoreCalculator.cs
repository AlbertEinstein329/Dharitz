using UnityEngine;

public static class ScoreCalculator
{
    public static int EvaluateAndApplyCombos(GridManager.DieData[,] logic, int rows, int cols, PlayerData player)
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
            for (int c = 0; c < cols; c++) if (logic[r, c] == null) { rowsFull[r] = false; break; }

            if (rowsFull[r])
            {
                completedRows++;
                currentConsecutive++;
                maxConsecutiveRows = Mathf.Max(maxConsecutiveRows, currentConsecutive);
            }
            else currentConsecutive = 0;
        }

        currentConsecutive = 0;
        for (int c = 0; c < cols; c++)
        {
            colsFull[c] = true;
            for (int r = 0; r < rows; r++) if (logic[r, c] == null) { colsFull[c] = false; break; }

            if (colsFull[c])
            {
                completedCols++;
                currentConsecutive++;
                maxConsecutiveCols = Mathf.Max(maxConsecutiveCols, currentConsecutive);
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

        int rowBasePoints = completedRows * ScoreManager.ROW_COMPLETE_BONUS;
        int colBasePoints = completedCols * ScoreManager.COL_COMPLETE_BONUS;

        int intersectionPoints = intersections * ScoreManager.INTERSECTION_BONUS;

        float multRow = completedRows > 0 ? ScoreManager.Instance.GetConsecutiveRowMultiplier(maxConsecutiveRows) : 0f;
        float multCol = completedCols > 0 ? ScoreManager.Instance.GetConsecutiveColMultiplier(maxConsecutiveCols) : 0f;

        float totalMultiplier = multRow + multCol;
        if (totalMultiplier == 0f) totalMultiplier = 1f;

        int lineScoreWithMultiplier = Mathf.FloorToInt((rowBasePoints + colBasePoints) * totalMultiplier);

        int currentTotalStructureScore = lineScoreWithMultiplier + intersectionPoints;

        int newPointsToEarn = currentTotalStructureScore - player.accumulatedStructurePoints;

        player.accumulatedStructurePoints = currentTotalStructureScore;

        return newPointsToEarn;
    }

    public static int Count3x3Contacts(GridManager.DieData[,] logic, int rows, int cols, int r, int c, int valorDado, out int contactosDiagonales)
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
                    GridManager.DieData vecino = logic[nr, nc];

                    if (vecino != null && vecino.value == valorDado)
                    {
                        contactosTotales++;

                        if (i != 0 && j != 0)
                        {
                            contactosDiagonales++;
                        }
                    }
                }
            }
        }
        return contactosTotales;
    }

    public static int ScanNewDiagonalConnections(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int groupId)
    {
        int conexionesNuevas = 0;

        int[] dr = { -1, -1, 1, 1 };
        int[] dc = { -1, 1, -1, 1 };

        for (int d = 0; d < 4; d++)
        {
            int nr = r + dr[d];
            int nc = c + dc[d];
            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
            {
                GridManager.DieData vecino = logic[nr, nc];
                if (vecino != null && vecino.color == color && vecino.value == logic[r, c].value && vecino.groupId != groupId)
                {
                    conexionesNuevas++;
                }
            }
        }
        return conexionesNuevas;
    }

    public static int GetOnesPenalties(GridManager.DieData[,] logic, int rows, int cols)
    {
        int penalties = 0;
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (logic[r, c] != null && logic[r, c].value == 1)
                {
                    bool hasContact = false;
                    for (int d = 0; d < 4; d++)
                    {
                        int nr = r + dr[d];
                        int nc = c + dc[d];
                        if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                        {
                            if (logic[nr, nc] != null && logic[nr, nc].color == logic[r, c].color)
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
