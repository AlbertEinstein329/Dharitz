using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>
    /// El highlight, el sonido «Nice» y el bonus de patron solo deben darse cuando la FORMA del grupo coincide con el
    /// patron de la variante, no por haber colocado todos los dados del grupo.
    /// </summary>
    public class PatternHighlightTests
    {
        private const int Rows = 10;
        private const int Cols = 8;

        // Patron en «L» de 3 dados (rotable y espejable), como el de la Variante_1_Base
        private static PatternDefDTO LPattern() => new PatternDefDTO
        {
            TargetNumber = 3,
            AllowRotation = true,
            AllowMirror = true,
            BaseShape = new List<GridPos> { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1) }
        };

        // Patron en linea vertical de 3 dados, SIN rotacion (como el de la Variante_3)
        private static PatternDefDTO VerticalLinePattern() => new PatternDefDTO
        {
            TargetNumber = 3,
            AllowRotation = false,
            AllowMirror = true,
            BaseShape = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) }
        };

        private static VariantDefDTO Variant(PatternDefDTO pattern)
        {
            var variant = new VariantDefDTO { VariantName = "test" };
            variant.Patterns[pattern.TargetNumber] = pattern;
            return variant;
        }

        private static MatchStateDTO NewMatch(PatternDefDTO pattern)
        {
            MatchStateDTO state = CoreMatchSetup.CreateMatch(1, new List<string> { "A" }, Rows, Cols, Variant(pattern));
            state.CurrentPhase = MatchPhase.PlayerTurn;
            return state;
        }

        private static PlacementResult Place(MatchStateDTO state, PatternDefDTO pattern, int row, int col, out bool accepted)
        {
            ScoringConfigDTO sc = state.ScoringConfig;
            var command = new PlaceDieCommand
            {
                PlayerId = 0,
                TargetCell = new GridPos(row, col),
                Color = DieColor.Red,
                GroupId = 7,
                Number = pattern.TargetNumber
            };

            accepted = CoreMatchProcessor.ProcessPlacementIntent(
                state, command, pattern, pattern,
                sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers,
                out PlacementResult result);
            return result;
        }

        private static List<GridPos> Cells(params (int, int)[] points)
        {
            var list = new List<GridPos>();
            foreach (var (row, col) in points) list.Add(new GridPos(row, col));
            return list;
        }

        // ------------------------------------------------------------------ CorePatternValidator

        [Test]
        public void CheckPattern_AcceptsRotatedMirroredAndTranslatedShapes()
        {
            PatternDefDTO l = LPattern();

            Assert.IsTrue(CorePatternValidator.CheckPattern(Cells((3, 3), (3, 4), (4, 4)), l), "forma base");
            Assert.IsTrue(CorePatternValidator.CheckPattern(Cells((5, 1), (6, 1), (6, 0)), l), "rotada/espejada y trasladada");
            Assert.IsTrue(CorePatternValidator.CheckPattern(Cells((0, 0), (1, 0), (1, 1)), l), "otra orientacion");
        }

        [Test]
        public void CheckPattern_RejectsAStraightLineForAnLPattern()
        {
            PatternDefDTO l = LPattern();

            Assert.IsFalse(CorePatternValidator.CheckPattern(Cells((3, 3), (3, 4), (3, 5)), l));
            Assert.IsFalse(CorePatternValidator.CheckPattern(Cells((3, 3), (4, 3), (5, 3)), l));
        }

        [Test]
        public void CheckPattern_RespectsDisabledRotation()
        {
            PatternDefDTO vertical = VerticalLinePattern();

            Assert.IsTrue(CorePatternValidator.CheckPattern(Cells((2, 5), (3, 5), (4, 5)), vertical), "vertical");
            Assert.IsFalse(CorePatternValidator.CheckPattern(Cells((2, 5), (2, 6), (2, 7)), vertical), "horizontal no vale sin rotacion");
        }

        // ------------------------------------------------------------------ Colocar

        [Test]
        public void Placement_LineForLPattern_ClosesGroupButDoesNotMatchOrAwardBonus()
        {
            PatternDefDTO l = LPattern();
            MatchStateDTO state = NewMatch(l);

            Place(state, l, 3, 3, out bool a1);
            Place(state, l, 3, 4, out bool a2);
            int scoreBeforeLast = state.PlayerProfiles[0].Score;
            PlacementResult last = Place(state, l, 3, 5, out bool a3);

            Assert.IsTrue(a1 && a2 && a3, "el motor acepta los tres dados");
            Assert.IsTrue(last.GroupClosed, "el grupo se cierra por cantidad");
            Assert.IsFalse(last.PatternMatched, "pero la forma NO es la del patron");
            Assert.AreEqual(0, last.PatternBonus);
            Assert.AreEqual(50, state.PlayerProfiles[0].Score - scoreBeforeLast, "solo los 50 base, sin bonus de patron");
        }

        [Test]
        public void Placement_LShape_MatchesAndAwardsPatternBonus()
        {
            PatternDefDTO l = LPattern();
            MatchStateDTO state = NewMatch(l);

            Place(state, l, 3, 3, out _);
            PlacementResult second = Place(state, l, 3, 4, out _);
            PlacementResult last = Place(state, l, 4, 4, out bool accepted);

            Assert.IsTrue(accepted);
            Assert.IsFalse(second.GroupClosed);
            Assert.IsFalse(second.PatternMatched, "con 2 de 3 dados aun no hay patron");
            Assert.IsTrue(last.GroupClosed);
            Assert.IsTrue(last.PatternMatched, "la forma coincide con el patron");
            Assert.AreEqual(200, last.PatternBonus);
        }

        [Test]
        public void Placement_RejectedMove_ReportsNoSuccess()
        {
            PatternDefDTO l = LPattern();
            MatchStateDTO state = NewMatch(l);

            PlacementResult result = Place(state, l, 99, 99, out bool accepted);

            Assert.IsFalse(accepted);
            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.GroupClosed);
            Assert.IsFalse(result.PatternMatched);
        }

        // ------------------------------------------------------------------ CoreCompletion (overlay / reconexion)

        [Test]
        public void FindMatchedCells_ReturnsOnlyCellsOfCompletedMatchingGroups()
        {
            PatternDefDTO l = LPattern();
            MatchStateDTO state = NewMatch(l);
            Place(state, l, 3, 3, out _);
            Place(state, l, 3, 4, out _);
            Place(state, l, 4, 4, out _);

            BoardStateDTO board = state.PlayerBoards[0];
            List<int> matched = CoreCompletion.FindMatchedCells(board, state.VariantConfig);

            CollectionAssert.AreEquivalent(new[] { board.GetIndex(3, 3), board.GetIndex(3, 4), board.GetIndex(4, 4) }, matched);
        }

        [Test]
        public void FindMatchedCells_IgnoresCompletedGroupsWithAWrongShapeAndIncompleteGroups()
        {
            PatternDefDTO l = LPattern();

            MatchStateDTO line = NewMatch(l);
            Place(line, l, 3, 3, out _);
            Place(line, l, 3, 4, out _);
            Place(line, l, 3, 5, out _);
            Assert.IsEmpty(CoreCompletion.FindMatchedCells(line.PlayerBoards[0], line.VariantConfig), "linea completa pero forma incorrecta");

            MatchStateDTO partial = NewMatch(l);
            Place(partial, l, 3, 3, out _);
            Place(partial, l, 3, 4, out _);
            Assert.IsEmpty(CoreCompletion.FindMatchedCells(partial.PlayerBoards[0], partial.VariantConfig), "grupo incompleto");
        }

        [Test]
        public void FindMatchedCells_WithoutVariant_ReturnsNothing()
        {
            Assert.IsEmpty(CoreCompletion.FindMatchedCells(new BoardStateDTO(Rows, Cols), null));
        }

        // ------------------------------------------------------------------ Mover

        [Test]
        public void Move_ThatBreaksTheShape_DoesNotCountAsPatternAndMoveThatFormsItDoes()
        {
            PatternDefDTO l = LPattern();
            MatchStateDTO state = NewMatch(l);
            Place(state, l, 3, 3, out _);
            Place(state, l, 3, 4, out _);
            Place(state, l, 3, 5, out _); // linea: grupo lleno pero sin patron

            var history = new List<TurnCheckpoint>();

            // Mover el extremo (3,5) a (4,4) forma la «L» (3,3),(3,4),(4,4)
            MoveExecutionResult forms = CoreTurnActions.TryMove(state, 0, 3, 5, 4, 4, l, history);
            Assert.IsTrue(forms.IsValid, "el movimiento es legal");
            Assert.IsTrue(forms.PatternCompleted, "ahora la forma coincide");
            Assert.AreEqual(100, forms.PointsAwarded);

            // Deshacer lo devuelve a la linea sin patron
            Assert.IsTrue(CoreTurnActions.TryUndo(state, 0, history));
            Assert.IsEmpty(CoreCompletion.FindMatchedCells(state.PlayerBoards[0], state.VariantConfig));
        }

        [Test]
        public void Move_InsideAFullGroupWithWrongShape_DoesNotAwardPoints()
        {
            PatternDefDTO l = LPattern();
            MatchStateDTO state = NewMatch(l);
            Place(state, l, 3, 3, out _);
            Place(state, l, 3, 4, out _);
            Place(state, l, 3, 5, out _); // linea completa, sin patron
            int scoreBefore = state.PlayerProfiles[0].Score;

            // Mover el extremo (3,3) a (3,6) deja otra linea: grupo lleno pero sin la forma del patron
            var history = new List<TurnCheckpoint>();
            MoveExecutionResult toLine = CoreTurnActions.TryMove(state, 0, 3, 3, 3, 6, l, history);

            Assert.IsTrue(toLine.IsValid, "el movimiento es legal (la linea sigue conectada)");
            Assert.IsFalse(toLine.PatternCompleted, "sigue siendo una linea: sin patron");
            Assert.AreEqual(scoreBefore, state.PlayerProfiles[0].Score, "sin puntos por mover dentro de un grupo sin forma");
        }
    }
}
