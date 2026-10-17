using System.Collections.Generic;

namespace MyGame.Core
{
    /// <summary>
    /// Deduce, solo a partir del tablero, que dados pertenecen a un patron COMPLETADO de la variante.
    /// Un grupo cuenta cuando esta lleno (tantos dados como su numero) y su forma coincide con el patron de ese numero
    /// (<see cref="CorePatternValidator.CheckPattern"/>, con las rotaciones/espejos que permita la variante).
    ///
    /// Se calcula desde el tablero y no desde <c>PlayerDataDTO.ActiveGroups</c> porque ahi solo vive el grupo activo de
    /// cada color: al robar otro dado del mismo color, el grupo ya cerrado deja de estar en esa tabla. Asi tambien sirve
    /// para repintar los patrones tras reconectar, deshacer o mover.
    /// </summary>
    public static class CoreCompletion
    {
        /// <summary>Indices planos (fila * columnas + columna) de las celdas que forman patrones completados.</summary>
        public static List<int> FindMatchedCells(BoardStateDTO board, VariantDefDTO variant)
        {
            var matched = new List<int>();
            if (board == null || variant == null) return matched;

            // Celdas por grupo; el valor de cada dado es el tamano objetivo del grupo
            var cellsByGroup = new Dictionary<int, List<int>>();
            for (int i = 0; i < board.Cells.Length; i++)
            {
                CellStateDTO cell = board.Cells[i];
                if (!cell.IsOccupied) continue;

                if (!cellsByGroup.TryGetValue(cell.GroupId, out List<int> list))
                {
                    list = new List<int>();
                    cellsByGroup[cell.GroupId] = list;
                }
                list.Add(i);
            }

            foreach (var kvp in cellsByGroup)
            {
                List<int> indices = kvp.Value;
                int target = board.Cells[indices[0]].Value;
                if (indices.Count != target) continue; // grupo incompleto (o con mas dados de los permitidos)

                PatternDefDTO pattern = variant.GetPattern(target);
                if (pattern == null) continue;

                var positions = new List<GridPos>(indices.Count);
                foreach (int index in indices) positions.Add(board.GetPos(index)); // (fila, columna) como X, Y

                if (CorePatternValidator.CheckPattern(positions, pattern)) matched.AddRange(indices);
            }

            return matched;
        }
    }
}
