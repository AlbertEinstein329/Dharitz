using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>El planificador sabe si un grupo abierto aun puede cerrar con la forma exacta del patron.</summary>
    public class PatternPlannerTests
    {
        private const int Rows = 10;
        private const int Cols = 8;

        // L de 3 dados, rotable y espejable
        private static PatternDefDTO L() => new PatternDefDTO
        {
            TargetNumber = 3,
            AllowRotation = true,
            AllowMirror = true,
            BaseShape = new List<GridPos> { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1) }
        };

        // Linea vertical de 3 dados SIN rotacion
        private static PatternDefDTO VerticalLine() => new PatternDefDTO
        {
            TargetNumber = 3,
            AllowRotation = false,
            AllowMirror = true,
            BaseShape = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) }
        };

        private static void Put(BoardStateDTO board, int r, int c, DieColor color, int groupId) =>
            board.Cells[board.GetIndex(r, c)] = new CellStateDTO { IsOccupied = true, Color = color, GroupId = groupId, Value = 3 };

        [Test]
        public void TwoCellsOfAnL_AreFeasible_AndTheTargetsCompleteIt()
        {
            var board = new BoardStateDTO(Rows, Cols);
            Put(board, 4, 4, DieColor.Red, 1);
            Put(board, 4, 5, DieColor.Red, 1);
            var group = new List<GridPos> { new GridPos(4, 4), new GridPos(4, 5) };

            PatternPlan plan = CorePatternPlanner.Plan(board, group, DieColor.Red, 1, L());

            Assert.IsTrue(plan.Feasible);
            Assert.AreEqual(1, plan.Remaining);
            // La L se cierra poniendo el tercer dado encima o debajo de cualquiera de los dos extremos
            Assert.IsTrue(plan.TargetCells.Contains(board.GetIndex(5, 5)));
            Assert.IsTrue(plan.TargetCells.Contains(board.GetIndex(3, 4)));
            Assert.IsFalse(plan.TargetCells.Contains(board.GetIndex(4, 6)), "una linea no es una L");
        }

        [Test]
        public void HorizontalPair_CannotBecomeAVerticalLineThatDoesNotRotate()
        {
            var board = new BoardStateDTO(Rows, Cols);
            Put(board, 4, 4, DieColor.Red, 1);
            Put(board, 4, 5, DieColor.Red, 1);
            var group = new List<GridPos> { new GridPos(4, 4), new GridPos(4, 5) };

            PatternPlan plan = CorePatternPlanner.Plan(board, group, DieColor.Red, 1, VerticalLine());

            Assert.IsFalse(plan.Feasible);
        }

        [Test]
        public void BlockedCompletionCells_MakeTheGroupInfeasible()
        {
            var board = new BoardStateDTO(Rows, Cols);
            Put(board, 0, 0, DieColor.Red, 1);
            Put(board, 1, 0, DieColor.Red, 1);
            // Debajo del par vertical hay un dado, y encima no cabe (esta en el borde): la linea de 3 ya no cabe
            Put(board, 2, 0, DieColor.White, 9);
            var group = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };

            PatternPlan plan = CorePatternPlanner.Plan(board, group, DieColor.Red, 1, VerticalLine());

            Assert.IsFalse(plan.Feasible);
        }

        [Test]
        public void NewGroup_PrefersTheMiddleOfTheBoardOverACorner()
        {
            var board = new BoardStateDTO(Rows, Cols);
            var perCell = new int[board.Cells.Length];

            PatternPlan plan = CorePatternPlanner.Plan(board, new List<GridPos>(), DieColor.Blue, 7, L(), perCell);

            Assert.IsTrue(plan.Feasible);
            Assert.Greater(perCell[board.GetIndex(5, 4)], perCell[board.GetIndex(0, 0)]);
        }

        [Test]
        public void OneDicePattern_IsAlwaysFeasible()
        {
            var board = new BoardStateDTO(Rows, Cols);
            var one = new PatternDefDTO { TargetNumber = 1, BaseShape = new List<GridPos> { new GridPos(0, 0) } };

            Assert.IsTrue(CorePatternPlanner.Plan(board, new List<GridPos>(), DieColor.Red, 1, one).Feasible);
        }
    }
}
