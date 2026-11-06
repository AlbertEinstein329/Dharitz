using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MyGame.Core
{
    public enum BotLevel
    {
        /// <summary>Elige la casilla legal que mas puntos da ahora.</summary>
        Greedy = 0,
        /// <summary>Como Greedy, pero resta huecos cerrados y grupos condenados, y premia avanzar filas y columnas.</summary>
        GreedyPenalized = 1,
        /// <summary>Simula muchas partidas cortas por cada casilla finalista y elige la de mejor media.</summary>
        MonteCarlo = 2
    }

    /// <summary>Copia aislada de lo que el bot necesita: se puede decidir en otro hilo sin tocar el estado de la partida.</summary>
    public sealed class BotSnapshot
    {
        public BoardStateDTO Board;
        public PlayerDataDTO Profile;
        public DieColor Color;
        public int Value;
        public int GroupId;
        public List<DieColor> Bag;
        public VariantDefDTO Variant;
        public ScoringConfigDTO Scoring;
        public int MaxDice;

        /// <summary>Foto del turno del jugador despues de robar. Devuelve null si no hay dado en la mano.</summary>
        public static BotSnapshot Capture(MatchStateDTO state, int playerIndex, int maxDicePerPlayer)
        {
            if (!state.HasDrawn || !state.CurrentDrawnColor.HasValue || !state.CurrentDrawnValue.HasValue) return null;

            PlayerDataDTO profile = CoreTurnActions.CloneProfile(state.PlayerProfiles[playerIndex]);
            DieColor color = state.CurrentDrawnColor.Value;
            if (!profile.ActiveGroups.TryGetValue(color, out GroupDataDTO group) || group == null) return null;

            return new BotSnapshot
            {
                Board = CoreTurnActions.CloneBoard(state.PlayerBoards[playerIndex]),
                Profile = profile,
                Color = color,
                Value = state.CurrentDrawnValue.Value,
                GroupId = group.Id,
                Bag = new List<DieColor>(state.DiceBag),
                Variant = state.VariantConfig,
                Scoring = state.ScoringConfig,
                MaxDice = maxDicePerPlayer
            };
        }
    }

    public sealed class BotDecision
    {
        public bool HasMove;
        public GridPos Cell;
        public BotLevel Level;
        public int LegalCells;
        public int Finalists;
        public int Rollouts;
        public double ElapsedMs;
        public float Value;
    }

    /// <summary>
    /// Cerebro del bot de las partidas locales. Logica pura de la Capa 0 (sin Unity): usa las mismas reglas que el juego
    /// (CorePlacementRules + CoreMatchProcessor) sobre copias del tablero, de modo que nunca propone una jugada ilegal.
    /// </summary>
    public static class BotBrain
    {
        public const int DefaultBudgetMs = 600;

        private const int Finalists = 8;
        private const int MaxRounds = 40;
        private const int Horizon = 8;
        private const int FastTries = 6;
        private const float DeathPenalty = 1500f;

        // Prioridades del bot: los patrones mandan; las filas y columnas cuentan a medias y solo desempatan en la posicion
        private const float LineWeight = 0.5f;      // fraccion del bono de lineas que el bot se "cree" al valorar
        private const float PatternWeight = 0.8f;   // fraccion del bono de un patron aun abierto que se acredita por avanzar hacia el
        private const float LineHint = 12f;         // empujon posicional a rellenar filas/columnas

        public static BotDecision Choose(BotSnapshot snap, BotLevel level, int seed, int budgetMs = DefaultBudgetMs)
        {
            var timer = Stopwatch.StartNew();
            var decision = new BotDecision { Level = level };
            var rng = new Random(seed);
            var sim = new Sim(snap, rng.Next());

            // 1) Todas las casillas legales y su valoracion inmediata
            var options = new List<Option>();
            sim.Load(snap);
            float potentialBefore = sim.PatternPotential();
            for (int r = 0; r < snap.Board.Rows; r++)
            {
                for (int c = 0; c < snap.Board.Cols; c++)
                {
                    // Una colocacion fallida no modifica el estado; una correcta si, asi que se recarga la copia
                    if (!sim.CanTouch(r, c) || !sim.Place(new GridPos(r, c))) continue;

                    int rawGain = sim.Profile.Score - snap.Profile.Score;
                    var option = new Option
                    {
                        Cell = new GridPos(r, c),
                        RawGain = rawGain,
                        Gain = rawGain - (1f - LineWeight) * (sim.Profile.AccumulatedStructurePoints - snap.Profile.AccumulatedStructurePoints)
                    };

                    // Avanzar (o romper) la forma de los grupos abiertos pesa tanto como los puntos que da ahora
                    option.Positional = sim.Heuristic(r, c) + (sim.PatternPotential() - potentialBefore);
                    option.Noise = (float)rng.NextDouble();
                    options.Add(option);
                    sim.Load(snap);
                }
            }

            decision.LegalCells = options.Count;
            if (options.Count == 0)
            {
                decision.ElapsedMs = timer.Elapsed.TotalMilliseconds;
                return decision;
            }

            decision.HasMove = true;

            if (level == BotLevel.Greedy)
            {
                options.Sort((a, b) => (b.RawGain + b.Noise * 0.01f).CompareTo(a.RawGain + a.Noise * 0.01f));
                return Finish(decision, options[0], options[0].RawGain, timer);
            }

            options.Sort((a, b) => (b.Gain + b.Positional + b.Noise * 0.01f).CompareTo(a.Gain + a.Positional + a.Noise * 0.01f));

            if (level == BotLevel.GreedyPenalized || options.Count == 1)
                return Finish(decision, options[0], options[0].Gain + options[0].Positional, timer);

            // 2) Monte Carlo sobre los mejores candidatos, repartiendo el tiempo por rondas
            int finalists = Math.Min(Finalists, options.Count);
            decision.Finalists = finalists;
            var sums = new double[finalists];
            var counts = new int[finalists];

            for (int round = 0; round < MaxRounds; round++)
            {
                // Mismos dados y mismo "azar" para todos los finalistas de la ronda: la comparacion entre ellos es mas limpia
                int roundSeed = rng.Next();
                for (int i = 0; i < finalists; i++)
                {
                    if (round > 0 && timer.ElapsedMilliseconds >= budgetMs) goto Done;
                    sums[i] += Rollout(sim, snap, options[i].Cell, new Random(roundSeed));
                    counts[i]++;
                    decision.Rollouts++;
                }
            }

        Done:
            int best = 0;
            double bestMean = double.NegativeInfinity;
            for (int i = 0; i < finalists; i++)
            {
                if (counts[i] == 0) continue;
                double mean = sums[i] / counts[i];
                if (mean > bestMean) { bestMean = mean; best = i; }
            }

            return Finish(decision, options[best], (float)bestMean, timer);
        }

        private static BotDecision Finish(BotDecision decision, Option option, float value, Stopwatch timer)
        {
            decision.Cell = option.Cell;
            decision.Value = value;
            decision.ElapsedMs = timer.Elapsed.TotalMilliseconds;
            return decision;
        }

        // Una partida corta hacia delante: pone la casilla elegida y juega Horizon turnos con una politica rapida
        private static float Rollout(Sim sim, BotSnapshot snap, GridPos first, Random rng)
        {
            sim.Load(snap);
            sim.State.InitializeRNG(rng.Next());
            if (!sim.Place(first)) return -DeathPenalty;
            CoreTurnActions.ConsumeHand(sim.State);

            // El orden futuro de la bolsa se desconoce: se baraja
            var bag = sim.State.DiceBag;
            bag.AddRange(snap.Bag);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                DieColor tmp = bag[i]; bag[i] = bag[j]; bag[j] = tmp;
            }

            int turns = Math.Min(Horizon, Math.Max(0, snap.MaxDice - sim.Profile.PlacedDice));
            for (int t = 0; t < turns; t++)
            {
                if (!sim.PlayFastTurn(rng)) return RolloutPoints(sim, snap) - DeathPenalty;
            }

            // Puntos ganados (con las lineas a media ponderacion) + estado del tablero + patrones abiertos que van bien
            return RolloutPoints(sim, snap) + sim.Heuristic(-1, -1) + sim.PatternPotential();
        }

        private static float RolloutPoints(Sim sim, BotSnapshot snap)
        {
            int structure = sim.Profile.AccumulatedStructurePoints - snap.Profile.AccumulatedStructurePoints;
            return (sim.Profile.Score - snap.Profile.Score) - (1f - LineWeight) * structure;
        }

        private struct Option
        {
            public GridPos Cell;
            public int RawGain;
            public float Gain;
            public float Positional;
            public float Noise;
        }

        // Estado de trabajo de un solo jugador (indice 0) que se reutiliza entre simulaciones
        private sealed class Sim
        {
            public readonly MatchStateDTO State = new MatchStateDTO();
            public BoardStateDTO Board;
            public PlayerDataDTO Profile;
            private readonly BotSnapshot snap;

            public Sim(BotSnapshot snapshot, int seed)
            {
                snap = snapshot;
                State.CurrentPhase = MatchPhase.PlayerTurn;
                State.CurrentPlayerIndex = 0;
                State.VariantConfig = snapshot.Variant;
                State.ScoringConfig = snapshot.Scoring;
                State.DiceBag = new List<DieColor>();
                State.InitializeRNG(seed);
            }

            public void Load(BotSnapshot s)
            {
                Board = CoreTurnActions.CloneBoard(s.Board);
                Profile = CoreTurnActions.CloneProfile(s.Profile);
                Profile.Id = 0;
                State.PlayerBoards[0] = Board;
                State.PlayerProfiles[0] = Profile;
                State.HasDrawn = true;
                State.CurrentDrawnColor = s.Color;
                State.CurrentDrawnValue = s.Value;
                State.DiceBag.Clear();
            }

            private PatternDefDTO Pattern(int number) => snap.Variant?.GetPattern(number);

            // Reglas de contacto del dado en mano
            public bool CanTouch(int r, int c)
            {
                DieColor color = State.CurrentDrawnColor.Value;
                int value = State.CurrentDrawnValue.Value;
                int groupId = Profile.ActiveGroups[color].Id;
                return CorePlacementRules.PassesContactRules(Board, Profile, r, c, color, groupId, value, Pattern(value));
            }

            // Coloca el dado en mano con el mismo procesador que usa el juego (comprueba supervivencia y puntua)
            public bool Place(GridPos cell)
            {
                DieColor color = State.CurrentDrawnColor.Value;
                int value = State.CurrentDrawnValue.Value;
                PatternDefDTO pattern = Pattern(value);
                ScoringConfigDTO sc = State.ScoringConfig;

                var command = new PlaceDieCommand
                {
                    PlayerId = 0,
                    TargetCell = cell,
                    Color = color,
                    GroupId = Profile.ActiveGroups[color].Id,
                    Number = value
                };

                return CoreMatchProcessor.ProcessPlacementIntent(State, command, pattern, pattern,
                    sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers);
            }

            // Turno rapido de la simulacion: robar, ordenar casillas por contacto y poner la primera que el juego acepte
            public bool PlayFastTurn(Random rng)
            {
                int redraws = 0;
                while (true)
                {
                    if (!CoreDrawProcessor.ProcessDrawIntent(State, new DrawDieCommand { PlayerId = 0 }, State.ServerRNG))
                        return true; // bolsa vacia: la simulacion acaba sin penalizar

                    DieColor color = State.CurrentDrawnColor.Value;
                    int value = State.CurrentDrawnValue.Value;
                    GroupDataDTO group = Profile.ActiveGroups[color];

                    // Si el grupo tiene forma posible, las casillas que la completan van primero; si es nuevo, las que
                    // admiten mas colocaciones del patron
                    PatternDefDTO groupPattern = Pattern(group.TargetSize);
                    PatternPlan plan = null;
                    int[] perCell = null;
                    if (groupPattern != null && groupPattern.TargetNumber != 1)
                    {
                        if (group.OccupiedCells.Count > 0)
                        {
                            plan = CorePatternPlanner.Plan(Board, group.OccupiedCells, color, group.Id, groupPattern);
                        }
                        else
                        {
                            perCell = new int[Board.Cells.Length];
                            plan = CorePatternPlanner.Plan(Board, group.OccupiedCells, color, group.Id, groupPattern, perCell);
                        }
                    }

                    var tries = new List<(float score, GridPos cell)>();
                    for (int r = 0; r < Board.Rows; r++)
                    {
                        for (int c = 0; c < Board.Cols; c++)
                        {
                            if (!CorePlacementRules.PassesContactRules(Board, Profile, r, c, color, group.Id, value, Pattern(value))) continue;

                            int index = Board.GetIndex(r, c);
                            float shape = 0f;
                            if (plan != null && plan.Feasible)
                            {
                                if (plan.TargetCells.Contains(index)) shape += 8f;
                                if (perCell != null) shape += perCell[index] * 1.2f;
                            }

                            tries.Add((shape + 0.3f * ContactScore(r, c, group.Id) + (float)rng.NextDouble() * 1.5f, new GridPos(r, c)));
                        }
                    }

                    tries.Sort((a, b) => b.score.CompareTo(a.score));
                    for (int i = 0; i < tries.Count && i < FastTries; i++)
                    {
                        if (!Place(tries[i].cell)) continue;
                        CoreTurnActions.ConsumeHand(State);
                        return true;
                    }

                    // Sin jugada: gasta un Re-Draw o muere (muerte subita)
                    if (Profile.ReDraws <= 0 || redraws++ >= 3) return false;
                    CoreDrawProcessor.ProcessReDrawIntent(State, new ReDrawCommand { PlayerId = 0 }, State.ServerRNG);
                }
            }

            // Prefiere casillas pegadas al propio grupo y a filas/columnas ya avanzadas
            private float ContactScore(int r, int c, int groupId)
            {
                float score = 0f;
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        if (i == 0 && j == 0) continue;
                        int nr = r + i, nc = c + j;
                        if (nr < 0 || nr >= Board.Rows || nc < 0 || nc >= Board.Cols) continue;
                        CellStateDTO n = Board.Cells[Board.GetIndex(nr, nc)];
                        if (!n.IsOccupied) continue;
                        score += n.GroupId == groupId ? ((i == 0 || j == 0) ? 3f : 1f) : 0.5f;
                    }
                }

                return score + 0.2f * LineProgress(r, c);
            }

            private float LineProgress(int r, int c)
            {
                int rowFilled = 0, colFilled = 0;
                for (int k = 0; k < Board.Cols; k++) if (Board.Cells[Board.GetIndex(r, k)].IsOccupied) rowFilled++;
                for (int k = 0; k < Board.Rows; k++) if (Board.Cells[Board.GetIndex(k, c)].IsOccupied) colFilled++;
                float rf = rowFilled / (float)Board.Cols;
                float cf = colFilled / (float)Board.Rows;
                return rf * rf + cf * cf;
            }

            /// <summary>
            /// Valoracion posicional del tablero actual: huecos cerrados (que penalizan al acabar), grupos condenados y
            /// avance de la fila y columna de la ultima casilla (si se indica).
            /// </summary>
            public float Heuristic(int lastRow, int lastCol)
            {
                float h = 0.5f * CoreTopologyCalculator.CalculateGapPenalty(Board.Cells, Board.Rows, Board.Cols, State.ScoringConfig, out _);

                foreach (var kvp in Profile.ActiveGroups)
                {
                    GroupDataDTO g = kvp.Value;
                    if (g != null && CoreTopologyCalculator.IsGroupDoomed(Board, g, Pattern(g.TargetSize))) h -= 150f;
                }

                if (lastRow >= 0)
                {
                    int rowFilled = 0, colFilled = 0;
                    for (int k = 0; k < Board.Cols; k++) if (Board.Cells[Board.GetIndex(lastRow, k)].IsOccupied) rowFilled++;
                    for (int k = 0; k < Board.Rows; k++) if (Board.Cells[Board.GetIndex(k, lastCol)].IsOccupied) colFilled++;
                    float rf = rowFilled / (float)Board.Cols;
                    float cf = colFilled / (float)Board.Rows;
                    h += LineHint * (rf * rf + cf * cf);
                }

                return h;
            }

            /// <summary>
            /// Valor "a medias" de los patrones que los grupos abiertos todavia pueden completar: el bono del patron por lo
            /// avanzado que va el grupo, rebajado si queda una sola forma posible. Un grupo que ya no puede formar su
            /// patron no aporta nada.
            /// </summary>
            public float PatternPotential()
            {
                float potential = 0f;
                foreach (var kvp in Profile.ActiveGroups)
                {
                    GroupDataDTO g = kvp.Value;
                    if (g == null || g.IsClosed || g.OccupiedCells.Count == 0) continue;

                    PatternDefDTO pattern = Pattern(g.TargetSize);
                    if (pattern == null || pattern.TargetNumber == 1) continue;

                    PatternPlan plan = CorePatternPlanner.Plan(Board, g.OccupiedCells, g.Color, g.Id, pattern);
                    if (!plan.Feasible) continue;

                    float progress = g.OccupiedCells.Count / (float)pattern.BaseShape.Count;
                    float robust = Math.Min(1f, 0.55f + 0.15f * Math.Min(plan.Placements, 3));
                    potential += State.ScoringConfig.GetPatternBonus(g.TargetSize) * progress * robust * PatternWeight;
                }

                return potential;
            }
        }
    }
}
