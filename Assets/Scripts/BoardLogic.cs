using System.Collections.Generic;
using UnityEngine;

public class BoardLogic : MonoBehaviour
{
    // Simulación de tu estructura de datos del tablero
    // Vector2Int representa la coordenada X, Y en el tablero
    public HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    public Dictionary<Vector2Int, string> diceColors = new Dictionary<Vector2Int, string>();

    /// <summary>
    /// Valida si mover un dado de un origen a un destino rompe la cohesión del tablero (1 sola isla).
    /// </summary>
    public bool WouldLeaveSplitIslands(Vector2Int origin, Vector2Int destination)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return false;

        // 1. RECONSTRUCCIÓN DE LA VERDAD ABSOLUTA
        // Leemos la matriz exacta para ignorar cualquier dado fantasma en la memoria.
        var logic = gm.gridManager.allBoardsLogic[gm.CurrentPlayerIndex];
        HashSet<Vector2Int> realOccupied = new HashSet<Vector2Int>();

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                if (logic[r, c] != null) realOccupied.Add(new Vector2Int(c, r));
            }
        }

        if (!realOccupied.Contains(origin)) return false;

        // 2. Simulación de movimiento
        HashSet<Vector2Int> simulatedBoard = new HashSet<Vector2Int>(realOccupied);
        simulatedBoard.Remove(origin);
        simulatedBoard.Add(destination);

        if (simulatedBoard.Count <= 1) return false;

        // 3. Empezar el escáner desde el destino garantiza tocar la isla principal
        Vector2Int startPoint = destination;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(startPoint);
        visited.Add(startPoint);

        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        // Algoritmo BFS
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;
                if (simulatedBoard.Contains(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Si hay una discrepancia, la isla se rompió.
        return visited.Count < simulatedBoard.Count;
    }
}