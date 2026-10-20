using System.Collections.Generic;
using System.Text;
using MyGame.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Banco de pruebas del bot: juega partidas completas de un jugador con el nucleo (sin escena) y compara el aleatorio legal con
/// los tres niveles. "Neto" = puntos + penalizacion por huecos cerrados, que es lo que se cobra al acabar.
/// Menu: Tools &gt; Dharitz &gt; Bot &gt; Benchmark.
/// </summary>
public static class DharitzBotBench
{
    private const int MaxDice = 52;

    [MenuItem("Tools/Dharitz/Bot/Benchmark")]
    public static void RunMenu() => Debug.Log(Run("Normal", 20, 200, true));

    public static string Run(string variantName, int games, int monteCarloBudgetMs, bool includeMonteCarlo)
    {
        VariantDefDTO variant = LoadVariant(variantName);
        if (variant == null) return "No se encontro la variante " + variantName;

        var sb = new StringBuilder($"Variante {variantName}, {games} partidas de un jugador\n");
        var contenders = new List<(string name, int level, int budget)> { ("Aleatorio legal", -1, 0), ("Greedy", 0, 0), ("Greedy penalizado", 1, 0) };
        if (includeMonteCarlo) contenders.Add(($"Monte Carlo {monteCarloBudgetMs} ms", 2, monteCarloBudgetMs));

        foreach (var (name, level, budget) in contenders)
        {
            double points = 0, gaps = 0, placed = 0, thinkMs = 0, maxMs = 0;
            int deaths = 0, decisions = 0, rollouts = 0;

            for (int g = 1; g <= games; g++)
            {
                GameResult r = Play(variant, g * 1237, level, budget);
                points += r.Score; gaps += r.Gaps; placed += r.Placed;
                thinkMs += r.ThinkMs; decisions += r.Decisions; rollouts += r.Rollouts;
                if (r.MaxMs > maxMs) maxMs = r.MaxMs;
                if (r.Dead) deaths++;
            }

            sb.AppendLine($"{name}: puntos {points / games:F0} | huecos {gaps / games:F0} | NETO {(points + gaps) / games:F0} | dados {placed / games:F1}/{MaxDice} | eliminado {deaths}/{games} | decision media {(decisions > 0 ? thinkMs / decisions : 0):F1} ms, max {maxMs:F0} ms, simulaciones {(decisions > 0 ? (float)rollouts / decisions : 0):F0}");
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
        public int Score, Gaps, Placed, Decisions, Rollouts;
        public bool Dead;
        public double MaxMs, ThinkMs;
    }

    private static GameResult Play(VariantDefDTO variant, int seed, int level, int budgetMs)
    {
        MatchStateDTO st = CoreMatchSetup.CreateMatch(seed, new List<string> { "Bot" }, 10, 8, variant);
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
                BotDecision d = level < 0 ? RandomLegal(snap, rnd) : BotBrain.Choose(snap, (BotLevel)level, seed * 31 + turn, budgetMs);

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
                    if (!CoreMatchProcessor.ProcessPlacementIntent(st, cmd, pattern, pattern, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers))
                        result.Dead = true; // el bot propuso algo que el juego rechaza: no deberia pasar nunca
                    else
                        CoreTurnActions.ConsumeHand(st);
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
        result.Score = p.Score;
        result.Placed = p.PlacedDice;
        result.Gaps = CoreTopologyCalculator.CalculateGapPenalty(st.PlayerBoards[0].Cells, 10, 8, out _);
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
