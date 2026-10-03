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
        if (logic[r, c] != null) return false;

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

        // 1. Traducimos la matriz visual 2D pesada a un array 1D ligero
        MyGame.Core.BoardStateDTO boardDTO = new MyGame.Core.BoardStateDTO(rows, cols);
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int idx = boardDTO.GetIndex(row, col);
                if (logic[row, col] != null)
                {
                    boardDTO.Cells[idx] = new MyGame.Core.CellStateDTO
                    {
                        IsOccupied = true,
                        Color = (MyGame.Core.DieColor)(int)logic[row, col].color,
                        GroupId = logic[row, col].groupId,
                        Value = logic[row, col].value
                    };
                }
            }
        }

        // 2. Extraemos los patrones de la variante
        MyGame.Core.PatternDefDTO pVariant = null;
        if (player.activeGroups.ContainsKey(color) && player.activeGroups[color] != null)
        {
            PatternData existingData = variant.GetPattern(player.activeGroups[color].targetSize);
            if (existingData != null) pVariant = existingData.ToDTO();
        }

        PatternData newPatternData = variant.GetPattern(number);
        MyGame.Core.PatternDefDTO pNew = newPatternData != null ? newPatternData.ToDTO() : null;

        // 3. Ejecutamos el validador estricto de alto rendimiento
        return MyGame.Core.CoreTopologyCalculator.ValidateSurvival(
            boardDTO,
            r,
            c,
            (MyGame.Core.DieColor)(int)color,
            currentGroupId,
            number,
            player.ToDTO(),
            pVariant,
            pNew
        );
    }
}