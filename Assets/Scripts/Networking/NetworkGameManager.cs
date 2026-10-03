using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using MyGame.Core;

namespace MyGame.Networking
{
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        private readonly Dictionary<ulong, int> clientIdToPlayerIndex = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> playerIndexToClientId = new Dictionary<int, ulong>();

        public NetworkVariable<int> NetworkCurrentPlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetworkMatchPhase = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<int, int, int, DieColor, int, int> OnServerDiePlaced; // playerIndex, row, col, color, val, score

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
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            int slot = clientIdToPlayerIndex.Count;
            clientIdToPlayerIndex[clientId] = slot;
            playerIndexToClientId[slot] = clientId;
            Debug.Log($"[NetworkGameManager] Cliente conectado. ClientId: {clientId} -> Slot de Jugador: {slot}");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientIdToPlayerIndex.TryGetValue(clientId, out int slot))
            {
                Debug.LogWarning($"[NetworkGameManager] Cliente desconectado. ClientId: {clientId} (Slot: {slot})");
                if (ReconnectionManager.Instance != null)
                {
                    ReconnectionManager.Instance.HandlePlayerDisconnect(slot);
                }
            }
        }

        public int GetPlayerIndex(ulong clientId)
        {
            return clientIdToPlayerIndex.TryGetValue(clientId, out int index) ? index : -1;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestDrawDieServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            int playerIndex = GetPlayerIndex(senderId);

            if (playerIndex != NetworkCurrentPlayerIndex.Value)
            {
                Debug.LogWarning($"[NetworkGameManager] Intento de DrawDie rechazado. No es el turno de ClientId {senderId}.");
                return;
            }

            var serverState = GameManager.Instance.ServerState;
            if (serverState == null || serverState.HasDrawn)
            {
                Debug.LogWarning("[NetworkGameManager] DrawDie rechazado: ya ha robado este turno.");
                return;
            }

            CoreDrawProcessor.ProcessDraw(serverState);
            NotifyDieDrawnClientRpc(playerIndex, serverState.CurrentDrawnColor.Value, serverState.CurrentDrawnValue.Value);
        }

        [ClientRpc]
        private void NotifyDieDrawnClientRpc(int playerIndex, DieColor color, int value)
        {
            Debug.Log($"[NetworkGameManager] Dado robado por Jugador {playerIndex}: Color={color}, Valor={value}");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ServerState.HasDrawn = true;
                GameManager.Instance.ServerState.CurrentDrawnColor = color;
                GameManager.Instance.ServerState.CurrentDrawnValue = value;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPlaceDieServerRpc(int row, int col, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            int playerIndex = GetPlayerIndex(senderId);

            if (playerIndex != NetworkCurrentPlayerIndex.Value)
            {
                Debug.LogWarning($"[NetworkGameManager] Placement rechazado. No es el turno de ClientId {senderId}.");
                return;
            }

            var serverState = GameManager.Instance.ServerState;
            if (serverState == null || !serverState.HasDrawn || !serverState.CurrentDrawnColor.HasValue)
            {
                Debug.LogWarning("[NetworkGameManager] Placement rechazado: no hay dado robado disponible.");
                return;
            }

            DieColor color = serverState.CurrentDrawnColor.Value;
            int value = serverState.CurrentDrawnValue.Value;

            bool success = CoreMatchProcessor.ProcessPlacementIntent(serverState, playerIndex, row, col, color, value);
            if (!success)
            {
                Debug.LogWarning($"[NetworkGameManager] Placement en ({row},{col}) rechazado por reglas autoritativas.");
                NotifyPlacementFailedClientRpc(senderId, "Movimiento no válido según las reglas.");
                return;
            }

            int updatedScore = serverState.PlayerProfiles.ContainsKey(playerIndex) ? serverState.PlayerProfiles[playerIndex].TotalScore : 0;
            NotifyDiePlacedClientRpc(playerIndex, row, col, color, value, updatedScore);

            // Avanzar turno en el servidor
            if (GameManager.Instance != null && GameManager.Instance.turnManager != null)
            {
                GameManager.Instance.turnManager.EndTurn();
                NetworkCurrentPlayerIndex.Value = GameManager.Instance.ServerState.CurrentPlayerIndex;
            }
        }

        [ClientRpc]
        private void NotifyDiePlacedClientRpc(int playerIndex, int row, int col, DieColor color, int value, int score)
        {
            Debug.Log($"[NetworkGameManager] Dado colocado autoritativamente. Jugador: {playerIndex}, Pos: ({row},{col}), Score: {score}");
            OnServerDiePlaced?.Invoke(playerIndex, row, col, color, value, score);
        }

        [ClientRpc]
        private void NotifyPlacementFailedClientRpc(ulong targetClientId, string reason)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                Debug.LogWarning($"[Cliente Local] Placement rechazado por el servidor: {reason}");
            }
        }
    }
}
