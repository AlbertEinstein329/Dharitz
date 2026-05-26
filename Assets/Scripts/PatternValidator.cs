using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class PatternValidator
{
    public static bool CheckPattern(List<Vector2Int> groupCells, PatternData patternData)
    {
        // El número 1 es un caso especial geométrico (1 sola celda siempre coincide consigo misma)
        if (patternData.targetNumber == 1) return true;

        if (patternData.baseShape == null || patternData.baseShape.Count == 0)
        {
            Debug.LogError($"Error: El patrón para el número {patternData.targetNumber} no tiene forma base asignada.");
            return false;
        }

        // ==========================================
        // SOLUCIÓN AL EJE INVERTIDO (X/Y SWAP)
        // ==========================================
        // Transponemos las celdas del tablero (Row, Col) para que coincidan 
        // con la orientación visual Cartesiana (X, Y) de la UI y del Inspector.
        List<Vector2Int> alignedGroupCells = TransponerEjes(groupCells);
        List<Vector2Int> targetPattern = patternData.baseShape;

        // 1. Intentar hacer coincidir con la forma original (y sus rotaciones si están permitidas)
        if (EvaluarRotaciones(alignedGroupCells, targetPattern, patternData.allowRotation))
            return true;

        // 2. Si falló y el espejo está permitido, invertimos la figura y probamos de nuevo
        if (patternData.allowMirror)
        {
            List<Vector2Int> mirroredPattern = EspejarPatron(targetPattern);
            if (EvaluarRotaciones(alignedGroupCells, mirroredPattern, patternData.allowRotation))
                return true;
        }

        return false;
    }

    // --- NUEVO MÉTODO PARA ALINEAR COORDENADAS ---
    private static List<Vector2Int> TransponerEjes(List<Vector2Int> points)
    {
        // Intercambiamos X por Y para convertir (Fila, Columna) en (X, Y)
        return points.Select(p => new Vector2Int(p.y, p.x)).ToList();
    }

    private static bool EvaluarRotaciones(List<Vector2Int> cells, List<Vector2Int> target, bool allowRotation)
    {
        // Si no permite rotación, el bucle solo corre 1 vez. Si permite, corre 4 veces (0°, 90°, 180°, 270°).
        int ciclos = allowRotation ? 4 : 1;
        List<Vector2Int> currentTarget = target;

        for (int i = 0; i < ciclos; i++)
        {
            if (AreShapesEqual(cells, currentTarget)) return true;
            currentTarget = RotarPatron(currentTarget); // Rota 90 grados para la siguiente iteración
        }

        return false;
    }

    private static bool AreShapesEqual(List<Vector2Int> shapeA, List<Vector2Int> shapeB)
    {
        if (shapeA.Count != shapeB.Count) return false;

        var normA = Normalize(shapeA);
        var normB = Normalize(shapeB);

        // Verifica si todas las coordenadas normalizadas coinciden exactamente
        return normA.All(a => normB.Any(b => b.x == a.x && b.y == a.y));

    }

    // Lleva cualquier figura a su punto de origen (0,0) relativo
    private static List<Vector2Int> Normalize(List<Vector2Int> points)
    {
        if (points.Count == 0) return points;
        int minX = points.Min(p => p.x);
        int minY = points.Min(p => p.y);
        return points.Select(p => new Vector2Int(p.x - minX, p.y - minY)).ToList();
    }

    // Matemática matricial básica: Rotación de 90 grados (x, y) -> (-y, x)
    private static List<Vector2Int> RotarPatron(List<Vector2Int> points)
    {
        return points.Select(p => new Vector2Int(-p.y, p.x)).ToList();
    }

    // Invierte la figura en el eje X (como mirarse en un espejo): (x, y) -> (-x, y)
    private static List<Vector2Int> EspejarPatron(List<Vector2Int> points)
    {
        return points.Select(p => new Vector2Int(-p.x, p.y)).ToList();
    }
}