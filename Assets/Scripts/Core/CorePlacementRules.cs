namespace MyGame.Core
{
    /// <summary>
    /// Reglas de contacto de una colocacion (limites, casilla libre, vecinos del grupo, mismo color de otro grupo, regla del 1)
    /// en la Capa 0. Es la misma logica que PlacementValidator aplica en la capa visual, sin tipos de Unity, para que el bot
    /// (y en el futuro el servidor) la usen sin depender de la escena.
    /// </summary>
    public static class CorePlacementRules
    {
        private const int PenalizeOnContact = 1; // SpecialRule.PenalizeOnContact de la capa visual

        /// <summary>
        /// Comprueba solo las reglas de contacto. La supervivencia de los grupos (que ningun grupo abierto quede
        /// imposible de completar) la comprueba despues CoreMatchProcessor.ProcessPlacementIntent.
        /// </summary>
        public static bool PassesContactRules(BoardStateDTO board, PlayerDataDTO player, int r, int c, DieColor color, int groupId, int number, PatternDefDTO newPattern)
        {
            if (r < 0 || r >= board.Rows || c < 0 || c >= board.Cols) return false;
            if (board.Cells[board.GetIndex(r, c)].IsOccupied) return false;

            bool boardEmpty = player.PlacedDice == 0;

            bool hasDiceInGroup = player.ActiveGroups.TryGetValue(color, out GroupDataDTO group) && group != null && group.OccupiedCells.Count > 0;
            bool penalizeOnes = number == 1 && newPattern != null && newPattern.SpecialRule == PenalizeOnContact;

            bool touchesOwnGroup = false;
            bool touchesAnyDie = false;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;

                    int nr = r + i;
                    int nc = c + j;
                    if (nr < 0 || nr >= board.Rows || nc < 0 || nc >= board.Cols) continue;

                    CellStateDTO neighbor = board.Cells[board.GetIndex(nr, nc)];
                    if (!neighbor.IsOccupied) continue;

                    if (penalizeOnes && neighbor.Value == 1 && neighbor.Color == color) return false;

                    bool ownGroup = neighbor.GroupId == groupId;
                    if (ownGroup) touchesOwnGroup = true;

                    bool orthogonal = i == 0 || j == 0;
                    if (orthogonal)
                    {
                        touchesAnyDie = true;
                        if (neighbor.Color == color && !ownGroup) return false;
                    }
                    else if (ownGroup)
                    {
                        touchesAnyDie = true;
                    }
                }
            }

            if (boardEmpty) return true;
            if (hasDiceInGroup && !touchesOwnGroup) return false;
            return touchesAnyDie;
        }

        /// <summary>Contacto + supervivencia: equivale a PlacementValidator.IsValidPlacement de la capa visual.</summary>
        public static bool IsLegal(BoardStateDTO board, PlayerDataDTO player, int r, int c, DieColor color, int groupId, int number, PatternDefDTO groupPattern, PatternDefDTO newPattern)
        {
            if (!PassesContactRules(board, player, r, c, color, groupId, number, newPattern)) return false;
            if (player.PlacedDice == 0) return true;
            return CoreTopologyCalculator.ValidateSurvival(board, r, c, color, groupId, number, player, groupPattern, newPattern);
        }
    }
}
