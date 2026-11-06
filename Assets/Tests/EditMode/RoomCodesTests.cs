using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>El codigo de sala lleva el numero de jugadores y tolera como lo escriba el jugador.</summary>
    public class RoomCodesTests
    {
        [Test]
        public void Format_AddsThePlayersAfterAHyphen_InUpperCase()
        {
            Assert.AreEqual("K7M2QX-2", RoomCodes.Format("k7m2qx", 2));
            Assert.AreEqual("ABC123-4", RoomCodes.Format(" abc123 ", 4));
        }

        [TestCase("K7M2QX-2", "K7M2QX", 2)]
        [TestCase("k7m2qx-4", "K7M2QX", 4)]
        [TestCase("  K7M2QX 2 ", "K7M2QX", 2)]
        [TestCase("K7M2QX2", "K7M2QX", 2)]
        [TestCase("k7-m2-qx-4", "K7M2QX", 4)]
        public void TryParse_AcceptsHowPlayersTypeIt(string input, string expectedCode, int expectedPlayers)
        {
            Assert.IsTrue(RoomCodes.TryParse(input, out string code, out int players));
            Assert.AreEqual(expectedCode, code);
            Assert.AreEqual(expectedPlayers, players);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("K7M2QX")]       // sin numero de jugadores y termina en letra
        [TestCase("K7M2QX-3")]     // 3 jugadores no existe
        [TestCase("AB-2")]         // codigo demasiado corto
        [TestCase("K7M2QXZZZZZZZZ-2")] // demasiado largo
        [TestCase("127.0.0.1")]    // una IP no es un codigo valido
        public void TryParse_RejectsWhatIsNotACode(string input)
        {
            Assert.IsFalse(RoomCodes.TryParse(input, out string code, out int players));
            Assert.IsNull(code);
            Assert.AreEqual(0, players);
        }

        [Test]
        public void FormatThenParse_RoundTrips()
        {
            string shown = RoomCodes.Format("Q9X4TB", 4);

            Assert.IsTrue(RoomCodes.TryParse(shown, out string code, out int players));
            Assert.AreEqual("Q9X4TB", code);
            Assert.AreEqual(4, players);
        }
    }
}
