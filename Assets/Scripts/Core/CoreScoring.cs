namespace MyGame.Core
{
    /// <summary>Reglas de puntos de los patrones: bono por tamano y bono de coleccion (los seis tamanos al menos una vez).</summary>
    public static class CoreScoring
    {
        /// <summary>Registra un patron cerrado del tamano indicado. Devuelve los puntos a sumar (bono + coleccion si procede).</summary>
        public static int AwardPattern(PlayerDataDTO player, int size, ScoringConfigDTO scoring)
        {
            if (size < 1 || size > 6) return 0;

            player.PatternCounts[size]++;
            int points = scoring.GetPatternBonus(size);
            if (player.PatternCounts[size] == 1 && OthersComplete(player.PatternCounts, size)) points += scoring.CollectionBonus;
            return points;
        }

        /// <summary>Revierte AwardPattern. Devuelve los puntos a restar.</summary>
        public static int RevokePattern(PlayerDataDTO player, int size, ScoringConfigDTO scoring)
        {
            if (size < 1 || size > 6 || player.PatternCounts[size] <= 0) return 0;

            bool wasLast = player.PatternCounts[size] == 1 && OthersComplete(player.PatternCounts, size);
            player.PatternCounts[size]--;
            return scoring.GetPatternBonus(size) + (wasLast ? scoring.CollectionBonus : 0);
        }

        private static bool OthersComplete(int[] counts, int except)
        {
            for (int n = 1; n <= 6; n++)
            {
                if (n != except && counts[n] < 1) return false;
            }

            return true;
        }
    }
}
