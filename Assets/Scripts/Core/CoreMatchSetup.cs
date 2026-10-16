using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    /// <summary>
    /// Ciclo de vida autoritativo de una partida: creación, avance de turno y descarte forzado.
    /// Lógica pura (sin Unity) para que el servidor la ejecute sin depender de UI ni escenas.
    /// </summary>
    public static class CoreMatchSetup
    {
        public const int DicePerColorPerPlayer = 13;

        // Construye el estado inicial completo: perfiles, tableros vacíos y bolsa barajada con el RNG del servidor
        public static MatchStateDTO CreateMatch(int seed, IList<string> playerNames, int rows, int cols, VariantDefDTO variant)
        {
            var state = new MatchStateDTO();
            state.InitializeRNG(seed);
            state.MatchId = Guid.NewGuid().ToString("N");
            state.VariantConfig = variant;

            for (int i = 0; i < playerNames.Count; i++)
            {
                state.PlayerProfiles[i] = new PlayerDataDTO(i, playerNames[i]);
                state.PlayerBoards[i] = new BoardStateDTO(rows, cols);
            }

            // Misma composición que DiceManager.InitializeBag: 13 dados por color y por jugador
            int dicePerColor = DicePerColorPerPlayer * playerNames.Count;
            state.DiceBag = new List<DieColor>(dicePerColor * 4);
            for (int i = 0; i < dicePerColor; i++)
            {
                state.DiceBag.Add(DieColor.Red);
                state.DiceBag.Add(DieColor.Blue);
                state.DiceBag.Add(DieColor.White);
                state.DiceBag.Add(DieColor.Black);
            }

            // Fisher-Yates con el RNG autoritativo
            for (int i = state.DiceBag.Count - 1; i > 0; i--)
            {
                int j = state.ServerRNG.Next(0, i + 1);
                DieColor temp = state.DiceBag[i];
                state.DiceBag[i] = state.DiceBag[j];
                state.DiceBag[j] = temp;
            }

            state.CurrentPlayerIndex = 0;
            state.HasDrawn = false;
            state.CurrentPhase = MatchPhase.PlayerTurn;
            return state;
        }

        // Pasa al siguiente jugador no eliminado. Si no queda ninguno, la partida termina.
        public static bool AdvanceTurn(MatchStateDTO state)
        {
            ClearHand(state);

            int playerCount = state.PlayerProfiles.Count;
            for (int step = 1; step <= playerCount; step++)
            {
                int next = (state.CurrentPlayerIndex + step) % playerCount;
                if (!state.PlayerProfiles[next].IsEliminated)
                {
                    state.CurrentPlayerIndex = next;
                    state.TurnNumber++;
                    return true;
                }
            }

            state.CurrentPhase = MatchPhase.GameOver;
            return false;
        }

        // Devuelve a la bolsa el dado en mano sin gastar Re-Draw (tiempo agotado o abandono)
        public static void DiscardDrawnDie(MatchStateDTO state)
        {
            if (!state.HasDrawn || !state.CurrentDrawnColor.HasValue)
            {
                ClearHand(state);
                return;
            }

            DieColor color = state.CurrentDrawnColor.Value;
            PlayerDataDTO player = state.PlayerProfiles[state.CurrentPlayerIndex];

            // Un grupo recién abierto sin dados colocados no debe sobrevivir al descarte
            if (player.ActiveGroups.TryGetValue(color, out GroupDataDTO group) && group.OccupiedCells.Count == 0)
            {
                player.ActiveGroups.Remove(color);
            }

            int insertIndex = state.ServerRNG.Next(0, state.DiceBag.Count + 1);
            state.DiceBag.Insert(insertIndex, color);
            ClearHand(state);
        }

        private static void ClearHand(MatchStateDTO state)
        {
            state.HasDrawn = false;
            state.CurrentDrawnColor = null;
            state.CurrentDrawnValue = null;
        }
    }
}
