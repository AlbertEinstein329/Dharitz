using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MyGame.Core;

[CreateAssetMenu(fileName = "NewVariant", menuName = "Dharitz/Variant Data")]
public class VariantData : ScriptableObject
{
    public string variantName;
    [TextArea] public string description;

    [Header("Patterns for this Variant")]
    public List<PatternData> patterns = new List<PatternData>();

    [Header("UI")]
    public Sprite iconSprite;
    public Color highlightColor = Color.white;

    public PatternData GetPattern(int diceNumber)
    {
        return patterns.Find(p => p.targetNumber == diceNumber);
    }

    // =========================================================
    // EL PUENTE: Convierte el ScriptableObject a DTO del servidor
    // =========================================================
    public VariantDefDTO ToDTO()
    {
        var dto = new VariantDefDTO { VariantName = this.variantName };
        foreach (PatternData p in patterns)
        {
            if (p != null)
                dto.Patterns[p.targetNumber] = p.ToDTO();
        }
        return dto;
    }
}
