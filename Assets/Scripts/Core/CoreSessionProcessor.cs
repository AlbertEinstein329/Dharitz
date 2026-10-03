using System;
using System.Linq;

namespace MyGame.Core
{
    public static class CoreSessionProcessor
    {
        public static void EvaluateSessionState(MatchStateDTO state, int maxDicePerPlayer)
        {
            if (state.CurrentPhase == MatchPhase.GameOver) return;

            bool allFinished = true;
            int alivePlayers = 0;

            foreach (var kvp in state.PlayerProfiles)
            {
                var player = kvp.Value;
                if (!player.IsEliminated)
                {
                    alivePlayers++;
                    if (player.PlacedDice < maxDicePerPlayer)
                    {
                        allFinished = false;
                    }
                }
            }

            // Condición de Victoria: Todos terminaron o todos fueron eliminados (Muerte súbita)
            if (alivePlayers == 0 || allFinished)
            {
                state.CurrentPhase = MatchPhase.GameOver;

                // El Servidor aplica el castigo final de Topología pura
                foreach (var kvp in state.PlayerBoards)
                {
                    int pId = kvp.Key;
                    int gapPenalty = CoreTopologyCalculator.CalculateGapPenalty(kvp.Value.Cells, kvp.Value.Rows, kvp.Value.Cols, out _);

                    // Mutación inmutable de la Capa 0
                    state.PlayerProfiles[pId].Score += gapPenalty;
                }
            }
        }
    }
}