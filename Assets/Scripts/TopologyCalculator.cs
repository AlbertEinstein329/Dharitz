using System.Collections.Generic;
using UnityEngine;

public static class TopologyCalculator
{
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
        if (size == 1) return -500;
        if (size == 2) return -700;
        if (size == 3) return -1000;

        return -(250 + ((size - 1) * 100));
    }

    public static bool ValidateSurvival(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int newGroupId, int newTargetSize, PlayerData player, VariantData variant)
    {
        bool tableroVacio = true;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (logic[i, j] != null) { tableroVacio = false; break; }
            }
            if (!tableroVacio) break;
        }

        if (tableroVacio) return true;

        Dictionary<int, int> dadosFaltantes = new Dictionary<int, int>();
        foreach (var group in player.activeGroups.Values)
        {
            if (group != null && !group.isClosed)
            {
                dadosFaltantes[group.id] = group.targetSize - group.occupiedCells.Count;
            }
        }

        logic[r, c] = new GridManager.DieData(color, newGroupId, newTargetSize);

        if (dadosFaltantes.ContainsKey(newGroupId))
        {
            dadosFaltantes[newGroupId] -= 1;
            if (dadosFaltantes[newGroupId] <= 0) dadosFaltantes.Remove(newGroupId);
        }
        else
        {
            if (newTargetSize - 1 > 0)
            {
                dadosFaltantes[newGroupId] = newTargetSize - 1;
            }
        }

        bool esValido = AnalyzeTopology(logic, rows, cols, dadosFaltantes, player, variant);

        logic[r, c] = null;

        return esValido;
    }

    private static bool AnalyzeTopology(GridManager.DieData[,] logic, int rows, int cols, Dictionary<int, int> dadosFaltantes, PlayerData player, VariantData variant)
    {
        int[] dr = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] dc = { 0, 0, -1, 1, -1, 1, -1, 1 };

        foreach (var kvp in dadosFaltantes)
        {
            int gId = kvp.Key;
            int requeridos = kvp.Value;

            if (requeridos <= 0) continue;

            GroupData grupoActivo = null;
            foreach (var g in player.activeGroups.Values) { if (g != null && g.id == gId) { grupoActivo = g; break; } }

            int sizeDelPatron = (grupoActivo != null) ? grupoActivo.targetSize : (requeridos + 1);

            PatternData patronDelGrupo = variant.GetPattern(sizeDelPatron);

            if (patronDelGrupo == null)
            {
                Debug.LogError($"CRÍTICO: El patrón {sizeDelPatron} NO EXISTE en la Variante '{variant.variantName}'.");
                return false;
            }

            bool puedeReservarDiagonal = patronDelGrupo.allowDiagonalReservation;
            int direccionesDeBusqueda = puedeReservarDiagonal ? 8 : 4;

            int vaciosAlcanzables = 0;
            bool[,] visitado = new bool[rows, cols];
            Queue<Vector2Int> cola = new Queue<Vector2Int>();

            DieColor colorDelGrupo = (grupoActivo != null) ? grupoActivo.color : DieColor.White;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (logic[r, c] != null && logic[r, c].groupId == gId)
                    {
                        colorDelGrupo = logic[r, c].color;
                        cola.Enqueue(new Vector2Int(r, c));
                        visitado[r, c] = true;
                    }
                }
            }

            while (cola.Count > 0)
            {
                Vector2Int actual = cola.Dequeue();

                for (int d = 0; d < direccionesDeBusqueda; d++)
                {
                    int nr = actual.x + dr[d];
                    int nc = actual.y + dc[d];

                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        if (logic[nr, nc] == null && !visitado[nr, nc])
                        {
                            bool esZonaMuerta = false;
                            for (int d2 = 0; d2 < 4; d2++)
                            {
                                int nnr = nr + dr[d2];
                                int nnc = nc + dc[d2];

                                if (nnr >= 0 && nnr < rows && nnc >= 0 && nnc < cols)
                                {
                                    GridManager.DieData vecinoDelVacio = logic[nnr, nnc];
                                    if (vecinoDelVacio != null && vecinoDelVacio.color == colorDelGrupo && vecinoDelVacio.groupId != gId)
                                    {
                                        esZonaMuerta = true;
                                        break;
                                    }
                                }
                            }

                            if (!esZonaMuerta)
                            {
                                visitado[nr, nc] = true;
                                vaciosAlcanzables++;
                                cola.Enqueue(new Vector2Int(nr, nc));
                            }
                        }
                    }
                }
            }

            if (vaciosAlcanzables < requeridos)
            {
                Debug.Log($"Bloqueo Topológico: El grupo {gId} necesita {requeridos} espacios, pero solo alcanza {vaciosAlcanzables}.");
                return false;
            }
        }

        return true;
    }

    public static bool IsValidMoveExtraction(GridManager.DieData[,] logic, int rows, int cols, int targetR, int targetC, int groupId, PlayerData player)
    {
        // Find the active group
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

        // If the group only has 1 die, removing it doesn't partition anything
        if (group.occupiedCells.Count <= 1) return true;

        // Find a starting point for BFS (any cell in the group that is NOT the one being removed)
        Vector2Int startNode = new Vector2Int(-1, -1);
        foreach (var cell in group.occupiedCells)
        {
            if (cell.x != targetR || cell.y != targetC)
            {
                startNode = cell;
                break;
            }
        }

        if (startNode.x == -1) return false; // Should not happen

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

            // Check adjacent cells (ortogonal AND diagonal, if diagonal connection is allowed for the group's pattern)
            // Wait, Dharitz pattern logic implies cells in a group touch orthogonally, unless it's a specific variant.
            // Let's assume standard adjacency check. To be safe, we check all 8 directions if diagonal is allowed.
            // But we don't have pattern data easily available here unless passed. Let's just check the group list.
            
            // To be purely precise to the group list:
            for (int d = 0; d < 8; d++)
            {
                int nr = current.x + dr[d];
                int nc = current.y + dc[d];

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    // Skip the cell we are simulating removal for
                    if (nr == targetR && nc == targetC) continue;

                    // If it's part of the group and not visited
                    if (!visited[nr, nc] && logic[nr, nc] != null && logic[nr, nc].groupId == groupId)
                    {
                        // Wait, do they need to be adjacent to be considered connected in this group?
                        // Yes, the game enforces groups to be contiguous.
                        visited[nr, nc] = true;
                        connectedCount++;
                        queue.Enqueue(new Vector2Int(nr, nc));
                    }
                }
            }
        }

        // Si la cantidad de dados conectados (excluyendo el que quitamos) es igual al total menos 1, el grupo sigue íntegro.
        return connectedCount == (group.occupiedCells.Count - 1);
    }
}
