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

        // ---- Patrones: bono por tamano de grupo (indices 1..6) y bono unico por cerrar los seis tamanos ----
        public int[] PatternBonuses      { get; set; }
        public int CollectionBonus       { get; set; }

        // ---- Huecos cerrados al acabar: puntos negativos por casilla y tope total (el tope 0 = sin tope) ----
        public int GapPenaltyPerCell     { get; set; }
        public int GapPenaltyCap         { get; set; }

        /// <summary>true = escalones antiguos (-500 / -700 por casilla / -1000 por casilla), solo para comparar en el benchmark.</summary>
        public bool LegacyGapTiers       { get; set; }

        private static readonly ScoringConfigDTO shared = Default();

        /// <summary>Instancia compartida de solo lectura para los calculos que no reciben configuracion.</summary>
        public static ScoringConfigDTO Shared => shared;

        /// <summary>
        /// Configuracion estandar de Dharitz. Los patrones son la fuente principal de puntos; las lineas (filas y
        /// columnas) son un extra moderado y los huecos penalizan con tope.
        /// </summary>
        public static ScoringConfigDTO Default() => new ScoringConfigDTO
        {
            PointsPerDie        = 25,
            RowCompleteBonus    = 80,
            ColCompleteBonus    = 100,
            IntersectionBonus   = 100,
            RowMultipliers      = new float[] { 1.0f, 1.3f, 1.6f, 2.0f },
            ColMultipliers      = new float[] { 1.0f, 1.4f, 1.8f, 2.2f, 2.6f },
            PatternBonuses      = new int[] { 0, 100, 150, 250, 400, 600, 850 },
            CollectionBonus     = 1200,
            GapPenaltyPerCell   = 300,
            GapPenaltyCap       = 1500,
            LegacyGapTiers      = false
        };

        /// <summary>Valores previos al reequilibrio de puntos; se conservan para comparar en el benchmark.</summary>
        public static ScoringConfigDTO Legacy() => new ScoringConfigDTO
        {
            PointsPerDie        = 50,
            RowCompleteBonus    = 120,
            ColCompleteBonus    = 170,
            IntersectionBonus   = 100,
            RowMultipliers      = new float[] { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f },
            ColMultipliers      = new float[] { 1.0f, 2.0f, 3.0f, 3.5f, 5.0f, 6.0f },
            PatternBonuses      = new int[] { 0, 100, 100, 200, 300, 400, 500 },
            CollectionBonus     = 0,
            GapPenaltyPerCell   = 0,
            GapPenaltyCap       = 0,
            LegacyGapTiers      = true
        };

        /// <summary>Bono de un patron cerrado segun el tamano del grupo.</summary>
        public int GetPatternBonus(int targetSize)
        {
            if (PatternBonuses == null || targetSize < 0 || targetSize >= PatternBonuses.Length) return 0;
            return PatternBonuses[targetSize];
        }

        /// <summary>Penalizacion (negativa) de UN hueco cerrado de ese tamano, sin tope.</summary>
        public int GapPenaltyFor(int gapSize)
        {
            if (gapSize <= 0) return 0;
            if (LegacyGapTiers)
            {
                if (gapSize == 1) return -500;
                if (gapSize == 2) return gapSize * -700;
                return gapSize * -1000;
            }

            return -GapPenaltyPerCell * gapSize;
        }

        /// <summary>Penalizacion total (negativa) de todos los huecos cerrados, aplicando el tope.</summary>
        public int GapPenaltyTotal(System.Collections.Generic.IEnumerable<int> gapSizes)
        {
            int total = 0;
            foreach (int size in gapSizes) total += GapPenaltyFor(size);
            if (GapPenaltyCap > 0 && total < -GapPenaltyCap) total = -GapPenaltyCap;
            return total;
        }
    }
}
