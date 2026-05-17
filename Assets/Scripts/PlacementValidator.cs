using UnityEngine;

public static class PlacementValidator
{
    public static bool CanBotPlaceHere(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int groupId, int number, PlayerData player, VariantData variant)
    {
        if (r < 0 || r >= rows || c < 0 || c >= cols) return false;
        if (logic[r, c] != null) return false;
        return IsValidPlacement(logic, rows, cols, r, c, color, groupId, number, player, variant);
    }

    public static bool IsValidPlacement(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int currentGroupId, int number, PlayerData player, VariantData variant)
    {
        bool isBoardEmpty = (player.placedDice == 0);

        PatternData currentPattern = variant.GetPattern(number);

        bool hasDiceInGroup = false;
        if (player.activeGroups.ContainsKey(color) && player.activeGroups[color] != null)
        {
            hasDiceInGroup = player.activeGroups[color].occupiedCells.Count > 0;
        }

        bool touchesOwnGroup = false;
        bool touchesAnyDie = false;

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;

                int nr = r + i;
                int nc = c + j;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    GridManager.DieData neighbor = logic[nr, nc];

                    if (neighbor != null)
                    {
                        if (number == 1 && neighbor.value == 1)
                        {
                            if (currentPattern.specialRule == SpecialRule.PenalizeOnContact)
                            {
                                if (neighbor.color == color) return false;
                            }
                        }

                        if (neighbor.groupId == currentGroupId)
                        {
                            touchesOwnGroup = true;
                        }

                        bool esOrtogonal = (i == 0 || j == 0);

                        if (esOrtogonal)
                        {
                            touchesAnyDie = true;
                            if (neighbor.color == color && neighbor.groupId != currentGroupId)
                                return false;
                        }
                        else
                        {
                            if (neighbor.groupId == currentGroupId)
                            {
                                touchesAnyDie = true;
                            }
                        }
                    }
                }
            }
        }

        if (isBoardEmpty) return true;
        if (hasDiceInGroup && !touchesOwnGroup) return false; 
        if (!touchesAnyDie) return false;

        return TopologyCalculator.ValidateSurvival(logic, rows, cols, r, c, color, currentGroupId, number, player, variant);
    }
}
