namespace MyGame.Core
{
    /// <summary>Dificultad del bot. Coincide con las variantes: Easy, Normal y Hard.</summary>
    public enum BotDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// Que busca el bot y cuanto piensa. Las tres dificultades usan el mismo Monte Carlo ligero; lo que cambia es el
    /// objetivo (pesos), cuanto simulan y si a veces se equivocan:
    /// Facil = intenta formar patrones; Normal = patrones y evitar huecos a toda costa; Dificil = patrones, filas y
    /// columnas y evitar huecos.
    /// </summary>
    public sealed class BotProfile
    {
        public string Name;

        // Cuanto piensa (ligero: pensado para moviles)
        public int BudgetMs;
        public int MaxRounds;
        public int Finalists;
        public int Horizon;

        /// <summary>Fraccion del bono de filas y columnas que el bot valora (0 = las ignora, 1 = completo).</summary>
        public float LineWeight;

        /// <summary>Fraccion del bono de un patron abierto que se acredita por avanzar hacia el (y se pierde al romperlo).</summary>
        public float PatternWeight;

        /// <summary>Empujon posicional a rellenar filas y columnas.</summary>
        public float LineHint;

        /// <summary>Multiplo del coste de las casillas encerradas (huecos): 1 = lo que cuestan de verdad al acabar.</summary>
        public float GapWeight;

        /// <summary>Castigo por dejar un grupo imposible de completar.</summary>
        public float DoomedPenalty;

        /// <summary>Probabilidad de jugar una de las otras mejores opciones en vez de la mejor (un bot flojo no siempre ve lo ideal).</summary>
        public float MistakeChance;

        public static BotProfile For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Hard: return Hard();
                case BotDifficulty.Normal: return Normal();
                default: return Easy();
            }
        }

        /// <summary>Facil: forma patrones, no se preocupa mucho de los huecos ni de las lineas y a veces falla.</summary>
        public static BotProfile Easy() => new BotProfile
        {
            Name = "Easy",
            BudgetMs = 120, MaxRounds = 8, Finalists = 4, Horizon = 4,
            LineWeight = 0f, PatternWeight = 0.6f, LineHint = 0f,
            GapWeight = 0.5f, DoomedPenalty = 100f, MistakeChance = 0.25f
        };

        /// <summary>Normal: forma patrones y evita los huecos a toda costa; ignora las lineas.</summary>
        public static BotProfile Normal() => new BotProfile
        {
            Name = "Normal",
            BudgetMs = 200, MaxRounds = 14, Finalists = 6, Horizon = 6,
            LineWeight = 0f, PatternWeight = 0.8f, LineHint = 0f,
            GapWeight = 4f, DoomedPenalty = 300f, MistakeChance = 0.08f
        };

        /// <summary>Dificil: patrones, filas y columnas, y evita los huecos; no se equivoca.</summary>
        public static BotProfile Hard() => new BotProfile
        {
            Name = "Hard",
            BudgetMs = 300, MaxRounds = 24, Finalists = 8, Horizon = 8,
            LineWeight = 1f, PatternWeight = 0.8f, LineHint = 12f,
            GapWeight = 1.5f, DoomedPenalty = 300f, MistakeChance = 0f
        };

        /// <summary>Valores de la primera version del bot (niveles Greedy / Penalizado / Monte Carlo): solo para comparar.</summary>
        public static BotProfile Legacy() => new BotProfile
        {
            Name = "Legacy",
            BudgetMs = BotBrain.DefaultBudgetMs, MaxRounds = 40, Finalists = 8, Horizon = 8,
            LineWeight = 0.5f, PatternWeight = 0.8f, LineHint = 12f,
            GapWeight = 0.5f, DoomedPenalty = 150f, MistakeChance = 0f
        };
    }
}
