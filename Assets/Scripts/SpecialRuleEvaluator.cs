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
    private const int ISOLATION_PENALTY = -250;
    private const int CONTACT_BONUS = 100;

    // NUEVO: Añadimos 'bool isFirstDieOnBoard' a la función
    public static RuleEvaluationResult EvaluatePlacement(SpecialRule activeRule, int totalContactos, int contactosDiagonales, bool isFirstDieOnBoard)
    {
        switch (activeRule)
        {
            case SpecialRule.PenalizeOnContact: // Variante 1
                if (totalContactos > 0)
                {
                    return new RuleEvaluationResult(totalContactos * ISOLATION_PENALTY, false);
                }
                return new RuleEvaluationResult(0, true);

            case SpecialRule.DiagonalOneContact: // Variante 2
                // EXCEPCIÓN: Si es el primer dado de todos, se salva de la multa y gana el bono base.
                if (isFirstDieOnBoard) return new RuleEvaluationResult(0, true);

                if (contactosDiagonales > 0)
                {
                    return new RuleEvaluationResult(contactosDiagonales * CONTACT_BONUS, true);
                }
                else
                {
                    return new RuleEvaluationResult(ISOLATION_PENALTY, false);
                }

            case SpecialRule.RewardOnContact: // Variante 3
                // EXCEPCIÓN: Si es el primer dado de todos, se salva de la multa y gana el bono base.
                if (isFirstDieOnBoard) return new RuleEvaluationResult(0, true);

                if (totalContactos > 0)
                {
                    return new RuleEvaluationResult(totalContactos * CONTACT_BONUS, true);
                }
                else
                {
                    return new RuleEvaluationResult(ISOLATION_PENALTY, false);
                }

            case SpecialRule.None:
            case SpecialRule.ExtraDiagonalContact:
            default:
                return new RuleEvaluationResult(0, true);
        }
    }
}