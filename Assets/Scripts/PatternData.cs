using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NuevoPatron", menuName = "Dharitz/Datos de Patron")]
public class PatternData : ScriptableObject
{
    [Header("Identificador")]
    [Tooltip("El número de la cara del dado (1 al 6)")]
    public int targetNumber;

    [Header("Forma Geométrica")]
    [Tooltip("Las coordenadas relativas. Ej: (0,0), (1,0)")]
    public List<Vector2Int> baseShape = new List<Vector2Int>();

    [Header("Reglas de Transformación")]
    public bool permiteRotacion = true;
    public bool permiteEspejo = true;

    [Header("Reglas Especiales (Variantes)")]
    [Tooltip("Define si este patrón tiene un comportamiento especial de puntaje o reserva")]
    public ReglaEspecial reglaEspecial = ReglaEspecial.Ninguna;

    public enum ReglaEspecial
    {
        Ninguna,
        AislamientoPenalizado, // Regla actual del 1
        AislamientoPremiado,   // Regla Variante 3 del 1
        ContactoDiagonalExtra, // Regla Variante 1 del 2, y Variante 2 del 3
        ContactoUnoDiagonal    // Regla Variante 2 del 1
    }
}