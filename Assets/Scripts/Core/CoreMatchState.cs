using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    // 1. Fases estrictas del motor de juego
    [Serializable]
    public enum MatchPhase
    {
        WaitingForPlayers,
        PlayerTurn,
        Resolution,
        GameOver
    }

    // 2. El comando de intención (Lo único que el cliente enviará al servidor)
    [Serializable]
    public struct PlaceDieCommand
    {
        public int PlayerId;
        public GridPos TargetCell;
        public DieColor Color;
        public int GroupId;
        public int Number;
    }

    // 3. La fuente de verdad absoluta de la sesión
    [Serializable]
    public class MatchStateDTO
    {
        public string MatchId { get; set; }
        public MatchPhase CurrentPhase { get; set; }
        public int CurrentPlayerIndex { get; set; }
        public int TurnNumber { get; set; }

        public bool HasDrawn { get; set; }
        public DieColor? CurrentDrawnColor { get; set; }
        public int? CurrentDrawnValue { get; set; }
        public List<DieColor> DiceBag { get; set; }

        // El estado de los tableros de todos los jugadores
        public Dictionary<int, BoardStateDTO> PlayerBoards { get; set; }

        // El estado de los perfiles y puntajes de todos los jugadores
        public Dictionary<int, PlayerDataDTO> PlayerProfiles { get; set; }

        // =========================================================
        // NUEVOS CAMPOS (F0.4 y F0.5): Configuración autoritativa
        // =========================================================

        /// <summary>
        /// El único generador de aleatoriedad de la partida.
        /// Vive SOLO en el servidor — los clientes nunca lo tocan.
        /// [NonSerialized] porque System.Random no es serializable por red;
        /// se recrea en el servidor con la misma Seed al reconectar.
        /// </summary>
        [NonSerialized]
        public System.Random ServerRNG;

        /// <summary>
        /// Semilla determinista de la partida.
        /// Se envía al cliente para reproducibilidad y replays futuros.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Definición de la variante seleccionada (patrones, reglas).
        /// Serializada desde el ScriptableObject VariantData.ToDTO() al inicio.
        /// </summary>
        public VariantDefDTO VariantConfig { get; set; }

        /// <summary>
        /// Configuración de puntaje (bonos, multiplicadores).
        /// Inyectada por el servidor al inicio — los clientes no pueden alterarla.
        /// </summary>
        public ScoringConfigDTO ScoringConfig { get; set; }

        public MatchStateDTO()
        {
            PlayerBoards = new Dictionary<int, BoardStateDTO>();
            PlayerProfiles = new Dictionary<int, PlayerDataDTO>();
            CurrentPhase = MatchPhase.WaitingForPlayers;
            TurnNumber = 1;
            // El ScoringConfig se inicializa con valores estándar por defecto
            ScoringConfig = ScoringConfigDTO.Default();
        }

        /// <summary>
        /// Inicializa el RNG del servidor con una semilla determinista.
        /// Llamar una sola vez al inicio de la partida.
        /// </summary>
        public void InitializeRNG(int seed)
        {
            Seed = seed;
            ServerRNG = new System.Random(seed);
        }
    }
}