using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    public static class CoreTopologyCalculator
    {
        private class GroupDemand
        {
            public DieColor Color;
            public int GroupId;
            public int Demand;
            public List<GridPos> StartCells;
            public HashSet<GridPos> ReachableSet;
            public bool AllowDiagonal;

            public GroupDemand(DieColor color, int groupId, int demand, List<GridPos> startCells, bool allowDiagonal)
            {
                Color = color;
                GroupId = groupId;
                Demand = demand;
                StartCells = startCells;
                ReachableSet = new HashSet<GridPos>();
                AllowDiagonal = allowDiagonal;
            }
        }

        // Ejecución en Capa 0: Recibe exclusivamente DTOs sin clonar la matriz 2D visual
        public static bool ValidateSurvival(BoardStateDTO board, int r, int c, DieColor color, int newGroupId, int newTargetSize, PlayerDataDTO player, PatternDefDTO variantPattern, PatternDefDTO newPattern)
        {
            // 1. Clonación ultraligera del array plano (Value Types = 0 Heap Allocation por elemento)
            CellStateDTO[] simCells = new CellStateDTO[board.Cells.Length];
            Array.Copy(board.Cells, simCells, board.Cells.Length);

            int simIndex = board.GetIndex(r, c);
            simCells[simIndex] = new CellStateDTO { IsOccupied = true, Color = color, GroupId = newGroupId, Value = newTargetSize };

            List<GroupDemand> demands = new List<GroupDemand>();
            bool currentGroupProcessed = false;

            foreach (var kvp in player.ActiveGroups)
            {
                DieColor grpColor = kvp.Key;
                GroupDataDTO grp = kvp.Value;

                if (grp == null || grp.IsClosed) continue;

                int demand = grp.TargetSize - grp.OccupiedCells.Count;
                List<GridPos> startCells = new List<GridPos>(grp.OccupiedCells);

                bool canDiag = variantPattern != null && variantPattern.AllowDiagonalReservation;

                if (grpColor == color && grp.Id == newGroupId)
                {
                    demand -= 1;
                    startCells.Add(new GridPos(r, c));
                    currentGroupProcessed = true;
                }

                if (demand > 0)
                {
                    demands.Add(new GroupDemand(grpColor, grp.Id, demand, startCells, canDiag));
                }
            }

            if (!currentGroupProcessed && (newTargetSize - 1) > 0)
            {
                bool newCanDiag = newPattern != null && newPattern.AllowDiagonalReservation;
                demands.Add(new GroupDemand(color, newGroupId, newTargetSize - 1, new List<GridPos> { new GridPos(r, c) }, newCanDiag));
            }

            if (demands.Count == 0) return true;

            foreach (var group in demands)
            {
                group.ReachableSet = GetReachableCells(simCells, board.Rows, board.Cols, group.Color, group.GroupId, group.StartCells, group.AllowDiagonal);
                if (group.ReachableSet.Count < group.Demand) return false;
            }

            int n = demands.Count;
            int maxCombinations = 1 << n;

            for (int i = 1; i < maxCombinations; i++)
            {
                int totalDemand = 0;
                HashSet<GridPos> unionReachable = new HashSet<GridPos>();

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

        private static HashSet<GridPos> GetReachableCells(CellStateDTO[] grid, int rows, int cols, DieColor gColor, int gId, List<GridPos> startCells, bool allowDiagonal)
        {
            HashSet<GridPos> reachable = new HashSet<GridPos>();
            Queue<GridPos> queue = new Queue<GridPos>();

            // Array plano unidimensional para rastreo
            bool[] visited = new bool[rows * cols];

            foreach (var cell in startCells)
            {
                queue.Enqueue(cell);
                visited[cell.X * cols + cell.Y] = true;
            }

            GridPos[] orthoDirs = { new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, -1), new GridPos(0, 1) };
            GridPos[] diagDirs = { new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1) };

            List<GridPos> validDirs = new List<GridPos>(orthoDirs);
            if (allowDiagonal) validDirs.AddRange(diagDirs);

            while (queue.Count > 0)
            {
                GridPos curr = queue.Dequeue();

                foreach (var dir in validDirs)
                {
                    int nr = curr.X + dir.X;
                    int nc = curr.Y + dir.Y;

                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        int nIdx = nr * cols + nc;
                        if (!visited[nIdx] && !grid[nIdx].IsOccupied)
                        {
                            bool isDeadZone = false;
                            foreach (var od in orthoDirs)
                            {
                                int or = nr + od.X;
                                int oc = nc + od.Y;

                                if (or >= 0 && or < rows && oc >= 0 && oc < cols)
                                {
                                    int oIdx = or * cols + oc;
                                    var neighbor = grid[oIdx];
                                    if (neighbor.IsOccupied && neighbor.Color == gColor && neighbor.GroupId != gId)
                                    {
                                        isDeadZone = true;
                                        break;
                                    }
                                }
                            }

                            if (!isDeadZone)
                            {
                                visited[nIdx] = true;
                                GridPos validPos = new GridPos(nr, nc);
                                reachable.Add(validPos);
                                queue.Enqueue(validPos);
                            }
                        }
                    }
                }
            }
            return reachable;
        }

        public static List<List<GridPos>> FindEnclosedGaps(CellStateDTO[] grid, int rows, int cols)
        {
            bool[] visited = new bool[rows * cols];
            var enclosedGaps = new List<List<GridPos>>();

            GridPos[] orthogonalDirections = { new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, -1), new GridPos(0, 1) };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (!grid[idx].IsOccupied && !visited[idx])
                    {
                        bool isEnclosed = true;
                        var gapCells = new List<GridPos>();
                        Queue<GridPos> queue = new Queue<GridPos>();

                        queue.Enqueue(new GridPos(r, c));
                        visited[idx] = true;

                        while (queue.Count > 0)
                        {
                            GridPos current = queue.Dequeue();
                            gapCells.Add(current);

                            foreach (GridPos dir in orthogonalDirections)
                            {
                                int nextR = current.X + dir.X;
                                int nextC = current.Y + dir.Y;

                                if (nextR < 0 || nextR >= rows || nextC < 0 || nextC >= cols)
                                {
                                    isEnclosed = false;
                                }
                                else
                                {
                                    int nextIdx = nextR * cols + nextC;
                                    if (!grid[nextIdx].IsOccupied && !visited[nextIdx])
                                    {
                                        visited[nextIdx] = true;
                                        queue.Enqueue(new GridPos(nextR, nextC));
                                    }
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

        public static int CalculateGapPenalty(CellStateDTO[] grid, int rows, int cols, out List<List<GridPos>> enclosedGapsOut)
        {
            int totalPenalty = 0;
            enclosedGapsOut = FindEnclosedGaps(grid, rows, cols);

            foreach (var gapCells in enclosedGapsOut)
            {
                totalPenalty += GetPenaltyForGapSize(gapCells.Count);
            }

            return totalPenalty;
        }

        public static int GetPenaltyForGapSize(int size)
        {
            if (size <= 0) return 0;
            if (size == 1) return size * -500;
            if (size == 2) return size * -700;
            if (size >= 3) return size * -1000;
            return 0;
        }

        public static bool IsValidMoveExtraction(CellStateDTO[] grid, int rows, int cols, int targetR, int targetC, int groupId, PlayerDataDTO player)
        {
            GroupDataDTO group = null;
            foreach (var g in player.ActiveGroups.Values)
            {
                if (g != null && g.Id == groupId)
                {
                    group = g;
                    break;
                }
            }

            if (group == null || group.IsClosed) return false;
            if (group.OccupiedCells.Count <= 1) return true;

            GridPos startNode = new GridPos(-1, -1);
            foreach (var cell in group.OccupiedCells)
            {
                if (cell.X != targetR || cell.Y != targetC)
                {
                    startNode = cell;
                    break;
                }
            }

            if (startNode.X == -1) return false;

            int[] dr = { -1, 1, 0, 0, -1, -1, 1, 1 };
            int[] dc = { 0, 0, -1, 1, -1, 1, -1, 1 };

            bool[] visited = new bool[rows * cols];
            Queue<GridPos> queue = new Queue<GridPos>();

            queue.Enqueue(startNode);
            visited[startNode.X * cols + startNode.Y] = true;

            int connectedCount = 1;

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();

                for (int d = 0; d < 8; d++)
                {
                    int nr = current.X + dr[d];
                    int nc = current.Y + dc[d];

                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        if (nr == targetR && nc == targetC) continue;

                        int nIdx = nr * cols + nc;
                        if (!visited[nIdx] && grid[nIdx].IsOccupied && grid[nIdx].GroupId == groupId)
                        {
                            visited[nIdx] = true;
                            connectedCount++;
                            queue.Enqueue(new GridPos(nr, nc));
                        }
                    }
                }
            }

            return connectedCount == (group.OccupiedCells.Count - 1);
        }
    }
}