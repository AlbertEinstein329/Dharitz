using System.Collections.Generic;
using System.Text;
using MyGame.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Banco de pruebas del bot: juega partidas completas de un jugador con el nucleo (sin escena) y compara el aleatorio legal con
/// los tres niveles, desglosando de donde sale la puntuacion. "Neto" = puntos finales incluida la penalizacion por huecos.
/// Menu: Tools &gt; Dharitz &gt; Bot &gt; Benchmark.
/// </summary>
public static class DharitzBotBench
{
    private const int MaxDice = 52;

    [MenuItem("Tools/Dharitz/Bot/Benchmark")]
    public static void RunMenu() => Debug.Log(Run("Normal", 20, 200, true, false));

    [MenuItem("Tools/Dharitz/Bot/Benchmark de dificultades (Easy, Normal, Hard)")]
    public static void RunProfilesMenu() => Debug.Log(RunProfiles("Normal", 16, 0));

    /// <summary>Compara las tres dificultades del juego (perfiles con Monte Carlo ligero) con el aleatorio legal. budgetOverrideMs 0 = el de cada perfil.</summary>
    public static string RunProfiles(string variantName, int games, int budgetOverrideMs)
    {
        return Run(variantName, games, budgetOverrideMs, false, false, true);
    }

    public static string Run(string variantName, int games, int monteCarloBudgetMs, bool includeMonteCarlo, bool legacyScoring)
    {
        return Run(variantName, games, monteCarloBudgetMs, includeMonteCarlo, legacyScoring, false);
    }

    public static string Run(string variantName, int games, int monteCarloBudgetMs, bool includeMonteCarlo, bool legacyScoring, bool profilesOnly)
    {
        VariantDefDTO variant = LoadVariant(variantName);
        if (variant == null) return "No se encontro la variante " + variantName;

        var sb = new StringBuilder($"Variante {variantName}, {games} partidas de un jugador, puntuacion {(legacyScoring ? "ANTIGUA" : "NUEVA")}\n");
        var contenders = new List<(string name, int level, int budget)>();
        if (profilesOnly)
        {
            contenders.Add(("Aleatorio legal", -1, 0));
            contenders.Add(("Perfil EASY", -2, monteCarloBudgetMs));
            contenders.Add(("Perfil NORMAL", -3, monteCarloBudgetMs));
            contenders.Add(("Perfil HARD", -4, monteCarloBudgetMs));
        }
        else
        {
            contenders.Add(("Aleatorio legal", -1, 0));
            contenders.Add(("Greedy", 0, 0));
            contenders.Add(("Greedy penalizado", 1, 0));
            if (includeMonteCarlo) contenders.Add(($"Monte Carlo {monteCarloBudgetMs} ms", 2, monteCarloBudgetMs));
        }

        foreach (var (name, level, budget) in contenders)
        {
            var total = new GameResult();
            double maxMs = 0;
            int deaths = 0;

            for (int g = 1; g <= games; g++)
            {
                GameResult r = Play(variant, g * 1237, level, budget, legacyScoring);
                total.Score += r.Score; total.Base += r.Base; total.Patterns += r.Patterns; total.Collection += r.Collection;
                total.Lines += r.Lines; total.Gaps += r.Gaps; total.Placed += r.Placed;
                total.ClosedGroups += r.ClosedGroups; total.MatchedGroups += r.MatchedGroups;
                total.Decisions += r.Decisions; total.Rollouts += r.Rollouts; total.ThinkMs += r.ThinkMs;
                if (r.MaxMs > maxMs) maxMs = r.MaxMs;
                if (r.Dead) deaths++;
            }

            double n = games;
            double net = (total.Score + total.Gaps) / n;
            double sumParts = (total.Base + total.Patterns + total.Collection + total.Lines) / n;
            sb.AppendLine($"{name}: NETO {net:F0} | base {total.Base / n:F0} ({100 * total.Base / n / System.Math.Max(1, sumParts):F0}%) | patrones {total.Patterns / n:F0} ({100 * total.Patterns / n / System.Math.Max(1, sumParts):F0}%) | coleccion {total.Collection / n:F0} | lineas {total.Lines / n:F0} ({100 * total.Lines / n / System.Math.Max(1, sumParts):F0}%) | huecos {total.Gaps / n:F0}");
            sb.AppendLine($"    grupos cerrados {total.ClosedGroups / n:F1}, con la forma exacta {total.MatchedGroups / n:F1} ({(total.ClosedGroups > 0 ? 100.0 * total.MatchedGroups / total.ClosedGroups : 0):F0}%) | dados {total.Placed / n:F1}/{MaxDice} | eliminado {deaths}/{games} | decision media {(total.Decisions > 0 ? total.ThinkMs / total.Decisions : 0):F1} ms, max {maxMs:F0} ms, simulaciones {(total.Decisions > 0 ? (float)total.Rollouts / total.Decisions : 0):F0}");
        }

        return sb.ToString();
    }

