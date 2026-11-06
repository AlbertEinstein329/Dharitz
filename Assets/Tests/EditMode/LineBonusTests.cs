using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>El bono de una fila o columna completa se cobra una sola vez, no con cada dado posterior.</summary>
    public class LineBonusTests
    {
        private static int Place(MatchStateDTO st, int row, int col, int groupId, DieColor color = DieColor.Blue)
        {
            PlayerDataDTO p = st.PlayerProfiles[0];
            int before = p.Score;
            ScoringConfigDTO sc = st.ScoringConfig;
            var cmd = new PlaceDieCommand { PlayerId = 0, TargetCell = new GridPos(row, col), Color = color, GroupId = groupId, Number = 1 };
            Assert.IsTrue(CoreMatchProcessor.ProcessPlacementIntent(st, cmd, null, null, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers));
            return p.Score - before;
        }

        [Test]
        public void CompletedRow_PaysItsBonusOnce()
        {
            MatchStateDTO st = CoreMatchSetup.CreateMatch(1, new List<string> { "A" }, 10, 8, new VariantDefDTO());
            BoardStateDTO board = st.PlayerBoards[0];
            for (int c = 0; c < 7; c++)
                board.Cells[board.GetIndex(0, c)] = new CellStateDTO { IsOccupied = true, Color = DieColor.White, GroupId = 500 + c, Value = 1 };

            int closing = Place(st, 0, 7, 1);   // cierra la fila 0
            // Dados siguientes: tienen que tocar algun dado (el servidor ya exige el contacto) y no pegarse a otro grupo de su color
            int next = Place(st, 1, 7, 2, DieColor.Red);
            int another = Place(st, 1, 6, 3, DieColor.Black);

            Assert.AreEqual(st.ScoringConfig.PointsPerDie + st.ScoringConfig.RowCompleteBonus, closing);
            Assert.AreEqual(st.ScoringConfig.PointsPerDie, next);
            Assert.AreEqual(st.ScoringConfig.PointsPerDie, another);
        }

        [Test]
        public void ProfileCopiedBetweenTurns_KeepsTheAccumulatedStructurePoints()
        {
            // Lo que hace la capa visual en local: cada turno el perfil se reconstruye con ToDTO y se copia de vuelta
            MatchStateDTO st = CoreMatchSetup.CreateMatch(2, new List<string> { "A" }, 10, 8, new VariantDefDTO());
            BoardStateDTO board = st.PlayerBoards[0];
            for (int c = 0; c < 7; c++)
                board.Cells[board.GetIndex(0, c)] = new CellStateDTO { IsOccupied = true, Color = DieColor.White, GroupId = 500 + c, Value = 1 };

            Place(st, 0, 7, 1);
            int accumulated = st.PlayerProfiles[0].AccumulatedStructurePoints;
            Assert.Greater(accumulated, 0);

            // Turno siguiente con el valor acumulado conservado: no se vuelve a pagar
            st.PlayerProfiles[0] = CoreTurnActions.CloneProfile(st.PlayerProfiles[0]);
            Assert.AreEqual(st.ScoringConfig.PointsPerDie, Place(st, 1, 7, 2, DieColor.Red));
            Assert.AreEqual(accumulated, st.PlayerProfiles[0].AccumulatedStructurePoints);
        }
    }
}
