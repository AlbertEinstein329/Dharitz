using System.Collections.Generic;
using UnityEngine;

public static class TopologyCalculator
{
    // =========================================================================
    // 1. ESTRUCTURAS Y MOTOR DE SUPERVIVENCIA 
    // =========================================================================
    private class GroupDemand
    {
        public DieColor Color;
        public int GroupId;
        public int Demand;
        public List<Vector2Int> StartCells;
        public HashSet<Vector2Int> ReachableSet;
        public bool AllowDiagonal;

        public GroupDemand(DieColor color, int groupId, int demand, List<Vector2Int> startCells, bool allowDiagonal)
        {
            Color = color;
            GroupId = groupId;
            Demand = demand;
            StartCells = startCells;
            ReachableSet = new HashSet<Vector2Int>();
            AllowDiagonal = allowDiagonal;
        }
    }

    public static bool ValidateSurvival(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int newGroupId, int newTargetSize, PlayerData player, VariantData variant)
    {
        GridManager.DieData[,] simLogic = (GridManager.DieData[,])logic.Clone();
        simLogic[r, c] = new GridManager.DieData(color, newGroupId, newTargetSize);

        List<GroupDemand> demands = new List<GroupDemand>();
        bool currentGroupProcessed = false;

        foreach (var kvp in player.activeGroups)
        {
            DieColor grpColor = kvp.Key;
            GroupData grp = kvp.Value;

            // =========================================================
            // EL ESCUDO CONTRA EL ERROR DEL DADO 1:
            // Si el juego vació el grupo (null) o ya está cerrado, lo ignoramos.
            // =========================================================
            if (grp == null || grp.isClosed) continue;

            int demand = grp.targetSize - grp.occupiedCells.Count;
            List<Vector2Int> startCells = new List<Vector2Int>(grp.occupiedCells);

            // Blindaje de Variante
            bool canDiag = false;
            if (variant != null)
            {
                PatternData pData = variant.GetPattern(grp.targetSize);
                if (pData != null) canDiag = pData.allowDiagonalReservation;
            }

            if (grpColor == color && grp.id == newGroupId)
            {
                demand -= 1;
                startCells.Add(new Vector2Int(r, c));
                currentGroupProcessed = true;
            }

            if (demand > 0)
            {
                demands.Add(new GroupDemand(grpColor, grp.id, demand, startCells, canDiag));
            }
        }

        if (!currentGroupProcessed && (newTargetSize - 1) > 0)
        {
            bool newCanDiag = false;
            if (variant != null)
            {
                PatternData newPat = variant.GetPattern(newTargetSize);
                if (newPat != null) newCanDiag = newPat.allowDiagonalReservation;
            }

            demands.Add(new GroupDemand(color, newGroupId, newTargetSize - 1, new List<Vector2Int> { new Vector2Int(r, c) }, newCanDiag));
        }

        if (demands.Count == 0) return true;

        foreach (var group in demands)
        {
            group.ReachableSet = GetReachableCells(simLogic, rows, cols, group.Color, group.GroupId, group.StartCells, group.AllowDiagonal);
            if (group.ReachableSet.Count < group.Demand) return false;
        }

        int n = demands.Count;
        int maxCombinations = 1 << n;

        for (int i = 1; i < maxCombinations; i++)
        {
            int totalDemand = 0;
            HashSet<Vector2Int> unionReachable = new HashSet<Vector2Int>();

            for (int j = 0; j < n; j++)
            {
                if ((i & (1 << j)) != 0)
                {
                    totalDemand += demands[j].Demand;
                    unionReachable.UnionWith(demands[j].ReachableSet);
                }
            }

            if (unionReachable.Count < totalDemand) return false;
        }

        return true;
    }

