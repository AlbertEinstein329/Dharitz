using UnityEngine;

public enum SpecialRule
{
    None,
    PenalizeOnContact,    // Regla Variante 1 (Dado 1)
    RewardOnContact,      // Regla Variante 3 (Dado 1)
    ExtraDiagonalContact, // Regla Variante 1 (Dado 2)
    DiagonalOneContact    // Regla Variante 2 (Dado 1)
}

public struct RuleEvaluationResult
{
    public int ScoreDelta;
    public bool IsPatternValid;

    public RuleEvaluationResult(int scoreDelta, bool isPatternValid)
    {
        ScoreDelta = scoreDelta;
        IsPatternValid = isPatternValid;
    }
}

public static class SpecialRuleEvaluator
{
    // El valor exacto del bono del patrón 1 (Para restarlo si se rompe la regla)
    private const int PATTERN_1_BONUS = 100; 
    private const int CONTACT_BONUS = 100;

    // Renombramos totalContactos a contactosOrtogonales para evitar confusiones
    public static RuleEvaluationResult EvaluatePlacement(SpecialRule activeRule, int contactosOrtogonales, int contactosDiagonales, bool isFirstDieOnBoard)
    {
        switch (activeRule)
        {
            case SpecialRule.PenalizeOnContact: // Variante 1
                // Regla 1: Si un 1 toca ortogonalmente a otro 1, NO gana el bono (false).
                // Además, RESTAMOS los 100 puntos de CADA dado adyacente que ya los había cobrado antes.
                if (contactosOrtogonales > 0)
                {
                    return new RuleEvaluationResult(-(contactosOrtogonales * PATTERN_1_BONUS), false);
                }
                
                // Si está completamente aislado, gana sus 100 puntos normales en el GameManager sin restar nada.
                return new RuleEvaluationResult(0, true);

            case SpecialRule.DiagonalOneContact: // Variante 2
                if (isFirstDieOnBoard) return new RuleEvaluationResult(0, true);

                // Variante 2: SOLO premia el contacto diagonal puro. Si toca algo ortogonal, se arruina el patrón.
                if (contactosDiagonales > 0 && contactosOrtogonales == 0)
                {
                    return new RuleEvaluationResult(contactosDiagonales * CONTACT_BONUS, true);
                }
                else
                {
                    return new RuleEvaluationResult(0, false);
                }

            case SpecialRule.RewardOnContact: // Variante 3
                if (isFirstDieOnBoard) return new RuleEvaluationResult(0, true);

                // Variante 3 ESTRICTA: Solo da puntos por contactos Ortogonales.
                if (contactosOrtogonales > 0)
                {
                    return new RuleEvaluationResult(contactosOrtogonales * CONTACT_BONUS, true);
                }
                else
                {
                    // Si está aislado o solo toca en diagonal, no gana bono.
                    return new RuleEvaluationResult(0, false);
                }

            case SpecialRule.None:
            case SpecialRule.ExtraDiagonalContact:
            default:
                return new RuleEvaluationResult(0, true);
        }
    }
}