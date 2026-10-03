using System;

namespace MyGame.Core
{
    /// <summary>
    /// Configuración de puntaje serializable para el servidor.
    /// El servidor inyecta esta config al MatchStateDTO al inicio de la partida.
    /// Los clientes nunca pueden modificarla — elimina las constantes hardcoded dispersas.
    /// </summary>
    [Serializable]
    public class ScoringConfigDTO
    {
        // ---- Bonos de líneas ----
        public int RowCompleteBonus      { get; set; }
        public int ColCompleteBonus      { get; set; }
        public int IntersectionBonus     { get; set; }

        // ---- Puntos base por dado colocado ----
        public int PointsPerDie          { get; set; }

        // ---- Multiplicadores de filas consecutivas ----
        public float[] RowMultipliers    { get; set; }

        // ---- Multiplicadores de columnas consecutivas ----
        public float[] ColMultipliers    { get; set; }

        /// <summary>
        /// Crea la configuración estándar de Dharitz.
        /// Migrado desde ScoreManager.cs para centralizar la fuente de verdad.
        /// </summary>
        public static ScoringConfigDTO Default() => new ScoringConfigDTO
        {
            PointsPerDie        = 50,
            RowCompleteBonus    = 120,
            ColCompleteBonus    = 170,
            IntersectionBonus   = 100,
            RowMultipliers      = new float[] { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f },
            ColMultipliers      = new float[] { 1.0f, 2.0f, 3.0f, 3.5f, 5.0f, 6.0f }
        };

        /// <summary>
        /// Calcula el bono de patrón según el tamaño del grupo completado.
        /// Migrado desde ScoreManager.GetPatternBonus().
        /// </summary>
        public int GetPatternBonus(int targetSize)
        {
            switch (targetSize)
            {
                case 1:
                case 2: return 100;
                case 3: return 200;
                case 4: return 300;
                case 5: return 400;
                case 6: return 500;
                default: return 0;
            }
        }
    }
}
