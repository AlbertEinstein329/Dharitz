using System.Collections.Generic;
using Unity.Netcode;
using MyGame.Core;
using CoreDieColor = MyGame.Core.DieColor; // El DieColor global (GameData.cs) gana sobre 'using MyGame.Core'

namespace MyGame.Networking
{
    public enum NetworkActionType : byte
    {
        None,
        MatchStarted,
        Drew,
        Placed,
        TurnForced
    }

    /// <summary>
    /// Último evento autoritativo, para que el cliente reproduzca el feedback (popup, sonido, celebración).
    /// </summary>
    public struct NetworkActionSnapshot : INetworkSerializable
    {
        public NetworkActionType Type;
        public int PlayerIndex;
        public int Row;
        public int Col;
        public int ScoreDelta;
        public bool ClosedGroup;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref PlayerIndex);
            serializer.SerializeValue(ref Row);
            serializer.SerializeValue(ref Col);
            serializer.SerializeValue(ref ScoreDelta);
            serializer.SerializeValue(ref ClosedGroup);
        }
    }

    public struct NetworkGroupSnapshot : INetworkSerializable
    {
        public byte Color;
        public int Id;
        public byte TargetSize;
        public short[] Cells; // Índice plano r * cols + c

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Color);
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref TargetSize);
            NetworkMatchSnapshot.SerializeArray(serializer, ref Cells);
        }
    }

    public struct NetworkPlayerSnapshot : INetworkSerializable
    {
        public string Name;
        public int Score;
        public int PlacedDice;
        public int ReDraws;
        public int UndoUses;
        public int MoveUses;
        public bool IsEliminated;
        public int[] PatternCounts;
        public NetworkBoardState Board;
        public NetworkGroupSnapshot[] Groups;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter && Name == null) Name = string.Empty;
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Score);
            serializer.SerializeValue(ref PlacedDice);
            serializer.SerializeValue(ref ReDraws);
            serializer.SerializeValue(ref UndoUses);
            serializer.SerializeValue(ref MoveUses);
            serializer.SerializeValue(ref IsEliminated);
            NetworkMatchSnapshot.SerializeArray(serializer, ref PatternCounts);
            Board.NetworkSerialize(serializer);

            int groupCount = serializer.IsWriter ? (Groups?.Length ?? 0) : 0;
            serializer.SerializeValue(ref groupCount);
            if (serializer.IsReader) Groups = new NetworkGroupSnapshot[groupCount];
            for (int i = 0; i < groupCount; i++) Groups[i].NetworkSerialize(serializer);
        }
    }

    /// <summary>
    /// Fotografía pública completa de la partida, enviada por el servidor tras cada acción.
    /// El orden de la bolsa NO viaja (solo el conteo por color), para que un cliente no pueda predecir robos.
    /// Al ser un estado completo y no un delta, un cliente que se pierde un mensaje se corrige con el siguiente.
    /// </summary>
    public struct NetworkMatchSnapshot : INetworkSerializable
    {
        public byte Phase;
        public int CurrentPlayerIndex;
        public int TurnNumber;
        public bool HasDrawn;
        public sbyte DrawnColor; // -1 = sin dado en mano
        public byte DrawnValue;
        public int[] BagCountsByColor; // Red, Blue, White, Black
        public ulong[] SlotClientIds;  // ClientId de Netcode que ocupa cada slot de jugador
        public NetworkPlayerSnapshot[] Players;
        public NetworkActionSnapshot LastAction;

        public int BagTotal
        {
            get
            {
                int total = 0;
                if (BagCountsByColor != null) foreach (int n in BagCountsByColor) total += n;
                return total;
            }
        }

        public static NetworkMatchSnapshot FromState(MatchStateDTO state, ulong[] slotClientIds, NetworkActionSnapshot lastAction)
        {
            var snap = new NetworkMatchSnapshot
            {
                Phase = (byte)state.CurrentPhase,
                CurrentPlayerIndex = state.CurrentPlayerIndex,
                TurnNumber = state.TurnNumber,
                HasDrawn = state.HasDrawn,
                DrawnColor = state.CurrentDrawnColor.HasValue ? (sbyte)state.CurrentDrawnColor.Value : (sbyte)-1,
                DrawnValue = (byte)(state.CurrentDrawnValue ?? 0),
                BagCountsByColor = new int[4],
                SlotClientIds = slotClientIds,
                Players = new NetworkPlayerSnapshot[state.PlayerProfiles.Count],
                LastAction = lastAction
            };

            if (state.DiceBag != null)
            {
                foreach (CoreDieColor color in state.DiceBag) snap.BagCountsByColor[(int)color]++;
            }

            for (int i = 0; i < snap.Players.Length; i++)
            {
                PlayerDataDTO profile = state.PlayerProfiles[i];
                BoardStateDTO board = state.PlayerBoards[i];

                var groups = new List<NetworkGroupSnapshot>();
                foreach (var kvp in profile.ActiveGroups)
                {
                    GroupDataDTO g = kvp.Value;
                    if (g == null) continue;

                    var cells = new short[g.OccupiedCells.Count];
                    int k = 0;
                    foreach (GridPos pos in g.OccupiedCells) cells[k++] = (short)board.GetIndex(pos.X, pos.Y);

                    groups.Add(new NetworkGroupSnapshot
                    {
                        Color = (byte)g.Color,
                        Id = g.Id,
                        TargetSize = (byte)g.TargetSize,
                        Cells = cells
                    });
                }

                snap.Players[i] = new NetworkPlayerSnapshot
                {
                    Name = profile.Name,
                    Score = profile.Score,
                    PlacedDice = profile.PlacedDice,
                    ReDraws = profile.ReDraws,
                    UndoUses = profile.CurrentUndoUses,
                    MoveUses = profile.CurrentMoveUses,
                    IsEliminated = profile.IsEliminated,
                    PatternCounts = (int[])profile.PatternCounts.Clone(),
                    Board = new NetworkBoardState(i, board),
                    Groups = groups.ToArray()
                };
            }

            return snap;
        }

        /// <summary>
        /// Reconstruye un MatchStateDTO espejo para el cliente. La bolsa se rellena con los conteos
        /// en orden fijo (el orden real es secreto del servidor); sirve para contadores y "bolsa vacía".
        /// </summary>
        public MatchStateDTO ToMatchState()
        {
            var state = new MatchStateDTO
            {
                CurrentPhase = (MatchPhase)Phase,
                CurrentPlayerIndex = CurrentPlayerIndex,
                TurnNumber = TurnNumber,
                HasDrawn = HasDrawn,
                CurrentDrawnColor = DrawnColor >= 0 ? (CoreDieColor?)(CoreDieColor)DrawnColor : null,
                CurrentDrawnValue = DrawnColor >= 0 ? (int?)DrawnValue : null,
                DiceBag = new List<CoreDieColor>()
            };

            for (int color = 0; color < BagCountsByColor.Length; color++)
            {
                for (int n = 0; n < BagCountsByColor[color]; n++) state.DiceBag.Add((CoreDieColor)color);
            }

            for (int i = 0; i < Players.Length; i++)
            {
                NetworkPlayerSnapshot ps = Players[i];
                BoardStateDTO board = ps.Board.ToDTO();

                var profile = new PlayerDataDTO(i, ps.Name)
                {
                    Score = ps.Score,
                    PlacedDice = ps.PlacedDice,
                    ReDraws = ps.ReDraws,
                    CurrentUndoUses = ps.UndoUses,
                    CurrentMoveUses = ps.MoveUses,
                    IsEliminated = ps.IsEliminated,
                    PatternCounts = (int[])ps.PatternCounts.Clone()
                };

                foreach (NetworkGroupSnapshot g in ps.Groups)
                {
                    var group = new GroupDataDTO(g.Id, (CoreDieColor)g.Color, g.TargetSize);
                    foreach (short cell in g.Cells) group.OccupiedCells.Add(board.GetPos(cell));
                    profile.ActiveGroups[group.Color] = group;
                }

                state.PlayerProfiles[i] = profile;
                state.PlayerBoards[i] = board;
            }

            return state;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref CurrentPlayerIndex);
            serializer.SerializeValue(ref TurnNumber);
            serializer.SerializeValue(ref HasDrawn);
            serializer.SerializeValue(ref DrawnColor);
            serializer.SerializeValue(ref DrawnValue);
            SerializeArray(serializer, ref BagCountsByColor);
            SerializeArray(serializer, ref SlotClientIds);

            int playerCount = serializer.IsWriter ? (Players?.Length ?? 0) : 0;
            serializer.SerializeValue(ref playerCount);
            if (serializer.IsReader) Players = new NetworkPlayerSnapshot[playerCount];
            for (int i = 0; i < playerCount; i++) Players[i].NetworkSerialize(serializer);

            LastAction.NetworkSerialize(serializer);
        }

        // Arrays con prefijo de longitud; un array nulo viaja como vacío
        internal static void SerializeArray<T>(BufferSerializer<T> serializer, ref int[] array) where T : IReaderWriter
        {
            int length = serializer.IsWriter ? (array?.Length ?? 0) : 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader) array = new int[length];
            for (int i = 0; i < length; i++) serializer.SerializeValue(ref array[i]);
        }

        internal static void SerializeArray<T>(BufferSerializer<T> serializer, ref short[] array) where T : IReaderWriter
        {
            int length = serializer.IsWriter ? (array?.Length ?? 0) : 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader) array = new short[length];
            for (int i = 0; i < length; i++) serializer.SerializeValue(ref array[i]);
        }

        internal static void SerializeArray<T>(BufferSerializer<T> serializer, ref ulong[] array) where T : IReaderWriter
        {
            int length = serializer.IsWriter ? (array?.Length ?? 0) : 0;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader) array = new ulong[length];
            for (int i = 0; i < length; i++) serializer.SerializeValue(ref array[i]);
        }
    }
}
