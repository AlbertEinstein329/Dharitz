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

        // Identidad estable del jugador (PlayerId de UGS): sobrevive a los cambios de ClientId al reconectar
        private readonly Dictionary<string, int> playerIdToSlot = new Dictionary<string, int>();

        public NetworkVariable<int> NetworkCurrentPlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetworkMatchPhase = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Slot de jugador de esta máquina (-1 = espectador o aún sin snapshot)
        public int LocalPlayerIndex { get; private set; } = -1;
        public bool IsMatchStarted => matchStarted;

        public event Action<NetworkMatchSnapshot> OnSnapshotApplied;

        /// <summary>El servidor rechazo una jugada de ESTE cliente (colocar, mover o deshacer).</summary>
        public event Action OnPlacementRejected;

        // Ultimo estado aplicado en esta maquina (para que la UI sepa si hay que confirmar, deshacer, etc.)
        public NetworkMatchSnapshot LastSnapshot { get; private set; }
        public bool HasSnapshot { get; private set; }

        // Antitrampas (solo servidor): ritmo maximo de peticiones por cliente y expulsion por reincidencia.
        // Una partida normal genera pocas acciones por segundo; esto solo corta spam, automatizacion o clientes modificados.
        private const int MaxPlayerIdLength = 64;
        private readonly CoreRateLimiter actionLimiter = new CoreRateLimiter(capacity: 8, refillPerSecond: 4, maxStrikes: 20, strikeWindowSeconds: 10);
        private readonly CoreRateLimiter registerLimiter = new CoreRateLimiter(capacity: 3, refillPerSecond: 0.2, maxStrikes: 5, strikeWindowSeconds: 30);
        private readonly Dictionary<ulong, double> lastRateLogTime = new Dictionary<ulong, double>();

        // Fase de confirmar (solo servidor): tras colocar, el turno sigue abierto hasta confirmar o agotar el tiempo.
        // La pila permite reversar acciones del turno (Undo) y se vacia al robar o cambiar de turno.
        private readonly List<TurnCheckpoint> turnHistory = new List<TurnCheckpoint>();
        private bool turnPlaced;

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
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            // Cada maquina (host incluido) se identifica ante el servidor con su PlayerId de UGS.
            // El slot se asigna al registrarse, no al conectar, para poder reconocer a quien vuelve.
            if (IsClient)
            {
                RegisterPlayerServerRpc(GetLocalPlayerId());
            }

            OnlineMatchPresenter.ResetPresentation();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
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
        // CONEXIONES, REGISTRO Y ARRANQUE (solo servidor)
        // =========================================================

        private static string GetLocalPlayerId()
        {
            try
            {
                var auth = Unity.Services.Authentication.AuthenticationService.Instance;
                if (auth != null && auth.IsSignedIn) return auth.PlayerId;
            }
            catch (Exception) { /* servicios no inicializados: se usa un id efimero */ }

            return "local-" + NetworkManager.Singleton.LocalClientId;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RegisterPlayerServerRpc(string playerId, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!PassesLimiter(registerLimiter, clientId, "RegisterPlayer")) return;

            if (playerId != null && playerId.Length > MaxPlayerIdLength)
            {
                Debug.LogWarning($"[NetworkGameManager] PlayerId de {playerId.Length} caracteres rechazado. Expulsando ClientId {clientId}.");
                NetworkManager.Singleton.DisconnectClient(clientId, "PlayerId invalido");
                return;
            }

            if (string.IsNullOrEmpty(playerId)) playerId = "client-" + clientId;

            // Mismo jugador que vuelve (o sigue conectado con otro ClientId): recupera su slot
            if (playerIdToSlot.TryGetValue(playerId, out int knownSlot))
            {
                RebindPlayer(playerId, knownSlot, clientId);
                return;
            }

            if (matchStarted)
            {
                Debug.LogWarning($"[NetworkGameManager] {playerId} intento unirse con la partida ya iniciada. Desconectando.");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            if (playerIdToSlot.Count >= ExpectedPlayers)
            {
                Debug.LogWarning($"[NetworkGameManager] Sala llena ({ExpectedPlayers}). Rechazando ClientId {clientId}.");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            int slot = 0;
            while (playerIndexToClientId.ContainsKey(slot) || playerIdToSlot.ContainsValue(slot)) slot++;

            playerIdToSlot[playerId] = slot;
            clientIdToPlayerIndex[clientId] = slot;
            playerIndexToClientId[slot] = clientId;
            Debug.Log($"[NetworkGameManager] Jugador registrado. ClientId {clientId} -> Slot {slot} ({playerIdToSlot.Count}/{ExpectedPlayers})");

            TryStartMatch();
        }

        private void RebindPlayer(string playerId, int slot, ulong newClientId)
        {
            if (matchStarted && GameManager.Instance.ServerState.PlayerProfiles.TryGetValue(slot, out PlayerDataDTO profile) && profile.IsEliminated)
            {
                Debug.LogWarning($"[NetworkGameManager] {playerId} (slot {slot}) estaba eliminado por abandono. No puede volver.");
                NetworkManager.Singleton.DisconnectClient(newClientId);
                return;
            }

            // Libera el ClientId anterior de este slot (si la caida aun no se habia detectado)
            if (playerIndexToClientId.TryGetValue(slot, out ulong oldClientId) && oldClientId != newClientId)
            {
                clientIdToPlayerIndex.Remove(oldClientId);
            }

            clientIdToPlayerIndex[newClientId] = slot;
            playerIndexToClientId[slot] = newClientId;
            Debug.Log($"[NetworkGameManager] Jugador {playerId} reconectado. Slot {slot} -> ClientId {newClientId}");

            if (!matchStarted) return;

            if (ReconnectionManager.Instance != null) ReconnectionManager.Instance.HandlePlayerReconnect(slot);

            // Snapshot completo para que el cliente que vuelve recupere tablero, mano y turno
            BroadcastSnapshot(default);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            actionLimiter.Forget(clientId);
            registerLimiter.Forget(clientId);
            lastRateLogTime.Remove(clientId);

            if (!clientIdToPlayerIndex.TryGetValue(clientId, out int slot)) return;

            Debug.LogWarning($"[NetworkGameManager] Cliente desconectado. ClientId: {clientId} (Slot: {slot})");
            clientIdToPlayerIndex.Remove(clientId);

            // Solo se libera el slot si ese ClientId seguia siendo el vigente (no un reemplazo ya registrado)
            if (playerIndexToClientId.TryGetValue(slot, out ulong current) && current != clientId) return;
            playerIndexToClientId.Remove(slot);

            if (!matchStarted)
            {
                // En la sala de espera el hueco se libera para otro jugador
                string key = null;
                foreach (var kvp in playerIdToSlot) if (kvp.Value == slot) { key = kvp.Key; break; }
                if (key != null) playerIdToSlot.Remove(key);
                return;
            }

            if (ReconnectionManager.Instance != null)
            {
                ReconnectionManager.Instance.HandlePlayerDisconnect(slot);
            }
        }

        private void TryStartMatch()
        {
            if (matchStarted || playerIdToSlot.Count < ExpectedPlayers) return;

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

            if (turnPlaced)
            {
                Debug.LogWarning("[NetworkGameManager] DrawDie rechazado: ya colocaste este turno. Confirma el turno.");
                return;
            }

            var drawCommand = new DrawDieCommand { PlayerId = playerIndex };
            if (!CoreDrawProcessor.ProcessDrawIntent(serverState, drawCommand, serverState.ServerRNG))
            {
                Debug.LogWarning("[NetworkGameManager] DrawDie rechazado por reglas autoritativas.");
                return;
            }

            turnHistory.Clear(); // robar confirma lo hecho antes, igual que en local

            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.Drew, PlayerIndex = playerIndex });
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestReDrawServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!TryGetActingPlayer(rpcParams, "ReDraw", out int playerIndex, out MatchStateDTO serverState)) return;

            if (turnPlaced)
            {
                Debug.LogWarning("[NetworkGameManager] ReDraw rechazado: ya colocaste este turno.");
                return;
            }

            if (!CoreDrawProcessor.ProcessReDrawIntent(serverState, new ReDrawCommand { PlayerId = playerIndex }, serverState.ServerRNG))
            {
                Debug.LogWarning("[NetworkGameManager] ReDraw rechazado por reglas autoritativas.");
                return;
            }

            turnHistory.Clear();

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

            // Las coordenadas llegan del cliente: se validan contra el tablero real antes de tocar nada
            BoardStateDTO placeBoard = serverState.PlayerBoards[playerIndex];
            if (!IsInsideBoard(placeBoard, row, col))
            {
                Debug.LogWarning($"[NetworkGameManager] Placement fuera de tablero ({row},{col}) rechazado.");
                NotifyPlacementFailedClientRpc(senderId, "Casilla fuera del tablero.");
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

            // Punto de restauracion previo a colocar: permite deshacer mientras el turno siga abierto
            TurnCheckpoint checkpoint = CoreTurnActions.Capture(serverState, playerIndex, true);

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
                sc.RowMultipliers, sc.ColMultipliers,
                out PlacementResult placement);

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
                ClosedGroup = placement.GroupClosed,
                PatternCompleted = placement.PatternMatched
            };

            // El dado queda colocado pero el turno sigue abierto: Undo / Move / Confirmar
            turnHistory.Add(checkpoint);
            CoreTurnActions.ConsumeHand(serverState);
            turnPlaced = true;

            BroadcastSnapshot(action);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestConfirmTurnServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!TryGetActingPlayer(rpcParams, "ConfirmTurn", out int playerIndex, out MatchStateDTO serverState)) return;

            if (!turnPlaced)
            {
                Debug.LogWarning("[NetworkGameManager] ConfirmTurn rechazado: aun no has colocado un dado.");
                return;
            }

            FinishTurn(serverState);
            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.TurnConfirmed, PlayerIndex = playerIndex });
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestUndoServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!TryGetActingPlayer(rpcParams, "Undo", out int playerIndex, out MatchStateDTO serverState)) return;

            bool undoingPlacement = turnHistory.Count > 0 && turnHistory[turnHistory.Count - 1].IsPlacement;

            if (!CoreTurnActions.TryUndo(serverState, playerIndex, turnHistory))
            {
                Debug.LogWarning("[NetworkGameManager] Undo rechazado (sin usos o sin acciones que deshacer).");
                NotifyPlacementFailedClientRpc(senderId, "No se puede deshacer.");
                return;
            }

            if (undoingPlacement) turnPlaced = false;

            BroadcastSnapshot(new NetworkActionSnapshot { Type = NetworkActionType.Undone, PlayerIndex = playerIndex });
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestMoveServerRpc(int fromRow, int fromCol, int toRow, int toCol, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!TryGetActingPlayer(rpcParams, "Move", out int playerIndex, out MatchStateDTO serverState)) return;

            // El patron sale del grupo del dado que se mueve, nunca del cliente
            PatternDefDTO pattern = null;
            BoardStateDTO board = serverState.PlayerBoards[playerIndex];
            if (!IsInsideBoard(board, fromRow, fromCol) || !IsInsideBoard(board, toRow, toCol))
            {
                Debug.LogWarning($"[NetworkGameManager] Move fuera de tablero ({fromRow},{fromCol})->({toRow},{toCol}) rechazado.");
                NotifyPlacementFailedClientRpc(senderId, "Casilla fuera del tablero.");
                return;
            }

            {
                CellStateDTO cell = board.Cells[board.GetIndex(fromRow, fromCol)];
                if (cell.IsOccupied && serverState.PlayerProfiles[playerIndex].ActiveGroups.TryGetValue(cell.Color, out GroupDataDTO movedGroup))
                {
                    pattern = serverState.VariantConfig?.GetPattern(movedGroup.TargetSize);
                }
            }

            int scoreBefore = serverState.PlayerProfiles[playerIndex].Score;
            MoveExecutionResult result = CoreTurnActions.TryMove(serverState, playerIndex, fromRow, fromCol, toRow, toCol, pattern, turnHistory);

            if (!result.IsValid)
            {
                Debug.LogWarning($"[NetworkGameManager] Move ({fromRow},{fromCol})->({toRow},{toCol}) rechazado por reglas autoritativas.");
                NotifyPlacementFailedClientRpc(senderId, "Movimiento no válido según las reglas.");
                return;
            }

            BroadcastSnapshot(new NetworkActionSnapshot
            {
                Type = NetworkActionType.Moved,
                PlayerIndex = playerIndex,
                Row = toRow,
                Col = toCol,
                ScoreDelta = serverState.PlayerProfiles[playerIndex].Score - scoreBefore,
                ClosedGroup = result.PatternCompleted,
                PatternCompleted = result.PatternCompleted
            });
        }

        private static bool IsInsideBoard(BoardStateDTO board, int row, int col)
        {
            return board != null && row >= 0 && row < board.Rows && col >= 0 && col < board.Cols;
        }

        /// <summary>
        /// Puerta de entrada de cualquier RPC de cliente: descarta el exceso de peticiones y expulsa a quien insiste.
        /// Tambien la usan otros sistemas de red (p. ej. los emojis).
        /// </summary>
        public bool AllowRpc(ulong clientId, string actionName) => PassesLimiter(actionLimiter, clientId, actionName);

        private bool PassesLimiter(CoreRateLimiter limiter, ulong clientId, string actionName)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            switch (limiter.Check(clientId, now))
            {
                case RateLimitResult.Allowed:
                    return true;

                case RateLimitResult.Kick:
                    Debug.LogWarning($"[AntiCheat] ClientId {clientId} supera el ritmo permitido de forma reiterada ({actionName}). Expulsando.");
                    limiter.Forget(clientId);
                    if (NetworkManager.Singleton != null) NetworkManager.Singleton.DisconnectClient(clientId, "Demasiadas peticiones");
                    return false;

                default:
                    // Un log por segundo y cliente como maximo: el propio spam no debe inundar la consola del servidor
                    if (!lastRateLogTime.TryGetValue(clientId, out double last) || now - last >= 1.0)
                    {
                        lastRateLogTime[clientId] = now;
                        Debug.LogWarning($"[AntiCheat] ClientId {clientId}: {actionName} descartado por exceso de ritmo.");
                    }
                    return false;
            }
        }

        private bool TryGetActingPlayer(ServerRpcParams rpcParams, string actionName, out int playerIndex, out MatchStateDTO serverState)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            playerIndex = GetPlayerIndex(senderId);
            serverState = GameManager.Instance != null ? GameManager.Instance.ServerState : null;

            if (!PassesLimiter(actionLimiter, senderId, actionName)) return false;

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
            turnHistory.Clear();
            turnPlaced = false;

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

            NetworkMatchSnapshot snapshot = NetworkMatchSnapshot.FromState(GameManager.Instance.ServerState, slots, action, turnPlaced, turnHistory.Count);
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
            LastSnapshot = snapshot;
            HasSnapshot = true;

            OnlineMatchPresenter.Apply(snapshot, LocalPlayerIndex, IsServer);
            OnSnapshotApplied?.Invoke(snapshot);
        }

        [ClientRpc]
        private void NotifyPlacementFailedClientRpc(ulong targetClientId, string reason)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                Debug.LogWarning($"[Cliente Local] Placement rechazado por el servidor: {reason}");
                OnPlacementRejected?.Invoke();
                if (PopUpManager.Instance != null)
                {
                    PopUpManager.Instance.ShowPopUp(Vector3.up * 2f, "Movimiento no válido", Color.red);
                }
            }
        }
    }
}
