using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    /// <summary>
    /// DTO puro del servidor para una variante de juego.
    /// Reemplaza el ScriptableObject VariantData en contextos sin Unity (servidor headless).
    /// </summary>
    [Serializable]
    public class VariantDefDTO
    {
        public string VariantName { get; set; }

        /// <summary>
        /// Diccionario: número de dado (1-6) -> definición del patrón para ese número.
        /// </summary>
        public Dictionary<int, PatternDefDTO> Patterns { get; set; }

        public VariantDefDTO()
        {
            Patterns = new Dictionary<int, PatternDefDTO>();
        }

        /// <summary>
        /// Obtiene el patrón para un número de dado dado. Retorna null si no existe.
        /// Equivalente servidor de VariantData.GetPattern(int).
        /// </summary>
        public PatternDefDTO GetPattern(int diceNumber)
        {
            Patterns.TryGetValue(diceNumber, out PatternDefDTO pattern);
            return pattern;
        }
    }
}
