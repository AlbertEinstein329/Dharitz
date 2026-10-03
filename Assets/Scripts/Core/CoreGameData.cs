using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    // 1. Sustituto puro de UnityEngine.Vector2Int
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int X { get; }
        public int Y { get; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    // 2. Colores del dado
    [Serializable]
    public enum DieColor
    {
        Red,
        Blue,
        White,
        Black
    }

    // 3. Sustituto de GridManager.DieData (Desacoplado)
    [Serializable]
    public class DieDataDTO
    {
        public DieColor Color { get; set; }
        public int GroupId { get; set; }
        public int Value { get; set; }

        public DieDataDTO(DieColor color, int groupId, int value)
        {
            Color = color;
            GroupId = groupId;
            Value = value;
        }
    }

    // 4. Sustituto de GroupData
    [Serializable]
    public class GroupDataDTO
    {
        public int Id { get; set; }
        public DieColor Color { get; set; }
        public int TargetSize { get; set; }

        // El HashSet optimiza las validaciones topológicas en el servidor
        public HashSet<GridPos> OccupiedCells { get; set; }

        public bool IsClosed => OccupiedCells.Count >= TargetSize;

        public GroupDataDTO(int id, DieColor color, int targetSize)
        {
            Id = id;
            Color = color;
            TargetSize = targetSize;
            OccupiedCells = new HashSet<GridPos>();
        }
    }

    // 5. Sustituto de PlayerData
    [Serializable]
    public class PlayerDataDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
        public int PlacedDice { get; set; }
        public bool IsEliminated { get; set; }

        public int[] PatternCounts { get; set; }
        public int AccumulatedStructurePoints { get; set; }
        public int AccumulatedOnesPenalty { get; set; }

        public int ReDraws { get; set; }
        public int CurrentUndoUses { get; set; }
        public int CurrentMoveUses { get; set; }
        public int TotalCoins { get; set; }

        public Dictionary<DieColor, GroupDataDTO> ActiveGroups { get; set; }

        public PlayerDataDTO(int id, string name)
        {
            Id = id;
            Name = name;
            Score = 0;
            PlacedDice = 0;
            IsEliminated = false;
            PatternCounts = new int[7];
            ReDraws = 3;
            CurrentUndoUses = 2;
            CurrentMoveUses = 1;
            ActiveGroups = new Dictionary<DieColor, GroupDataDTO>();
        }
    }

    // 1. Tipo de valor puro, sin referencias a memoria administrada (0 Garbage Collection)
    [Serializable]
    public struct CellStateDTO
    {
        public bool IsOccupied;
        public DieColor Color;
        public int GroupId;
        public int Value;
    }

    // 2. Tablero aplanado de alto rendimiento
    [Serializable]
    public class BoardStateDTO
    {
        public int Rows { get; set; }
        public int Cols { get; set; }
        public CellStateDTO[] Cells { get; set; }

        public BoardStateDTO(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            Cells = new CellStateDTO[rows * cols];
        }

        // Conversión matemática de 2D a 1D
        public int GetIndex(int r, int c) => r * Cols + c;

        // Conversión matemática de 1D a 2D
        public GridPos GetPos(int index) => new GridPos(index / Cols, index % Cols);
    }
}