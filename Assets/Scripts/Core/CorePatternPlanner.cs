using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MyGame.Core
{
    /// <summary>Colocaciones posibles de un patron para un grupo abierto.</summary>
    public sealed class PatternPlan
    {
        /// <summary>true si el grupo todavia puede terminar con la forma exacta del patron.</summary>
        public bool Feasible;

        /// <summary>Cuantas colocaciones distintas del patron siguen abiertas (mas = mas margen).</summary>
        public int Placements;

        /// <summary>Casillas libres que completarian el patron (union de todas las colocaciones posibles).</summary>
        public HashSet<int> TargetCells = new HashSet<int>();

        /// <summary>Casillas que faltan por poner.</summary>
        public int Remaining;
    }

    /// <summary>
    /// Planificador de formas: dado un grupo abierto y el patron de su tamano, calcula si todavia puede cerrarse con esa
    /// forma y que casillas la completarian. Logica pura de la Capa 0, segura para usar desde otro hilo.
    /// </summary>
    public static class CorePatternPlanner
    {
        private static readonly ConditionalWeakTable<PatternDefDTO, GridPos[][]> orientationCache = new ConditionalWeakTable<PatternDefDTO, GridPos[][]>();

        /// <summary>Orientaciones distintas del patron (rotaciones y espejo segun sus indicadores), normalizadas a (0,0).</summary>
        public static GridPos[][] Orientations(PatternDefDTO pattern)
        {
            return orientationCache.GetValue(pattern, BuildOrientations);
        }

        private static GridPos[][] BuildOrientations(PatternDefDTO pattern)
        {
            var result = new List<GridPos[]>();
            var seen = new HashSet<string>();

            var variants = new List<List<GridPos>> { new List<GridPos>(pattern.BaseShape) };
            if (pattern.AllowMirror)
            {
                var mirrored = new List<GridPos>();
                foreach (GridPos p in pattern.BaseShape) mirrored.Add(new GridPos(-p.X, p.Y));
                variants.Add(mirrored);
            }

            foreach (List<GridPos> variant in variants)
            {
                List<GridPos> current = variant;
                int turns = pattern.AllowRotation ? 4 : 1;
                for (int i = 0; i < turns; i++)
                {
                    GridPos[] normalized = Normalize(current);
                    if (seen.Add(Key(normalized))) result.Add(normalized);

                    var rotated = new List<GridPos>(current.Count);
                    foreach (GridPos p in current) rotated.Add(new GridPos(-p.Y, p.X));
                    current = rotated;
                }
            }

            return result.ToArray();
        }

        private static GridPos[] Normalize(List<GridPos> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (GridPos p in cells)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
            }

            var normalized = new GridPos[cells.Count];
            for (int i = 0; i < cells.Count; i++) normalized[i] = new GridPos(cells[i].X - minX, cells[i].Y - minY);
            System.Array.Sort(normalized, (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            return normalized;
        }

        private static string Key(GridPos[] cells)
        {
            var sb = new System.Text.StringBuilder();
            foreach (GridPos p in cells) sb.Append(p.X).Append(',').Append(p.Y).Append(';');
            return sb.ToString();
        }

        /// <summary>
        /// Calcula las colocaciones del patron compatibles con los dados que el grupo ya tiene. Si el grupo no tiene dados
        /// (<paramref name="groupCells"/> vacio) cuenta todas las colocaciones libres y rellena <paramref name="perCell"/>
        /// con cuantas colocaciones pasan por cada casilla (sirve para elegir donde abrir un grupo).
        /// </summary>
        public static PatternPlan Plan(BoardStateDTO board, ICollection<GridPos> groupCells, DieColor color, int groupId, PatternDefDTO pattern, int[] perCell = null)
        {
            var plan = new PatternPlan();
            if (pattern == null) return plan;

            int size = pattern.BaseShape.Count;
            int placed = groupCells?.Count ?? 0;
            plan.Remaining = size - placed;

            if (pattern.TargetNumber == 1 || size <= 1)
            {
                // El patron de un solo dado se cumple siempre
                plan.Feasible = true;
                plan.Placements = 1;
                return plan;
            }

            GridPos[][] orientations = Orientations(pattern);
            GridPos first = default;
            if (placed > 0)
            {
                foreach (GridPos g in groupCells) { first = g; break; }
            }

            var cells = new int[size];

            foreach (GridPos[] shape in orientations)
            {
                if (placed > 0)
                {
                    // El primer dado del grupo tiene que coincidir con alguna casilla de la forma
                    for (int anchor = 0; anchor < shape.Length; anchor++)
                        TryPlacement(board, groupCells, placed, color, groupId, shape, first.X - shape[anchor].X, first.Y - shape[anchor].Y, cells, plan, perCell);
                }
                else
                {
                    for (int dr = 0; dr < board.Rows; dr++)
                        for (int dc = 0; dc < board.Cols; dc++)
                            TryPlacement(board, groupCells, placed, color, groupId, shape, dr, dc, cells, plan, perCell);
                }
            }

            plan.Feasible = plan.Placements > 0;
            return plan;
        }

        private static void TryPlacement(BoardStateDTO board, ICollection<GridPos> groupCells, int placed, DieColor color, int groupId,
            GridPos[] shape, int dr, int dc, int[] cells, PatternPlan plan, int[] perCell)
        {
            for (int i = 0; i < shape.Length; i++)
            {
                int r = shape[i].X + dr;
                int c = shape[i].Y + dc;
                if (r < 0 || r >= board.Rows || c < 0 || c >= board.Cols) return;
                cells[i] = board.GetIndex(r, c);
            }

            // Todos los dados que ya tiene el grupo deben caer dentro de esta colocacion
            if (placed > 0)
            {
                foreach (GridPos g in groupCells)
                {
                    int idx = board.GetIndex(g.X, g.Y);
                    bool found = false;
                    for (int i = 0; i < cells.Length; i++)
                    {
                        if (cells[i] == idx) { found = true; break; }
                    }

                    if (!found) return;
                }
            }

            // Las demas casillas tienen que poder recibir un dado de este grupo
            for (int i = 0; i < cells.Length; i++)
            {
                bool isGroupCell = false;
                if (placed > 0)
                {
                    foreach (GridPos g in groupCells)
                    {
                        if (board.GetIndex(g.X, g.Y) == cells[i]) { isGroupCell = true; break; }
                    }
                }

                if (isGroupCell) continue;
                if (!IsFreeFor(board, cells[i], color, groupId)) return;
            }

            plan.Placements++;
            for (int i = 0; i < cells.Length; i++)
            {
                bool isGroupCell = false;
                if (placed > 0)
                {
                    foreach (GridPos g in groupCells)
                    {
                        if (board.GetIndex(g.X, g.Y) == cells[i]) { isGroupCell = true; break; }
                    }
                }

                if (isGroupCell) continue;
                plan.TargetCells.Add(cells[i]);
                if (perCell != null) perCell[cells[i]]++;
            }
        }

        // Libre y sin dado vecino (en cruz) del mismo color de otro grupo, que lo dejaria como zona muerta
        private static bool IsFreeFor(BoardStateDTO board, int index, DieColor color, int groupId)
        {
            if (board.Cells[index].IsOccupied) return false;

            int r = index / board.Cols;
            int c = index % board.Cols;
            for (int d = 0; d < 4; d++)
            {
                int nr = r + (d == 0 ? -1 : d == 1 ? 1 : 0);
                int nc = c + (d == 2 ? -1 : d == 3 ? 1 : 0);
                if (nr < 0 || nr >= board.Rows || nc < 0 || nc >= board.Cols) continue;
                CellStateDTO n = board.Cells[board.GetIndex(nr, nc)];
                if (n.IsOccupied && n.Color == color && n.GroupId != groupId) return false;
            }

            return true;
        }
    }
}
