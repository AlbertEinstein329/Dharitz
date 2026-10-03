using UnityEngine;
using MyGame.Core;

public static class ScoreCalculator
{

    public static int Count3x3Contacts(GridManager.DieData[,] logic, int rows, int cols, int r, int c, int valorDado, out int contactosDiagonales)
    {
        BoardStateDTO boardDTO = ConvertToDTO(logic, rows, cols);
        return CoreScoreCalculator.Count3x3Contacts(boardDTO.Cells, rows, cols, r, c, valorDado, out contactosDiagonales);
    }

    public static int GetOrthogonalConnections(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int groupId)
    {
        BoardStateDTO boardDTO = ConvertToDTO(logic, rows, cols);
        return CoreScoreCalculator.GetOrthogonalConnections(boardDTO.Cells, rows, cols, r, c, (MyGame.Core.DieColor)(int)color, groupId);
    }

    public static int GetDiagonalConnections(GridManager.DieData[,] logic, int rows, int cols, int r, int c, DieColor color, int groupId)
    {
        BoardStateDTO boardDTO = ConvertToDTO(logic, rows, cols);
        return CoreScoreCalculator.GetDiagonalConnections(boardDTO.Cells, rows, cols, r, c, (MyGame.Core.DieColor)(int)color, groupId);
    }

    public static int GetOnesPenalties(GridManager.DieData[,] logic, int rows, int cols)
    {
        BoardStateDTO boardDTO = ConvertToDTO(logic, rows, cols);
        return CoreScoreCalculator.GetOnesPenalties(boardDTO.Cells, rows, cols);
    }

    // Método utilitario interno para el puente
    private static BoardStateDTO ConvertToDTO(GridManager.DieData[,] logic, int rows, int cols)
    {
        BoardStateDTO boardDTO = new BoardStateDTO(rows, cols);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int idx = boardDTO.GetIndex(r, c);
                if (logic[r, c] != null)
                {
                    boardDTO.Cells[idx] = new CellStateDTO
                    {
                        IsOccupied = true,
                        Color = (MyGame.Core.DieColor)(int)logic[r, c].color,
                        GroupId = logic[r, c].groupId,
                        Value = logic[r, c].value
                    };
                }
            }
        }
        return boardDTO;
    }
}