using UnityEngine;
using System.Collections.Generic;

// Definición global de colores [cite: 10-14]
public enum DieColor { Rojo, Azul, Blanco, Negro }


[System.Serializable]
public class GroupData
{
    public int id;
    public DieColor color;
    public int targetSize;
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();

    // Esta línea es VITAL: se cierra automáticamente al llegar al número
    public bool isClosed => occupiedCells.Count >= targetSize;
}

[System.Serializable]
public class PlayerData
{
    public int id;
    public string name;
    public int score = 0;
    public int dadosColocados = 0;
    public int[] conteoPatrones = new int[7];

    public bool isBot = false;
    public int botDifficulty = 0;

    // --- NUEVO: Contador de Re-Draws permitidos ---
    public int reDraws = 3;

    public Dictionary<DieColor, GroupData> activeGroups = new Dictionary<DieColor, GroupData>();

    public PlayerData(int id, string name, bool isBot = false, int difficulty = 0)
    {
        this.id = id;
        this.name = name;
        this.isBot = isBot;
        this.botDifficulty = difficulty;
        this.reDraws = 300; // Inicializamos con 300 usos por partida
        this.activeGroups = new Dictionary<DieColor, GroupData>();
        this.conteoPatrones = new int[7];
    }


}