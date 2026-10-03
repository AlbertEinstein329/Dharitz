using UnityEngine;
using System.Collections.Generic;
using MyGame.Core; // Importación obligatoria de la Capa 0

// Definición global visual de colores (Mantenida para el Frontend)
public enum DieColor { Red, Blue, White, Black }

[System.Serializable]
public class GroupData
{
    public int id;
    public DieColor color;
    public int targetSize;
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();

    public bool isClosed => occupiedCells.Count >= targetSize;

    // EL PUENTE: De Unity al Servidor
    public GroupDataDTO ToDTO()
    {
        var dto = new GroupDataDTO(this.id, (MyGame.Core.DieColor)(int)this.color, this.targetSize);
        foreach (var cell in occupiedCells)
        {
            dto.OccupiedCells.Add(new GridPos(cell.x, cell.y));
        }
        return dto;
    }
}

[System.Serializable]
public class PlayerData
{
    public int id;
    public string name;
    public int avatarId;
    public int score = 0;
    public int placedDice = 0;
    public bool isEliminated = false;
    public int[] patternCounts = new int[7];
    public int accumulatedStructurePoints = 0;
    public int accumulatedOnesPenalty = 0;

    public bool isBot = false;
    public int botDifficulty = 0;

    public const int DEFAULT_REDRAWS = 3;
    public int reDraws = DEFAULT_REDRAWS;

    public int currentUndoUses;
    public int currentMoveUses;

    public Dictionary<DieColor, GroupData> activeGroups = new Dictionary<DieColor, GroupData>();

    public PlayerData(int id, string name, bool isBot = false, int difficulty = 0)
    {
        this.id = id;
        this.name = name;
        this.isBot = isBot;
        this.botDifficulty = difficulty;
        this.reDraws = DEFAULT_REDRAWS;
        this.currentUndoUses = 2;
        this.currentMoveUses = 1;
        this.activeGroups = new Dictionary<DieColor, GroupData>();
        this.patternCounts = new int[7];
    }

    // EL PUENTE: Empaquetado completo del estado del jugador
    public PlayerDataDTO ToDTO()
    {
        var dto = new PlayerDataDTO(this.id, this.name)
        {
            Score = this.score,
            PlacedDice = this.placedDice,
            IsEliminated = this.isEliminated,
            PatternCounts = (int[])this.patternCounts.Clone(),
            AccumulatedStructurePoints = this.accumulatedStructurePoints,
            AccumulatedOnesPenalty = this.accumulatedOnesPenalty,
            ReDraws = this.reDraws,
            CurrentUndoUses = this.currentUndoUses,
            CurrentMoveUses = this.currentMoveUses
        };

        foreach (var kvp in activeGroups)
        {
            // BLOQUEO DE SEGURIDAD: Ignoramos los grupos nulos inyectados por DiceManager
            if (kvp.Value != null)
            {
                dto.ActiveGroups[(MyGame.Core.DieColor)(int)kvp.Key] = kvp.Value.ToDTO();
            }
        }

        return dto;
    }
}