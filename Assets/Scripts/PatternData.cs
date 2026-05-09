using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NuevoPatron", menuName = "Dharitz/Datos de Patron")]
public class PatternData : ScriptableObject
{
    [Header("Identificador")]
    [Tooltip("Die face number (1 to 6)")]
    public int targetNumber;

    [Header("Geometric Shape")]
    [Tooltip("Relative coordinates. Ex: (0,0), (1,0)")]
    public List<Vector2Int> baseShape = new List<Vector2Int>();

    [Header("Transformation Rules")]
    public bool allowRotation = true;
    public bool allowMirror = true;

    [Header("Special Rules (Variants)")]
    [Tooltip("Defines if this pattern has special scoring or reservation behavior")]
    public SpecialRule specialRule = SpecialRule.None;
    
    [Tooltip("If true, this pattern allows reserving spaces diagonally for survival checks")]
    public bool allowDiagonalReservation = false;

    // NOTE: SpecialRule enum is defined globally in SpecialRuleEvaluator.cs
    // Do NOT add a duplicate enum here.
}