    private static HashSet<Vector2Int> GetReachableCells(GridManager.DieData[,] grid, int rows, int cols, DieColor gColor, int gId, List<Vector2Int> startCells, bool allowDiagonal)
    {
        HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        bool[,] visited = new bool[rows, cols];

        foreach (var cell in startCells)
        {
            queue.Enqueue(cell);
            visited[cell.x, cell.y] = true;
        }

        Vector2Int[] orthoDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int[] diagDirs = { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };

        List<Vector2Int> validDirs = new List<Vector2Int>(orthoDirs);
        if (allowDiagonal) validDirs.AddRange(diagDirs);

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();

            foreach (var dir in validDirs)
            {
                int nr = curr.x + dir.x;
                int nc = curr.y + dir.y;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    if (!visited[nr, nc] && grid[nr, nc] == null)
                    {
                        bool isDeadZone = false;
                        foreach (var od in orthoDirs)
                        {
                            int or = nr + od.x;
                            int oc = nc + od.y;

                            if (or >= 0 && or < rows && oc >= 0 && oc < cols)
                            {
                                var neighbor = grid[or, oc];
                                if (neighbor != null && neighbor.color == gColor && neighbor.groupId != gId)
                                {
                                    isDeadZone = true;
                                    break;
                                }
                            }
                        }

                        if (!isDeadZone)
                        {
                            visited[nr, nc] = true;
                            reachable.Add(new Vector2Int(nr, nc));
                            queue.Enqueue(new Vector2Int(nr, nc));
                        }
                    }
                }
            }
        }
        return reachable;
    }

    // =========================================================================
    // 2. MÉTODOS ORIGINALES
    // =========================================================================
    public static List<List<Vector2Int>> FindEnclosedGaps(GridManager.DieData[,] logic, int rows, int cols)
    {
        bool[,] visited = new bool[rows, cols];
        var enclosedGaps = new List<List<Vector2Int>>();

        Vector2Int[] orthogonalDirections = new Vector2Int[] {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (logic[r, c] == null && !visited[r, c])
                {
                    bool isEnclosed = true;
                    var gapCells = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();

                    queue.Enqueue(new Vector2Int(r, c));
                    visited[r, c] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        gapCells.Add(current);

                        foreach (Vector2Int dir in orthogonalDirections)
                        {
                            int nextR = current.x + dir.x;
                            int nextC = current.y + dir.y;

                            if (nextR < 0 || nextR >= rows || nextC < 0 || nextC >= cols)
                            {
                                isEnclosed = false;
                            }
                            else if (logic[nextR, nextC] == null && !visited[nextR, nextC])
                            {
                                visited[nextR, nextC] = true;
                                queue.Enqueue(new Vector2Int(nextR, nextC));
                            }
                        }
                    }

                    if (isEnclosed && gapCells.Count > 0)
                    {
                        enclosedGaps.Add(gapCells);
                    }
                }
            }
        }

        return enclosedGaps;
    }

    public static int CalculateGapPenalty(GridManager.DieData[,] logic, int rows, int cols, out List<List<Vector2Int>> enclosedGapsOut)
    {
        int totalPenalty = 0;
        enclosedGapsOut = FindEnclosedGaps(logic, rows, cols);

        foreach (var gapCells in enclosedGapsOut)
        {
            totalPenalty += GetPenaltyForGapSize(gapCells.Count);
        }

        return totalPenalty;
    }

    public static int GetPenaltyForGapSize(int size)
    {
        if (size <= 0) return 0;

        // Si es 1 solo Gap aislado: 1 * -500 = -500 en total
        if (size == 1) return size * -500;

        // Si son 2 Gaps juntos: 2 * -700 = -1400 en total
        if (size == 2) return size * -700;

        // Si son 3 o más Gaps juntos: tamaño * -1000
        // (Ej: 3 gaps = -3000, 5 gaps = -5000)
        if (size >= 3) return size * -1000;

        return 0;
    }
    public static bool IsValidMoveExtraction(GridManager.DieData[,] logic, int rows, int cols, int targetR, int targetC, int groupId, PlayerData player)
    {
        GroupData group = null;
        foreach (var g in player.activeGroups.Values)
        {
            if (g != null && g.id == groupId)
            {
                group = g;
                break;
            }
        }

        if (group == null || group.isClosed) return false;
        if (group.occupiedCells.Count <= 1) return true;

        Vector2Int startNode = new Vector2Int(-1, -1);
        foreach (var cell in group.occupiedCells)
        {
            if (cell.x != targetR || cell.y != targetC)
            {
                startNode = cell;
                break;
            }
        }

        if (startNode.x == -1) return false;

        int[] dr = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] dc = { 0, 0, -1, 1, -1, 1, -1, 1 };

        bool[,] visited = new bool[rows, cols];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(startNode);
        visited[startNode.x, startNode.y] = true;

        int connectedCount = 1;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int d = 0; d < 8; d++)
            {
                int nr = current.x + dr[d];
                int nc = current.y + dc[d];

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    if (nr == targetR && nc == targetC) continue;

                    if (!visited[nr, nc] && logic[nr, nc] != null && logic[nr, nc].groupId == groupId)
                    {
                        visited[nr, nc] = true;
                        connectedCount++;
                        queue.Enqueue(new Vector2Int(nr, nc));
                    }
                }
            }
        }

        return connectedCount == (group.occupiedCells.Count - 1);
    }
}