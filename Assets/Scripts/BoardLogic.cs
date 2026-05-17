using System.Collections.Generic;
using UnityEngine;

public class BoardLogic : MonoBehaviour
{
    // Simulación de tu estructura de datos del tablero
    // Vector2Int representa la coordenada X, Y en el tablero
    public HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    public Dictionary<Vector2Int, string> diceColors = new Dictionary<Vector2Int, string>();

    /// <summary>
    /// Valida si mover un dado viola la regla de mantener una sola isla cohesionada.
    /// </summary>
    public bool WouldLeaveSplitIslands(Vector2Int cellToRemove)
    {
        if (!occupiedCells.Contains(cellToRemove)) return false;

        // Clonamos el estado actual simulando la remoción
        HashSet<Vector2Int> simulatedBoard = new HashSet<Vector2Int>(occupiedCells);
        simulatedBoard.Remove(cellToRemove);

        if (simulatedBoard.Count <= 1) return false; // Un solo dado o ninguno siempre es conexo

        // Tomamos un punto de partida cualquiera para el BFS
        Vector2Int startPoint = Vector2Int.zero;
        foreach (var cell in simulatedBoard)
        {
            startPoint = cell;
            break;
        }

        // Algoritmo BFS (Breadth-First Search) de alto rendimiento
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(startPoint);
        visited.Add(startPoint);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

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

        // Si la cantidad de nodos visitados es igual al tamaño del tablero simulado,
        // significa que todo sigue conectado en una sola isla.
        return visited.Count != simulatedBoard.Count;
    }
}