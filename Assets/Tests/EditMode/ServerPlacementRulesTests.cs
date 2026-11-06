using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>
    /// El servidor (ProcessPlacementIntent) impone las reglas de contacto: un cliente alterado no puede colocar un dado
    /// donde las reglas no lo permiten.
    /// </summary>
    public class ServerPlacementRulesTests
    {
        private static MatchStateDTO NewMatch() =>
            CoreMatchSetup.CreateMatch(9, new List<string> { "A" }, 10, 8, new VariantDefDTO());

        private static bool Place(MatchStateDTO st, int row, int col, DieColor color, int groupId, int number, PatternDefDTO pattern = null)
        {
            ScoringConfigDTO sc = st.ScoringConfig;
            var cmd = new PlaceDieCommand { PlayerId = 0, TargetCell = new GridPos(row, col), Color = color, GroupId = groupId, Number = number };
            return CoreMatchProcessor.ProcessPlacementIntent(st, cmd, pattern, pattern, sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus, sc.RowMultipliers, sc.ColMultipliers);
        }

        // En el juego, robar un dado de un color cuyo grupo ya esta cerrado abre un grupo nuevo; aqui se imita para que cada
        // test pruebe la regla que dice probar y no el grupo cerrado que se quedaria en el estado
        private static void OpenNewGroup(MatchStateDTO st, DieColor color) => st.PlayerProfiles[0].ActiveGroups.Remove(color);

        [Test]
        public void FirstDie_CanGoAnywhere()
        {
            Assert.IsTrue(Place(NewMatch(), 6, 6, DieColor.Red, 1, 3));
        }

        [Test]
        public void SecondDie_FarFromEveryDie_IsRejected()
        {
            MatchStateDTO st = NewMatch();
            Assert.IsTrue(Place(st, 2, 2, DieColor.Red, 1, 3));

            Assert.IsFalse(Place(st, 8, 7, DieColor.Blue, 2, 3), "aislado: no toca ningun dado");
            Assert.AreEqual(1, st.PlayerProfiles[0].PlacedDice, "el rechazo no cambia el estado");
        }

        [Test]
        public void DieOfAnOpenGroup_MustTouchItsOwnGroup()
        {
            MatchStateDTO st = NewMatch();
            Assert.IsTrue(Place(st, 2, 2, DieColor.Red, 1, 3));
            Assert.IsFalse(Place(st, 7, 7, DieColor.Blue, 2, 3), "otro color lejos: rechazado");

            // Un dado de otro color pegado al primero si vale; pero el siguiente Rojo del grupo 1 debe tocar al Rojo anterior
            Assert.IsTrue(Place(st, 2, 3, DieColor.Blue, 2, 3));
            Assert.IsFalse(Place(st, 5, 5, DieColor.Red, 1, 3), "Rojo del grupo 1 lejos de su grupo");
            Assert.IsTrue(Place(st, 3, 2, DieColor.Red, 1, 3), "Rojo del grupo 1 tocando a su grupo");
        }

        [Test]
        public void DieCannotTouchOrthogonally_AnotherGroupOfTheSameColor()
        {
            MatchStateDTO st = NewMatch();
            Assert.IsTrue(Place(st, 4, 4, DieColor.Red, 1, 1));   // grupo de 1 dado (se cierra)
            OpenNewGroup(st, DieColor.Red);

            // Otro grupo Rojo (id 2) pegado en cruz al anterior: ilegal
            Assert.IsFalse(Place(st, 4, 5, DieColor.Red, 2, 2));
            // Lo mismo en diagonal, tocando a un dado de otro color, si es legal
            Assert.IsTrue(Place(st, 5, 4, DieColor.Blue, 3, 2));
            Assert.IsTrue(Place(st, 5, 5, DieColor.Red, 2, 2));
        }

        [Test]
        public void OnesCannotTouch_WhenTheVariantPenalizesContact()
        {
            var penalize = new PatternDefDTO { TargetNumber = 1, SpecialRule = 1, BaseShape = new List<GridPos> { new GridPos(0, 0) } };

            // Un 1 Rojo en (4,4) y un dado Azul en (5,4); un segundo 1 Rojo en (5,5) toca al Azul (valido por contacto)
            // pero queda en diagonal de otro 1 Rojo: eso solo lo prohibe la regla del 1.
            MatchStateDTO st = NewMatch();
            Assert.IsTrue(Place(st, 4, 4, DieColor.Red, 1, 1, penalize));
            Assert.IsTrue(Place(st, 5, 4, DieColor.Blue, 2, 1, penalize));
            OpenNewGroup(st, DieColor.Red);
            Assert.IsFalse(Place(st, 5, 5, DieColor.Red, 3, 1, penalize), "dos 1 del mismo color en diagonal: regla del 1");

            // Control: la misma jugada sin esa regla es legal
            MatchStateDTO control = NewMatch();
            Assert.IsTrue(Place(control, 4, 4, DieColor.Red, 1, 1));
            Assert.IsTrue(Place(control, 5, 4, DieColor.Blue, 2, 1));
            OpenNewGroup(control, DieColor.Red);
            Assert.IsTrue(Place(control, 5, 5, DieColor.Red, 3, 1));
        }
    }
}
