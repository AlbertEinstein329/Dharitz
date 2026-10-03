using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MyGame.Core;

[CreateAssetMenu(fileName = "NewPattern", menuName = "Dharitz/Pattern Data")]
public class PatternData : ScriptableObject
{
    public int targetNumber;
    public bool allowRotation;
    public bool allowMirror;
    public bool allowDiagonalReservation;
    public List<Vector2Int> baseShape = new List<Vector2Int>();

    // EL PUENTE: Convierte la data serializada de Unity a Data del Servidor
    public PatternDefDTO ToDTO()
    {
        return new PatternDefDTO
        {
            TargetNumber = this.targetNumber,
            AllowRotation = this.allowRotation,
            AllowMirror = this.allowMirror,
            AllowDiagonalReservation = this.allowDiagonalReservation,
            BaseShape = this.baseShape.Select(v => new GridPos(v.x, v.y)).ToList()
        };
    }
}