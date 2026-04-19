using UnityEngine;

public enum SpecialRule
{
    None,
    PenalizeOnContact,    // Regla Variante 1
    RewardOnContact,      // Regla Variante 3
    ExtraDiagonalContact, // Regla Variante 1 del dado 2
    DiagonalOneContact    // Regla Variante 2 del 1
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
    private const int MULTA_POR_AISLAMIENTO = -250;
    private const int BONO_POR_CONTACTO = 50;

    // NUEVO: Añadimos 'bool isFirstDieOnBoard' a la función
    public static RuleEvaluationResult EvaluatePlacement(SpecialRule activeRule, int totalContactos, int contactosDiagonales, bool isFirstDieOnBoard)
    {
        switch (activeRule)
        {
            case SpecialRule.PenalizeOnContact: // Variante 1
                if (totalContactos > 0)
                {
                    return new RuleEvaluationResult(totalContactos * MULTA_POR_AISLAMIENTO, false);
                }
                return new RuleEvaluationResult(0, true);

            case SpecialRule.DiagonalOneContact: // Variante 2
                // EXCEPCIÓN: Si es el primer dado de todos, se salva de la multa y gana el bono base.
                if (isFirstDieOnBoard) return new RuleEvaluationResult(0, true);

                if (contactosDiagonales > 0)
                {
                    return new RuleEvaluationResult(contactosDiagonales * BONO_POR_CONTACTO, true);
                }
                else
                {
                    return new RuleEvaluationResult(MULTA_POR_AISLAMIENTO, false);
                }

            case SpecialRule.RewardOnContact: // Variante 3
                // EXCEPCIÓN: Si es el primer dado de todos, se salva de la multa y gana el bono base.
                if (isFirstDieOnBoard) return new RuleEvaluationResult(0, true);

                if (totalContactos > 0)
                {
                    return new RuleEvaluationResult(totalContactos * BONO_POR_CONTACTO, true);
                }
                else
                {
                    return new RuleEvaluationResult(MULTA_POR_AISLAMIENTO, false);
                }

            case SpecialRule.None:
            case SpecialRule.ExtraDiagonalContact:
            default:
                return new RuleEvaluationResult(0, true);
        }
    }
}