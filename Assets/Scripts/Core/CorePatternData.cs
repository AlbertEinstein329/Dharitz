using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGame.Core
{
    // 1. Estructura de Datos Pura para el Patrón (Reemplaza la lógica interna del ScriptableObject)
    [Serializable]
    public class PatternDefDTO
    {
        public int TargetNumber { get; set; }
        public bool AllowRotation { get; set; }
        public bool AllowMirror { get; set; }
        public bool AllowDiagonalReservation { get; set; }
        public List<GridPos> BaseShape { get; set; }

        public PatternDefDTO()
        {
            BaseShape = new List<GridPos>();
        }
    }

    // 2. Motor Matemático de Geometría (Servidor y Cliente)
    public static class CorePatternValidator
    {
        public static bool CheckPattern(List<GridPos> groupCells, PatternDefDTO patternData)
        {
            if (patternData.TargetNumber == 1) return true;
            if (patternData.BaseShape == null || patternData.BaseShape.Count == 0) return false;

            List<GridPos> targetPattern = patternData.BaseShape;

            if (EvaluarRotaciones(groupCells, targetPattern, patternData.AllowRotation))
                return true;

            if (patternData.AllowMirror)
            {
                List<GridPos> mirroredPattern = EspejarPatron(targetPattern);
                if (EvaluarRotaciones(groupCells, mirroredPattern, patternData.AllowRotation))
                    return true;
            }

            return false;
        }

        private static bool EvaluarRotaciones(List<GridPos> cells, List<GridPos> target, bool allowRotation)
        {
            int ciclos = allowRotation ? 4 : 1;
            List<GridPos> currentTarget = target;

            for (int i = 0; i < ciclos; i++)
            {
                if (AreShapesEqual(cells, currentTarget)) return true;
                currentTarget = RotarPatron(currentTarget);
            }

            return false;
        }

        private static bool AreShapesEqual(List<GridPos> shapeA, List<GridPos> shapeB)
        {
            if (shapeA.Count != shapeB.Count) return false;

            var normA = Normalize(shapeA);
            var normB = Normalize(shapeB);

            return normA.All(a => normB.Any(b => b.X == a.X && b.Y == a.Y));
        }

        private static List<GridPos> Normalize(List<GridPos> points)
        {
            if (points.Count == 0) return points;
            int minX = points.Min(p => p.X);
            int minY = points.Min(p => p.Y);
            return points.Select(p => new GridPos(p.X - minX, p.Y - minY)).ToList();
        }

        private static List<GridPos> RotarPatron(List<GridPos> points)
        {
            return points.Select(p => new GridPos(-p.Y, p.X)).ToList();
        }

        private static List<GridPos> EspejarPatron(List<GridPos> points)
        {
            return points.Select(p => new GridPos(-p.X, p.Y)).ToList();
        }
    }
}