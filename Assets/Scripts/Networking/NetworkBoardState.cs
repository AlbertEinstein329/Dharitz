using System;
using UnityEngine;
using Unity.Netcode;
using MyGame.Core;

namespace MyGame.Networking
{
    /// <summary>
    /// Estructura de red optimizada para sincronizar el estado del tablero (8x10).
    /// Serializa los 80 casilleros en una ráfaga comprimida de bytes (80 bytes max).
    /// </summary>
    public struct NetworkBoardState : INetworkSerializable
    {
        public int PlayerIndex;
        public byte Rows;
        public byte Cols;
        public byte[] CompressedCells; // Cada celda = (colorId << 4) | (numero & 0x0F)

        public NetworkBoardState(int playerIndex, BoardStateDTO dto)
        {
            PlayerIndex = playerIndex;
            Rows = (byte)dto.Rows;
            Cols = (byte)dto.Cols;
            CompressedCells = new byte[Rows * Cols];

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int index = r * Cols + c;
                    var cell = dto.Cells[index];
                    if (cell.IsOccupied)
                    {
                        byte colorVal = (byte)cell.Color;
                        byte numberVal = (byte)cell.Value;
                        CompressedCells[index] = (byte)(((colorVal + 1) << 4) | (numberVal & 0x0F));
                    }
                    else
                    {
                        CompressedCells[index] = 0; // Celda vacía
                    }
                }
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerIndex);
            serializer.SerializeValue(ref Rows);
            serializer.SerializeValue(ref Cols);

            if (serializer.IsReader)
            {
                int count = Rows * Cols;
                CompressedCells = new byte[count];
            }

            for (int i = 0; i < CompressedCells.Length; i++)
            {
                serializer.SerializeValue(ref CompressedCells[i]);
            }
        }

        public BoardStateDTO ToDTO()
        {
            BoardStateDTO dto = new BoardStateDTO(Rows, Cols);
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int index = r * Cols + c;
                    byte raw = CompressedCells[index];
                    if (raw != 0)
                    {
                        int colorVal = (raw >> 4) - 1;
                        int number = raw & 0x0F;
                        dto.Cells[index] = new CellStateDTO
                        {
                            IsOccupied = true,
                            Color = (DieColor)colorVal,
                            Value = number
                        };
                    }
                }
            }
            return dto;
        }
    }
}
