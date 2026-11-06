using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>Conocer el PlayerId de otro jugador no basta para quedarse con su asiento: hace falta su token de reconexion.</summary>
    public class ReconnectTokensTests
    {
        [Test]
        public void IssuedToken_IsRandomHex_AndVerifiesForItsOwner()
        {
            var tokens = new ReconnectTokens();
            string a = tokens.Issue("player-a");

            Assert.AreEqual(32, a.Length);
            StringAssert.IsMatch("^[0-9a-f]{32}$", a);
            Assert.IsTrue(tokens.Verify("player-a", a));
        }

        [Test]
        public void TokensAreDifferentForEveryPlayerAndEveryIssue()
        {
            var tokens = new ReconnectTokens();
            string a = tokens.Issue("player-a");
            string b = tokens.Issue("player-b");

            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, tokens.Issue("player-c"));
        }

        [Test]
        public void SomeoneElsesToken_OrNoToken_DoesNotVerify()
        {
            var tokens = new ReconnectTokens();
            string a = tokens.Issue("player-a");
            string b = tokens.Issue("player-b");

            Assert.IsFalse(tokens.Verify("player-a", b), "el token del rival no vale");
            Assert.IsFalse(tokens.Verify("player-a", null));
            Assert.IsFalse(tokens.Verify("player-a", ""));
            Assert.IsFalse(tokens.Verify("player-a", a.Substring(0, 31)), "un token truncado no vale");
            Assert.IsFalse(tokens.Verify("player-a", a.ToUpperInvariant()), "distingue mayusculas");
        }

        [Test]
        public void UnknownPlayer_NeverVerifies()
        {
            var tokens = new ReconnectTokens();
            tokens.Issue("player-a");

            Assert.IsFalse(tokens.Verify("player-x", "00000000000000000000000000000000"));
            Assert.IsFalse(tokens.Has("player-x"));
            Assert.IsFalse(tokens.Verify(null, "abc"));
        }

        [Test]
        public void ReissuingReplacesTheOldToken_AndClearForgetsEverything()
        {
            var tokens = new ReconnectTokens();
            string first = tokens.Issue("player-a");
            string second = tokens.Issue("player-a");

            Assert.IsFalse(tokens.Verify("player-a", first));
            Assert.IsTrue(tokens.Verify("player-a", second));

            tokens.Clear();
            Assert.IsFalse(tokens.Verify("player-a", second));
        }
    }
}
