using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewVariant", menuName = "Dharitz/Variant Data")]
public class VariantData : ScriptableObject
{
    public string variantName;
    [TextArea] public string description;

    [Header("Patterns for this Variant")]
    public List<PatternData> patterns = new List<PatternData>();

    
    // Helper function for GameManager to extract the correct pattern
    public PatternData GetPattern(int diceNumber)
    {
        return patterns.Find(p => p.targetNumber == diceNumber);
    }
}
