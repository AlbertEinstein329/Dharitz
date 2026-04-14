using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class PatternValidator
{

    // Diccionario con las formas base según el GDD [cite: 32]
    private static readonly Dictionary<int, List<Vector2Int>> Patterns = new Dictionary<int, List<Vector2Int>> {
        { 2, new List<Vector2Int> { new Vector2Int(0,0), new Vector2Int(1,1) } },
        { 3, new List<Vector2Int> { new Vector2Int(0,0), new Vector2Int(1,1), new Vector2Int(1,0) } },
        { 4, new List<Vector2Int> { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(1,2) } },
        { 5, new List<Vector2Int> { new Vector2Int(0,1), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(1,2), new Vector2Int(2,1) } },
        { 6, new List<Vector2Int> { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0), new Vector2Int(0,1), new Vector2Int(3,1) } }
    };

    public static bool CheckPattern(List<Vector2Int> groupCells, int targetNumber)
    {
        // El número 1 tiene una regla de aislamiento, no una forma geométrica fija [cite: 28, 32]
        if (targetNumber == 1) return true;
        if (!Patterns.ContainsKey(targetNumber)) return false;

        List<Vector2Int> targetPattern = Patterns[targetNumber];

        // Comprobamos la forma en sus 4 posibles rotaciones [cite: 34]
        for (int i = 0; i < 4; i++)
        {
            if (AreShapesEqual(groupCells, targetPattern)) return true;
            targetPattern = RotatePattern(targetPattern);
        }

        return false;
    }

    private static bool AreShapesEqual(List<Vector2Int> shapeA, List<Vector2Int> shapeB)
    {
        if (shapeA.Count != shapeB.Count) return false;

        var normA = Normalize(shapeA);
        var normB = Normalize(shapeB);

        // Verifica si todas las coordenadas normalizadas coinciden
        return normA.All(a => normB.Any(b => b.x == a.x && b.y == a.y));
    }

    private static List<Vector2Int> Normalize(List<Vector2Int> points)
    {
        int minX = points.Min(p => p.x);
        int minY = points.Min(p => p.y);
        return points.Select(p => new Vector2Int(p.x - minX, p.y - minY)).ToList();
    }

    private static List<Vector2Int> RotatePattern(List<Vector2Int> points)
    {
        return points.Select(p => new Vector2Int(-p.y, p.x)).ToList();
    }
}

