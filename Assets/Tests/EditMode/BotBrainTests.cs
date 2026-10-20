using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>El bot solo debe proponer jugadas que el juego acepte, y avisar cuando no hay ninguna.</summary>
    public class BotBrainTests
    {
        private static MatchStateDTO NewMatch(int seed) =>
            CoreMatchSetup.CreateMatch(seed, new List<string> { "Bot" }, 10, 8, new VariantDefDTO { VariantName = "Test" });

        private static bool PlayWholeGame(int seed, BotLevel level, out int illegalProposals)
        {
            MatchStateDTO st = NewMatch(seed);
            illegalProposals = 0;

            for (int turn = 0; turn < 60 && st.PlayerProfiles[0].PlacedDice < 52; turn++)
            {
                if (!CoreDrawProcessor.ProcessDrawIntent(st, new DrawDieCommand { PlayerId = 0 }, st.ServerRNG)) return true;

                for (int redraws = 0; ; redraws++)
                {
                    BotSnapshot snap = BotSnapshot.Capture(st, 0, 52);
                    BotDecision d = BotBrain.Choose(snap, level, seed + turn, 40);

                    if (d.HasMove)
                    {
                        DieColor color = st.CurrentDrawnColor.Value;
                        int value = st.CurrentDrawnValue.Value;
                        var cmd = new PlaceDieCommand { PlayerId = 0, TargetCell = d.Cell, Color = color, GroupId = st.PlayerProfiles[0].ActiveGroups[color].Id, Number = value };
                        ScoringConfigDTO sc = st.ScoringConfig;
                        if (!CoreMatchProcessor.ProcessPlacementIntent(st, cmd, null, null, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers))
                            illegalProposals++;
                        else
                            CoreTurnActions.ConsumeHand(st);
                        break;
                    }

                    if (st.PlayerProfiles[0].ReDraws <= 0 || redraws >= 5) return false; // muerte subita
                    CoreDrawProcessor.ProcessReDrawIntent(st, new ReDrawCommand { PlayerId = 0 }, st.ServerRNG);
                    if (!CoreDrawProcessor.ProcessDrawIntent(st, new DrawDieCommand { PlayerId = 0 }, st.ServerRNG)) return true;
                }
            }

            return true;
        }

        [TestCase(BotLevel.Greedy)]
        [TestCase(BotLevel.GreedyPenalized)]
        [TestCase(BotLevel.MonteCarlo)]
        public void Bot_NeverProposesAnIllegalMove(BotLevel level)
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                PlayWholeGame(seed * 101, level, out int illegal);
                Assert.AreEqual(0, illegal, $"{level} propuso una jugada ilegal (seed {seed * 101})");
            }
        }

        [Test]
        public void FirstDie_CanGoAnywhereOnTheBoard()
        {
            MatchStateDTO st = NewMatch(5);
            CoreDrawProcessor.ProcessDrawIntent(st, new DrawDieCommand { PlayerId = 0 }, st.ServerRNG);

            BotDecision d = BotBrain.Choose(BotSnapshot.Capture(st, 0, 52), BotLevel.Greedy, 1);

            Assert.IsTrue(d.HasMove);
            Assert.AreEqual(80, d.LegalCells);
        }

        [Test]
        public void NoMoves_WhenEveryEmptyCellIsOutOfReach()
        {
            MatchStateDTO st = NewMatch(6);
            BoardStateDTO board = st.PlayerBoards[0];
            PlayerDataDTO player = st.PlayerProfiles[0];

            // Tablero lleno salvo una esquina: el dado en mano es de otro color y la esquina toca un dado de su mismo color y otro grupo
            for (int i = 0; i < board.Cells.Length; i++)
                board.Cells[i] = new CellStateDTO { IsOccupied = true, Color = DieColor.White, GroupId = 900 + i, Value = 1 };
            board.Cells[board.GetIndex(0, 0)] = default;
            player.PlacedDice = 79;

            st.HasDrawn = true;
            st.CurrentDrawnColor = DieColor.White;
            st.CurrentDrawnValue = 1;
            player.ActiveGroups[DieColor.White] = new GroupDataDTO(1, DieColor.White, 1);

            BotDecision d = BotBrain.Choose(BotSnapshot.Capture(st, 0, 52), BotLevel.GreedyPenalized, 1);

            Assert.IsFalse(d.HasMove);
        }
    }
}
