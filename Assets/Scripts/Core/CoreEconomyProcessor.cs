using System;

namespace MyGame.Core
{
    [Serializable]
    public struct BuyUpgradeCommand
    {
        public int PlayerId;
        public string UpgradeType;
        public int Cost;
        public int MaxUses;
    }

    public static class CoreEconomyProcessor
    {
        // La Capa 0 evalúa si el jugador es solvente y si no ha roto el límite máximo
        public static bool ProcessBuyIntent(MatchStateDTO state, BuyUpgradeCommand command)
        {
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return false;

            if (!state.PlayerProfiles.ContainsKey(command.PlayerId)) return false;
            PlayerDataDTO player = state.PlayerProfiles[command.PlayerId];

            // 1. Verificación de Solvencia (Server-Side)
            if (player.TotalCoins < command.Cost) return false;

            // 2. Inyección de Mejoras
            if (command.UpgradeType == "Undo")
            {
                if (player.CurrentUndoUses >= command.MaxUses) return false;
                player.CurrentUndoUses++;
            }
            else if (command.UpgradeType == "Move")
            {
                if (player.CurrentMoveUses >= command.MaxUses) return false;
                player.CurrentMoveUses++;
            }
            else return false;

            // 3. Cobro Autoritativo
            player.TotalCoins -= command.Cost;
            return true;
        }
    }
}