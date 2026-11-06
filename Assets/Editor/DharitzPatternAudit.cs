using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MyGame.Core;

/// <summary>
/// Audita las plantillas de patron de todas las Variantes (Assets/Resources/Variantes): numero coherente, sin celdas
/// repetidas, conectadas, y que el validador acepte cada orientacion permitida (rotacion/espejo/traslacion) y rechace
/// una forma distinta. Detecta patrones mal definidos antes de que el jugador los sufra.
/// </summary>
public static class DharitzPatternAudit
{
    [MenuItem("Tools/Dharitz/Auditar patrones de variantes")]
    public static void RunFromMenu()
    {
        string report = Run(out int problems);
        if (problems == 0) Debug.Log(report);
        else Debug.LogWarning(report);
    }

    public static string Run(out int problems)
    {
        var sb = new StringBuilder();
        problems = 0;

        VariantData[] variants = Resources.LoadAll<VariantData>("Variantes");
        sb.AppendLine($"[PatternAudit] {variants.Length} variantes encontradas.");

        foreach (VariantData variant in variants.OrderBy(v => v.name))
        {
            sb.AppendLine($"- {variant.name} ({variant.variantName}): {variant.patterns.Count} patrones");

            for (int number = 1; number <= 6; number++)
            {
                PatternData pattern = variant.GetPattern(number);
                if (pattern == null)
                {
                    sb.AppendLine($"    PROBLEMA patron {number}: falta en la variante");
                    problems++;
                    continue;
                }

                problems += AuditPattern(variant.name, number, pattern, sb);
            }
        }

        sb.AppendLine(problems == 0 ? "[PatternAudit] OK: sin problemas." : $"[PatternAudit] {problems} problema(s).");
        return sb.ToString();
    }

    private static int AuditPattern(string variantName, int number, PatternData pattern, StringBuilder sb)
    {
        int problems = 0;
        string tag = $"    {pattern.name} (n={number})";
        List<GridPos> shape = pattern.baseShape.Select(v => new GridPos(v.x, v.y)).ToList();

        if (pattern.targetNumber != number) { sb.AppendLine($"{tag}: targetNumber={pattern.targetNumber} no coincide"); problems++; }
        if (shape.Count != number) { sb.AppendLine($"{tag}: tiene {shape.Count} celdas, deberia tener {number}"); problems++; }
        if (shape.Distinct().Count() != shape.Count) { sb.AppendLine($"{tag}: celdas repetidas"); problems++; }
        if (number > 1 && !IsConnected(shape)) { sb.AppendLine($"{tag}: celdas no conectadas (ni en diagonal)"); problems++; }

        PatternDefDTO dto = pattern.ToDTO();

        // La forma base, trasladada, debe aceptarse siempre
        if (!CorePatternValidator.CheckPattern(Translate(shape, 4, 3), dto)) { sb.AppendLine($"{tag}: no acepta su propia forma base"); problems++; }

        // Todas las orientaciones permitidas deben aceptarse
        foreach (List<GridPos> variantShape in Orientations(shape, pattern.allowRotation, pattern.allowMirror))
        {
            if (!CorePatternValidator.CheckPattern(Translate(variantShape, 2, 2), dto))
            {
                sb.AppendLine($"{tag}: rechaza una orientacion permitida");
                problems++;
                break;
            }
        }

        // Una forma distinta de la misma cantidad de dados (si existe) debe rechazarse
        if (number > 1)
        {
            List<GridPos> other = FindDifferentShape(shape, dto, pattern.allowRotation, pattern.allowMirror);
            if (other != null && CorePatternValidator.CheckPattern(other, dto)) { sb.AppendLine($"{tag}: acepta una forma distinta"); problems++; }
        }

        // Orientaciones NO permitidas: si no hay rotacion/espejo, una orientacion girada igual de valida debe rechazarse
        if (!pattern.allowRotation && number > 1)
        {
            List<GridPos> rotated = Rotate(shape);
            bool rotatedIsSame = SameShape(rotated, shape) || (pattern.allowMirror && SameShape(rotated, Mirror(shape)));
            if (!rotatedIsSame && CorePatternValidator.CheckPattern(Translate(rotated, 2, 2), dto)) { sb.AppendLine($"{tag}: acepta una rotacion aunque allowRotation=false"); problems++; }
        }

        if (problems == 0) sb.AppendLine($"{tag}: ok (rot={pattern.allowRotation}, espejo={pattern.allowMirror}, forma={string.Join(" ", shape.Select(c => $"({c.X},{c.Y})"))})");
        return problems;
    }

