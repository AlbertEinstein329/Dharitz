using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>
    /// Un grupo abierto rodeado de dados no puede completarse: no debe bloquear el resto del tablero ni dejar a su color sin jugada.
    /// </summary>
    public class DoomedGroupTests
    {
        private const int Rows = 10;
        private const int Cols = 8;

        private static void Place(BoardStateDTO board, int r, int c, DieColor color, int groupId, int value) =>
            board.Cells[board.GetIndex(r, c)] = new CellStateDTO { IsOccupied = true, Color = color, GroupId = groupId, Value = value };

        // Rojo de 2 dados con un solo dado en (5,5) y sus 4 vecinos ortogonales ocupados; Azul de 3 dados con uno en (1,1)
        private static (BoardStateDTO board, PlayerDataDTO player) Scenario()
        {
            var board = new BoardStateDTO(Rows, Cols);
            var player = new PlayerDataDTO(0, "P1");

            Place(board, 5, 5, DieColor.Red, 1, 2);
            Place(board, 4, 5, DieColor.White, 90, 1);
            Place(board, 6, 5, DieColor.White, 91, 1);
            Place(board, 5, 4, DieColor.White, 92, 1);
            Place(board, 5, 6, DieColor.White, 93, 1);
            var red = new GroupDataDTO(1, DieColor.Red, 2);
            red.OccupiedCells.Add(new GridPos(5, 5));
            player.ActiveGroups[DieColor.Red] = red;

            Place(board, 1, 1, DieColor.Blue, 2, 3);
            var blue = new GroupDataDTO(2, DieColor.Blue, 3);
            blue.OccupiedCells.Add(new GridPos(1, 1));
            player.ActiveGroups[DieColor.Blue] = blue;

            return (board, player);
        }

        [Test]
        public void EnclosedGroup_IsDoomed_AndFreeGroupIsNot()
        {
            var (board, player) = Scenario();
            Assert.IsTrue(CoreTopologyCalculator.IsGroupDoomed(board, player.ActiveGroups[DieColor.Red], null));
            Assert.IsFalse(CoreTopologyCalculator.IsGroupDoomed(board, player.ActiveGroups[DieColor.Blue], null));
        }

        [Test]
        public void DoomedGroup_DoesNotBlockOtherPlacements()
        {
            var (board, player) = Scenario();
            // Continuar el grupo Azul en (1,2) es legal aunque el Rojo ya no se pueda completar
            Assert.IsTrue(CoreTopologyCalculator.ValidateSurvival(board, 1, 2, DieColor.Blue, 2, 3, player, null, null));
        }

        [Test]
        public void AbandonDoomedGroups_RemovesOnlyTheDoomedOnes()
        {
            var (board, player) = Scenario();
            var state = new MatchStateDTO();
            state.PlayerBoards[0] = board;
            state.PlayerProfiles[0] = player;

            Assert.AreEqual(1, CoreTopologyCalculator.AbandonDoomedGroups(state, 0));
            Assert.IsFalse(player.ActiveGroups.ContainsKey(DieColor.Red));
            Assert.IsTrue(player.ActiveGroups.ContainsKey(DieColor.Blue));
        }
    }
}
