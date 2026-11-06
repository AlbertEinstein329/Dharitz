using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>Puntuacion configurable: topes de huecos, bono de coleccion y reversion al deshacer.</summary>
    public class ScoringConfigTests
    {
        [Test]
        public void GapPenalty_IsCapped_ButTheLegacyTiersAreNot()
        {
            var sizes = new List<int> { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            Assert.AreEqual(-1500, ScoringConfigDTO.Default().GapPenaltyTotal(sizes));
            Assert.AreEqual(-5000, ScoringConfigDTO.Legacy().GapPenaltyTotal(sizes));
        }

        [Test]
        public void GapPenalty_ScalesWithTheSizeOfTheGap()
        {
            ScoringConfigDTO sc = ScoringConfigDTO.Default();

            Assert.AreEqual(-300, sc.GapPenaltyFor(1));
            Assert.AreEqual(-900, sc.GapPenaltyFor(3));
        }

        [Test]
        public void CollectionBonus_IsAwardedOnceWhenTheSixSizesAreCompleted_AndRevokedWithTheLastOne()
        {
            ScoringConfigDTO sc = ScoringConfigDTO.Default();
            var player = new PlayerDataDTO(0, "A");

            for (int size = 1; size <= 5; size++) Assert.AreEqual(sc.GetPatternBonus(size), CoreScoring.AwardPattern(player, size, sc));

            Assert.AreEqual(sc.GetPatternBonus(6) + sc.CollectionBonus, CoreScoring.AwardPattern(player, 6, sc), "el sexto tamano trae la coleccion");
            Assert.AreEqual(sc.GetPatternBonus(6), CoreScoring.AwardPattern(player, 6, sc), "repetir un tamano ya no la da");

            Assert.AreEqual(sc.GetPatternBonus(6), CoreScoring.RevokePattern(player, 6, sc));
            Assert.AreEqual(sc.GetPatternBonus(6) + sc.CollectionBonus, CoreScoring.RevokePattern(player, 6, sc));
        }

        [Test]
        public void Undo_OfTheDieThatClosedARow_GivesBackTheDieAndTheRowBonus()
        {
            MatchStateDTO st = CoreMatchSetup.CreateMatch(3, new List<string> { "A" }, 10, 8, new VariantDefDTO());
            BoardStateDTO board = st.PlayerBoards[0];
            for (int c = 0; c < 7; c++)
                board.Cells[board.GetIndex(0, c)] = new CellStateDTO { IsOccupied = true, Color = DieColor.White, GroupId = 500 + c, Value = 1 };

            ScoringConfigDTO sc = st.ScoringConfig;
            int before = st.PlayerProfiles[0].Score;
            var cmd = new PlaceDieCommand { PlayerId = 0, TargetCell = new GridPos(0, 7), Color = DieColor.Blue, GroupId = 1, Number = 1 };
            Assert.IsTrue(CoreMatchProcessor.ProcessPlacementIntent(st, cmd, null, null, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers));
            Assert.Greater(st.PlayerProfiles[0].Score, before);

            var undo = new UndoDieCommand { PlayerId = 0, TargetCell = cmd.TargetCell, Color = cmd.Color, GroupId = cmd.GroupId, Number = cmd.Number };
            Assert.IsTrue(CoreUndoProcessor.ProcessUndoIntent(st, undo, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers));

            Assert.AreEqual(before, st.PlayerProfiles[0].Score);
            Assert.AreEqual(0, st.PlayerProfiles[0].AccumulatedStructurePoints);
        }
    }
}
