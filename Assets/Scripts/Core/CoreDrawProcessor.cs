using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    [Serializable]
    public struct DrawDieCommand { public int PlayerId; }

    [Serializable]
    public struct ReDrawCommand { public int PlayerId; }

    public static class CoreDrawProcessor
    {
        // El servidor evalúa la intención de robar un dado
        public static bool ProcessDrawIntent(MatchStateDTO state, DrawDieCommand command, System.Random rng)
        {
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return false;
            if (state.CurrentPlayerIndex != command.PlayerId) return false;
            if (state.HasDrawn) return false; // Ya tiene un dado en mano
            if (state.DiceBag.Count == 0) return false; // Bolsa vacía

            // 1. Extracción determinista
            state.CurrentDrawnColor = state.DiceBag[0];
            state.DiceBag.RemoveAt(0);
            state.HasDrawn = true;

            PlayerDataDTO player = state.PlayerProfiles[command.PlayerId];
            DieColor color = state.CurrentDrawnColor.Value;

            // 2. Lógica de asignación de Grupos y Valores
            if (!player.ActiveGroups.ContainsKey(color))
            {
                int randomValue = rng.Next(1, 7);
                int randomId = rng.Next(10000, 99999);
                player.ActiveGroups[color] = new GroupDataDTO(randomId, color, randomValue);
                state.CurrentDrawnValue = randomValue;
            }
            else
            {
                GroupDataDTO group = player.ActiveGroups[color];
                if (group.IsClosed)
                {
                    int randomValue = rng.Next(1, 7);
                    int randomId = rng.Next(10000, 99999);
                    player.ActiveGroups[color] = new GroupDataDTO(randomId, color, randomValue);
                    state.CurrentDrawnValue = randomValue;
                }
                else
                {
                    state.CurrentDrawnValue = group.TargetSize;
                }
            }

            return true;
        }

        // El servidor evalúa la intención de devolver un dado
        public static bool ProcessReDrawIntent(MatchStateDTO state, ReDrawCommand command, System.Random rng)
        {
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return false;
            if (state.CurrentPlayerIndex != command.PlayerId) return false;
            if (!state.HasDrawn) return false; // No hay dado para devolver
            if (!state.CurrentDrawnColor.HasValue) return false;

            PlayerDataDTO player = state.PlayerProfiles[command.PlayerId];
            if (player.ReDraws <= 0) return false;

            DieColor color = state.CurrentDrawnColor.Value;

            // 1. Limpiar grupo huérfano
            if (player.ActiveGroups.ContainsKey(color) && player.ActiveGroups[color].OccupiedCells.Count == 0)
            {
                player.ActiveGroups.Remove(color);
            }

            // 2. Devolución aleatoria a la bolsa
            int insertIndex = rng.Next(0, state.DiceBag.Count + 1);
            state.DiceBag.Insert(insertIndex, color);

            // 3. Mutación de economía y estado
            player.ReDraws--;
            state.HasDrawn = false;
            state.CurrentDrawnColor = null;
            state.CurrentDrawnValue = null;

            return true;
        }
    }
}