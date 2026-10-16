using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using MyGame.Core;
using CoreDieColor = MyGame.Core.DieColor; // El DieColor global (GameData.cs) gana sobre 'using MyGame.Core', asi que aliasamos el de Core

namespace MyGame.Networking
{
    /// <summary>
    /// Autoridad de la partida online. El servidor ejecuta toda regla en la Capa 0 (MyGame.Core)
    /// y, tras cada cambio, difunde un NetworkMatchSnapshot completo que los clientes pintan.
    /// Los clientes solo envían intenciones (robar, re-robar, colocar).
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        private readonly Dictionary<ulong, int> clientIdToPlayerIndex = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> playerIndexToClientId = new Dictionary<int, ulong>();

        public NetworkVariable<int> NetworkCurrentPlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetworkMatchPhase = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Slot de jugador de esta máquina (-1 = espectador o aún sin snapshot)
        public int LocalPlayerIndex { get; private set; } = -1;
        public bool IsMatchStarted => matchStarted;

        public event Action<NetworkMatchSnapshot> OnSnapshotApplied;

        private bool matchStarted;
        private NetworkMatchSnapshot pendingSnapshot;
        private bool hasPendingSnapshot;

        private int ExpectedPlayers => GameManager.Instance != null ? GameManager.Instance.numPlayers : 2;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // El host (o clientes muy rápidos) pueden haberse conectado antes de que este objeto spawneara
                foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    RegisterClient(clientId);
                }
                TryStartMatch();
            }

            OnlineMatchPresenter.ResetPresentation();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        private void Update()
        {
            // El snapshot puede llegar antes de que GridManager.Start haya creado los tableros
            if (hasPendingSnapshot && OnlineMatchPresenter.IsSceneReady(pendingSnapshot.Players.Length))
            {
                hasPendingSnapshot = false;
                ApplySnapshot(pendingSnapshot);
            }
        }

        // =========================================================
        // CONEXIONES Y ARRANQUE (solo servidor)
        // =========================================================

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            if (matchStarted)
            {
                // TODO(reconexión): los ClientId cambian al reconectar; hace falta identificar al jugador por PlayerId de UGS
                Debug.LogWarning($"[NetworkGameManager] ClientId {clientId} intentó unirse con la partida ya iniciada. Desconectando.");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            RegisterClient(clientId);
            TryStartMatch();
        }

        private void RegisterClient(ulong clientId)
        {
            if (clientIdToPlayerIndex.ContainsKey(clientId)) return;

            if (clientIdToPlayerIndex.Count >= ExpectedPlayers)
            {
                Debug.LogWarning($"[NetworkGameManager] Sala llena ({ExpectedPlayers}). Rechazando ClientId {clientId}.");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            int slot = 0;
            while (playerIndexToClientId.ContainsKey(slot)) slot++;

            clientIdToPlayerIndex[clientId] = slot;
            playerIndexToClientId[slot] = clientId;
            Debug.Log($"[NetworkGameManager] Cliente conectado. ClientId: {clientId} -> Slot de Jugador: {slot} ({clientIdToPlayerIndex.Count}/{ExpectedPlayers})");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientIdToPlayerIndex.TryGetValue(clientId, out int slot))
            {
                Debug.LogWarning($"[NetworkGameManager] Cliente desconectado. ClientId: {clientId} (Slot: {slot})");

                if (!matchStarted)
                {
                    // En la sala de espera el hueco se libera para otro jugador
                    clientIdToPlayerIndex.Remove(clientId);
                    playerIndexToClientId.Remove(slot);
                    return;
                }

                if (ReconnectionManager.Instance != null)
                {
                    ReconnectionManager.Instance.HandlePlayerDisconnect(slot);
                }
            }
        }

        private void TryStartMatch()
        {
            if (matchStarted || clientIdToPlayerIndex.Count < ExpectedPlayers) return;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.gridManager == null)
            {
                Debug.LogError("[NetworkGameManager] No hay GameManager/GridManager en la escena; no se puede iniciar la partida.");
                return;
            }

            var names = new List<string>(ExpectedPlayers);
            for (int i = 0; i < ExpectedPlayers; i++) names.Add($"Jugador {i + 1}");

            int seed = Environment.TickCount;
            VariantDefDTO variant = gm.currentVariant != null ? gm.currentVariant.ToDTO() : null;

            MatchStateDTO state = CoreMatchSetup.CreateMatch(seed, names, gm.gridManager.rows, gm.gridManager.cols, variant);
            gm.AdoptServerState(state);
            matchStarted = true;

            Debug.Log($"[NetworkGameManager] Partida iniciada. Jugadores: {ExpectedPlayers}, Semilla: {seed}, Dados en bolsa: {state.DiceBag.Count}");

            SyncTurnVariables(state);
            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.MatchStarted });
        }

        public int GetPlayerIndex(ulong clientId)
        {
            return clientIdToPlayerIndex.TryGetValue(clientId, out int index) ? index : -1;
        }

        // =========================================================
        // INTENCIONES DEL CLIENTE
        // =========================================================

        [ServerRpc(RequireOwnership = false)]
        public void RequestDrawDieServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!TryGetActingPlayer(rpcParams, "DrawDie", out int playerIndex, out MatchStateDTO serverState)) return;

            var drawCommand = new DrawDieCommand { PlayerId = playerIndex };
            if (!CoreDrawProcessor.ProcessDrawIntent(serverState, drawCommand, serverState.ServerRNG))
            {
                Debug.LogWarning("[NetworkGameManager] DrawDie rechazado por reglas autoritativas.");
                return;
            }

            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.Drew, PlayerIndex = playerIndex });
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestReDrawServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!TryGetActingPlayer(rpcParams, "ReDraw", out int playerIndex, out MatchStateDTO serverState)) return;

            if (!CoreDrawProcessor.ProcessReDrawIntent(serverState, new ReDrawCommand { PlayerId = playerIndex }, serverState.ServerRNG))
            {
                Debug.LogWarning("[NetworkGameManager] ReDraw rechazado por reglas autoritativas.");
                return;
            }

            // Igual que DiceManager.UseReDraw en local: devolver el dado implica robar otro al instante
            CoreDrawProcessor.ProcessDrawIntent(serverState, new DrawDieCommand { PlayerId = playerIndex }, serverState.ServerRNG);
            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.Drew, PlayerIndex = playerIndex });
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPlaceDieServerRpc(int row, int col, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!TryGetActingPlayer(rpcParams, "Placement", out int playerIndex, out MatchStateDTO serverState)) return;

            if (!serverState.HasDrawn || !serverState.CurrentDrawnColor.HasValue)
            {
                Debug.LogWarning("[NetworkGameManager] Placement rechazado: no hay dado robado disponible.");
                return;
            }

            CoreDieColor color = serverState.CurrentDrawnColor.Value;
            int value = serverState.CurrentDrawnValue.Value;
            PlayerDataDTO profile = serverState.PlayerProfiles[playerIndex];

            // Grupo activo y patrón: salen del estado autoritativo del servidor, nunca del cliente
            if (!profile.ActiveGroups.TryGetValue(color, out GroupDataDTO group))
            {
                Debug.LogError($"[NetworkGameManager] Estado incoherente: el jugador {playerIndex} tiene dado {color} sin grupo activo.");
                return;
            }

            PatternDefDTO pattern = serverState.VariantConfig?.GetPattern(value);
            ScoringConfigDTO sc = serverState.ScoringConfig;
            int scoreBefore = profile.Score;

            // Convención del juego local: X = fila, Y = columna (ver GridManager.GetBoardStateDTO)
            var placeCommand = new PlaceDieCommand
            {
                PlayerId = playerIndex,
                TargetCell = new GridPos(row, col),
                Color = color,
                GroupId = group.Id,
                Number = value
            };

            bool success = CoreMatchProcessor.ProcessPlacementIntent(
                serverState, placeCommand, pattern, pattern,
                sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus,
                sc.RowMultipliers, sc.ColMultipliers);

            if (!success)
            {
                Debug.LogWarning($"[NetworkGameManager] Placement en ({row},{col}) rechazado por reglas autoritativas.");
                NotifyPlacementFailedClientRpc(senderId, "Movimiento no válido según las reglas.");
                return;
            }

            var action = new NetworkActionSnapshot
            {
                Type = NetworkActionType.Placed,
                PlayerIndex = playerIndex,
                Row = row,
                Col = col,
                ScoreDelta = profile.Score - scoreBefore,
                ClosedGroup = profile.ActiveGroups[color].IsClosed
            };

            FinishTurn(serverState);
            BroadcastSnapshot(action);
        }

        private bool TryGetActingPlayer(ServerRpcParams rpcParams, string actionName, out int playerIndex, out MatchStateDTO serverState)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            playerIndex = GetPlayerIndex(senderId);
            serverState = GameManager.Instance != null ? GameManager.Instance.ServerState : null;

            if (!matchStarted || serverState == null || serverState.CurrentPhase != MatchPhase.PlayerTurn)
            {
                Debug.LogWarning($"[NetworkGameManager] {actionName} rechazado: la partida no está en juego.");
                return false;
            }

            if (playerIndex < 0 || playerIndex != serverState.CurrentPlayerIndex)
            {
                Debug.LogWarning($"[NetworkGameManager] {actionName} rechazado. No es el turno de ClientId {senderId}.");
                return false;
            }

            return true;
        }

        // =========================================================
        // TURNOS (solo servidor)
        // =========================================================

        /// <summary>
        /// Cierra el turno actual sin colocar (tiempo agotado o abandono): el dado en mano vuelve a la bolsa.
        /// </summary>
        public void ServerForceEndTurn()
        {
            if (!IsServer || !matchStarted) return;

            MatchStateDTO state = GameManager.Instance.ServerState;
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return;

            int playerIndex = state.CurrentPlayerIndex;
            CoreMatchSetup.DiscardDrawnDie(state);
            FinishTurn(state);
            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.TurnForced, PlayerIndex = playerIndex });
        }

        /// <summary>
        /// Difunde el estado tras un cambio hecho fuera de un RPC (p. ej. eliminación por abandono).
        /// </summary>
        public void ServerPushState()
        {
            if (!IsServer || !matchStarted) return;

            MatchStateDTO state = GameManager.Instance.ServerState;
            CoreSessionProcessor.EvaluateSessionState(state, GameManager.Instance.maxDicePerPlayer);
            SyncTurnVariables(state, restartTimer: false);
            BroadcastSnapshot(default);
        }

        private void FinishTurn(MatchStateDTO state)
        {
            CoreSessionProcessor.EvaluateSessionState(state, GameManager.Instance.maxDicePerPlayer);

            if (state.CurrentPhase != MatchPhase.GameOver)
            {
                CoreMatchSetup.AdvanceTurn(state);

                // Bolsa vacía sin dado en mano: nadie puede volver a jugar, se cierra la partida.
                // maxDicePerPlayer = 0 fuerza a EvaluateSessionState a aplicar las multas finales.
                if (state.CurrentPhase != MatchPhase.GameOver && state.DiceBag.Count == 0)
                {
                    CoreSessionProcessor.EvaluateSessionState(state, 0);
                }
            }

            SyncTurnVariables(state);
        }

        private void SyncTurnVariables(MatchStateDTO state, bool restartTimer = true)
        {
            NetworkCurrentPlayerIndex.Value = state.CurrentPlayerIndex;
            NetworkMatchPhase.Value = (int)state.CurrentPhase;

            if (NetworkTurnManager.Instance != null)
            {
                if (state.CurrentPhase == MatchPhase.PlayerTurn && restartTimer)
                    NetworkTurnManager.Instance.AdvanceTurn(state.CurrentPlayerIndex);
                else if (state.CurrentPhase != MatchPhase.PlayerTurn)
                    NetworkTurnManager.Instance.StopTurnTimer();
            }
        }

        // =========================================================
        // REPLICACIÓN
        // =========================================================

        private void BroadcastSnapshot(NetworkActionSnapshot action)
        {
            var slots = new ulong[ExpectedPlayers];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = playerIndexToClientId.TryGetValue(i, out ulong id) ? id : ulong.MaxValue;
            }

            NetworkMatchSnapshot snapshot = NetworkMatchSnapshot.FromState(GameManager.Instance.ServerState, slots, action);
            ReceiveSnapshotClientRpc(snapshot);
        }

        [ClientRpc]
        private void ReceiveSnapshotClientRpc(NetworkMatchSnapshot snapshot)
        {
            LocalPlayerIndex = Array.IndexOf(snapshot.SlotClientIds, NetworkManager.Singleton.LocalClientId);

            if (!OnlineMatchPresenter.IsSceneReady(snapshot.Players.Length))
            {
                // Si llegan varios antes de estar listos, basta con el más reciente (es un estado completo)
                pendingSnapshot = snapshot;
                hasPendingSnapshot = true;
                return;
            }

            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(NetworkMatchSnapshot snapshot)
        {
            OnlineMatchPresenter.Apply(snapshot, LocalPlayerIndex, IsServer);
            OnSnapshotApplied?.Invoke(snapshot);
        }

        [ClientRpc]
        private void NotifyPlacementFailedClientRpc(ulong targetClientId, string reason)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                Debug.LogWarning($"[Cliente Local] Placement rechazado por el servidor: {reason}");
                if (PopUpManager.Instance != null)
                {
                    PopUpManager.Instance.ShowPopUp(Vector3.up * 2f, "Movimiento no válido", Color.red);
                }
            }
        }
    }
}
