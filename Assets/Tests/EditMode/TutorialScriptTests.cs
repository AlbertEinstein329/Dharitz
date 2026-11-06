using System.Collections.Generic;
using NUnit.Framework;

namespace MyGame.Core.Tests
{
    /// <summary>Los dados del tutorial salen en el orden del guion, con el valor fijado, y despues vuelve la bolsa normal.</summary>
    public class TutorialScriptTests
    {
        private static MatchStateDTO NewState(bool withScript)
        {
            var state = new MatchStateDTO { CurrentPhase = MatchPhase.PlayerTurn, CurrentPlayerIndex = 0 };
            state.InitializeRNG(1234);
            state.PlayerProfiles[0] = new PlayerDataDTO(0, "A");
            state.PlayerBoards[0] = new BoardStateDTO(10, 8);
            state.DiceBag = new List<DieColor>();
            for (int i = 0; i < 20; i++) state.DiceBag.AddRange(new[] { DieColor.Red, DieColor.Blue, DieColor.White, DieColor.Black });
            if (withScript) state.TutorialDraws = TutorialScript.CreateDraws();
            return state;
        }

        private static void Draw(MatchStateDTO state)
        {
            state.HasDrawn = false;
            Assert.IsTrue(CoreDrawProcessor.ProcessDrawIntent(state, new DrawDieCommand { PlayerId = 0 }, state.ServerRNG));
        }

        [Test]
        public void ScriptedDraws_ComeOutInOrder_WithTheScriptedGroupSize()
        {
            MatchStateDTO state = NewState(true);

            Draw(state);
            Assert.AreEqual(DieColor.Red, state.CurrentDrawnColor);
            Assert.AreEqual(2, state.CurrentDrawnValue);

            Draw(state); // mismo grupo: el valor lo manda el grupo abierto
            Assert.AreEqual(DieColor.Red, state.CurrentDrawnColor);
            Assert.AreEqual(2, state.CurrentDrawnValue);

            Draw(state);
            Assert.AreEqual(DieColor.Blue, state.CurrentDrawnColor);
            Assert.AreEqual(1, state.CurrentDrawnValue);

            Draw(state);
            Assert.AreEqual(DieColor.White, state.CurrentDrawnColor);
            Assert.AreEqual(3, state.CurrentDrawnValue);
        }

        [Test]
        public void ScriptedDraws_AreTheSameInEveryGame_AndTakeTheirDieOutOfTheBag()
        {
            MatchStateDTO a = NewState(true);
            MatchStateDTO b = NewState(true);
            int bagBefore = a.DiceBag.Count;

            var seenA = new List<string>();
            var seenB = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                Draw(a); Draw(b);
                seenA.Add(a.CurrentDrawnColor + ":" + a.CurrentDrawnValue);
                seenB.Add(b.CurrentDrawnColor + ":" + b.CurrentDrawnValue);
            }

            CollectionAssert.AreEqual(seenA, seenB);
            Assert.AreEqual(bagBefore - 5, a.DiceBag.Count);
            Assert.AreEqual(0, a.TutorialDraws.Count, "el guion se consume entero");
        }

        [Test]
        public void WithoutAScript_TheBagDecidesAsBefore()
        {
            MatchStateDTO state = NewState(false);
            DieColor first = state.DiceBag[0];

            Draw(state);

            Assert.AreEqual(first, state.CurrentDrawnColor);
        }
    }
}
