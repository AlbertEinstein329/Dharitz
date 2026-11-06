using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MyGame.Core;

[CreateAssetMenu(fileName = "NewPattern", menuName = "Dharitz/Pattern Data")]
public class PatternData : ScriptableObject
{
    public int targetNumber;
    [UnityEngine.Serialization.FormerlySerializedAs("permiteRotacion")]
    public bool allowRotation;
    [UnityEngine.Serialization.FormerlySerializedAs("permiteEspejo")]
    public bool allowMirror;
    [UnityEngine.Serialization.FormerlySerializedAs("permiteReservaDiagonal")]
    public bool allowDiagonalReservation;

    // Regla especial de puntaje/contacto (enum global en SpecialRuleEvaluator.cs)
    [UnityEngine.Serialization.FormerlySerializedAs("reglaEspecial")]
    public SpecialRule specialRule = SpecialRule.None;

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
            SpecialRule = (int)this.specialRule,
            // En los assets y en la UI de patrones x es horizontal (columna) e y vertical (fila); el nucleo usa (fila, columna)
            BaseShape = this.baseShape.Select(v => new GridPos(v.y, v.x)).ToList()
        };
    }
}