using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MyGame.Core;

public static class PatternValidator
{
    public static bool CheckPattern(List<Vector2Int> groupCells, PatternData patternData)
    {
        // 1. Convertimos la entrada de Unity a formatos DTO
        List<GridPos> coreCells = TransponerEjes(groupCells);
        PatternDefDTO corePattern = patternData.ToDTO();

        // 2. Delegamos el cálculo a la Máquina Gemela (La misma que usará el servidor)
        return CorePatternValidator.CheckPattern(coreCells, corePattern);
    }

    private static List<GridPos> TransponerEjes(List<Vector2Int> points)
    {
        // Mantenemos tu corrección del eje invertido, pero mapeamos a GridPos
        return points.Select(p => new GridPos(p.y, p.x)).ToList();
    }
}