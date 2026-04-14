using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NuevaVariante", menuName = "Dharitz/Datos de Variante")]
public class VariantData : ScriptableObject
{
    public string nombreVariante;
    [TextArea] public string descripcion;

    [Header("Patrones de esta Variante")]
    public List<PatternData> patrones = new List<PatternData>();

    [Header("Configuración Global")]
    public bool reservasDiagonalesPermitidas = false; // Útil para la Variante 1 y 3

    // Una función rápida para que el GameManager extraiga el patrón correcto
    public PatternData ObtenerPatron(int numeroDado)
    {
        return patrones.Find(p => p.targetNumber == numeroDado);
    }
}