    private static VariantDefDTO LoadVariant(string variantName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:VariantData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<VariantData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.variantName == variantName) return data.ToDTO();
        }

        return null;
    }

    private struct GameResult
    {
        public int Score, Base, Patterns, Collection, Lines, Gaps, Placed, Decisions, Rollouts, ClosedGroups, MatchedGroups;
        public bool Dead;
        public double MaxMs, ThinkMs;
    }

    private static GameResult Play(VariantDefDTO variant, int seed, int level, int budgetMs, bool legacyScoring)
    {
        MatchStateDTO st = CoreMatchSetup.CreateMatch(seed, new List<string> { "Bot" }, 10, 8, variant);
        if (legacyScoring) st.ScoringConfig = ScoringConfigDTO.Legacy();

        var result = new GameResult();
        var rnd = new System.Random(seed);

        for (int turn = 0; turn < 150 && st.CurrentPhase == MatchPhase.PlayerTurn; turn++)
        {
            if (st.PlayerProfiles[0].PlacedDice >= MaxDice) break;
            if (!CoreDrawProcessor.ProcessDrawIntent(st, new DrawDieCommand { PlayerId = 0 }, st.ServerRNG)) break;

            int redraws = 0;
            while (true)
            {
                BotSnapshot snap = BotSnapshot.Capture(st, 0, MaxDice);
                BotDecision d;
                if (level == -1) d = RandomLegal(snap, rnd);
                else if (level <= -2) d = BotBrain.Choose(snap, BotProfile.For((BotDifficulty)(-level - 2)), seed * 31 + turn, budgetMs);
                else d = BotBrain.Choose(snap, (BotLevel)level, seed * 31 + turn, budgetMs);

                result.Decisions++;
                result.Rollouts += d.Rollouts;
                result.ThinkMs += d.ElapsedMs;
                if (d.ElapsedMs > result.MaxMs) result.MaxMs = d.ElapsedMs;

                if (d.HasMove)
                {
                    MyGame.Core.DieColor color = st.CurrentDrawnColor.Value;
                    int value = st.CurrentDrawnValue.Value;
                    PatternDefDTO pattern = st.VariantConfig.GetPattern(value);
                    ScoringConfigDTO sc = st.ScoringConfig;
                    var cmd = new PlaceDieCommand { PlayerId = 0, TargetCell = d.Cell, Color = color, GroupId = st.PlayerProfiles[0].ActiveGroups[color].Id, Number = value };
                    if (!CoreMatchProcessor.ProcessPlacementIntent(st, cmd, pattern, pattern, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers, out PlacementResult placement))
                    {
                        result.Dead = true; // el bot propuso algo que el juego rechaza: no deberia pasar nunca
                    }
                    else
                    {
                        if (placement.GroupClosed) result.ClosedGroups++;
                        if (placement.PatternMatched) result.MatchedGroups++;
                        CoreTurnActions.ConsumeHand(st);
                    }
                    break;
                }

                if (st.PlayerProfiles[0].ReDraws > 0 && redraws++ < 5)
                {
                    CoreDrawProcessor.ProcessReDrawIntent(st, new ReDrawCommand { PlayerId = 0 }, st.ServerRNG);
                    if (!CoreDrawProcessor.ProcessDrawIntent(st, new DrawDieCommand { PlayerId = 0 }, st.ServerRNG)) break;
                    continue;
                }

                result.Dead = true; // sin jugada ni Re-Draws: muerte subita
                break;
            }

            if (result.Dead) break;
            st.HasDrawn = false;
        }

        PlayerDataDTO p = st.PlayerProfiles[0];
        ScoringConfigDTO scoring = st.ScoringConfig;
        result.Placed = p.PlacedDice;
        result.Gaps = CoreTopologyCalculator.CalculateGapPenalty(st.PlayerBoards[0].Cells, 10, 8, scoring, out _);
        result.Score = p.Score;
        result.Base = p.PlacedDice * scoring.PointsPerDie;
        result.Lines = p.AccumulatedStructurePoints;
        for (int size = 1; size <= 6; size++) result.Patterns += p.PatternCounts[size] * scoring.GetPatternBonus(size);

        bool all = true;
        for (int size = 1; size <= 6; size++) if (p.PatternCounts[size] < 1) all = false;
        if (all) result.Collection = scoring.CollectionBonus;

        return result;
    }

    // Referencia: una casilla legal cualquiera (mismas reglas que el bot)
    private static BotDecision RandomLegal(BotSnapshot snap, System.Random rnd)
    {
        PatternDefDTO pattern = snap.Variant.GetPattern(snap.Value);
        var legal = new List<GridPos>();
        for (int r = 0; r < snap.Board.Rows; r++)
        {
            for (int c = 0; c < snap.Board.Cols; c++)
            {
                if (!CorePlacementRules.IsLegal(snap.Board, snap.Profile, r, c, snap.Color, snap.GroupId, snap.Value, pattern, pattern)) continue;
                legal.Add(new GridPos(r, c));
            }
        }

        var decision = new BotDecision { LegalCells = legal.Count };
        if (legal.Count > 0)
        {
            decision.HasMove = true;
            decision.Cell = legal[rnd.Next(legal.Count)];
        }

        return decision;
    }
}