    // -------- utilidades de geometria (independientes del validador, para no auditar el codigo con si mismo) --------

    private static List<GridPos> Translate(List<GridPos> cells, int dx, int dy)
    {
        int minX = cells.Min(c => c.X);
        int minY = cells.Min(c => c.Y);
        return cells.Select(c => new GridPos(c.X - minX + dx, c.Y - minY + dy)).ToList();
    }

    private static List<GridPos> Rotate(List<GridPos> cells) => cells.Select(c => new GridPos(-c.Y, c.X)).ToList();
    private static List<GridPos> Mirror(List<GridPos> cells) => cells.Select(c => new GridPos(-c.X, c.Y)).ToList();

    private static IEnumerable<List<GridPos>> Orientations(List<GridPos> shape, bool rotate, bool mirror)
    {
        var bases = new List<List<GridPos>> { shape };
        if (mirror) bases.Add(Mirror(shape));

        foreach (List<GridPos> b in bases)
        {
            List<GridPos> current = b;
            int turns = rotate ? 4 : 1;
            for (int i = 0; i < turns; i++)
            {
                yield return current;
                current = Rotate(current);
            }
        }
    }

    private static bool SameShape(List<GridPos> a, List<GridPos> b)
    {
        if (a.Count != b.Count) return false;
        var na = Translate(a, 0, 0).OrderBy(c => c.X).ThenBy(c => c.Y).ToList();
        var nb = Translate(b, 0, 0).OrderBy(c => c.X).ThenBy(c => c.Y).ToList();
        return na.SequenceEqual(nb);
    }

    private static bool IsConnected(List<GridPos> cells)
    {
        var remaining = new HashSet<GridPos>(cells);
        var queue = new Queue<GridPos>();
        queue.Enqueue(cells[0]);
        remaining.Remove(cells[0]);

        while (queue.Count > 0)
        {
            GridPos cur = queue.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var next = new GridPos(cur.X + dx, cur.Y + dy);
                    if (remaining.Remove(next)) queue.Enqueue(next);
                }
            }
        }
        return remaining.Count == 0;
    }

    // Busca, entre formas de la misma cantidad dentro de una caja pequena, una que NO sea ninguna orientacion permitida
    private static List<GridPos> FindDifferentShape(List<GridPos> shape, PatternDefDTO dto, bool rotate, bool mirror)
    {
        int n = shape.Count;
        var candidates = new List<List<GridPos>>
        {
            Enumerable.Range(0, n).Select(i => new GridPos(i, 0)).ToList(),         // linea vertical
            Enumerable.Range(0, n).Select(i => new GridPos(0, i)).ToList(),         // linea horizontal
            Enumerable.Range(0, n).Select(i => new GridPos(i, i)).ToList(),         // diagonal
            Enumerable.Range(0, n).Select(i => new GridPos(i / 2, i % 2)).ToList() // bloque 2 de ancho
        };

        foreach (List<GridPos> candidate in candidates)
        {
            if (candidate.Distinct().Count() != n) continue;
            bool isAllowed = Orientations(shape, rotate, mirror).Any(o => SameShape(o, candidate));
            if (!isAllowed) return Translate(candidate, 3, 3);
        }
        return null;
    }
}
