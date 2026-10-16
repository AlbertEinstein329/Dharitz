using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    public class ReconnectionManager : MonoBehaviour
    {
        public static ReconnectionManager Instance { get; private set; }

        private const float ReconnectTimeoutSeconds = 30.0f;
        private readonly Dictionary<int, Coroutine> disconnectCoroutines = new Dictionary<int, Coroutine>();

        public event Action<int> OnPlayerDisconnectedGracePeriod;
        public event Action<int> OnPlayerReconnected;
        public event Action<int> OnPlayerEliminatedByTimeout;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void HandlePlayerDisconnect(int playerIndex)
        {
            if (disconnectCoroutines.ContainsKey(playerIndex)) return;

            Debug.LogWarning($"[ReconnectionManager] Jugador {playerIndex} desconectado. Iniciando temporizador de reconexión ({ReconnectTimeoutSeconds}s)...");
            OnPlayerDisconnectedGracePeriod?.Invoke(playerIndex);

            Coroutine routine = StartCoroutine(WaitReconnectTimeoutRoutine(playerIndex));
            disconnectCoroutines[playerIndex] = routine;
        }

        public void HandlePlayerReconnect(int playerIndex)
        {
            if (disconnectCoroutines.TryGetValue(playerIndex, out Coroutine routine))
            {
                StopCoroutine(routine);
                disconnectCoroutines.Remove(playerIndex);
                Debug.Log($"[ReconnectionManager] Jugador {playerIndex} reconectado con éxito.");
                OnPlayerReconnected?.Invoke(playerIndex);
            }
        }

        private IEnumerator WaitReconnectTimeoutRoutine(int playerIndex)
        {
            yield return new WaitForSeconds(ReconnectTimeoutSeconds);

            disconnectCoroutines.Remove(playerIndex);
            Debug.LogError($"[ReconnectionManager] Tiempo de reconexión agotado para Jugador {playerIndex}. Eliminando por abandono.");

            // F5.2: Marcar como eliminado en el servidor autoritativo
            if (GameManager.Instance != null && GameManager.Instance.ServerState != null)
            {
                if (GameManager.Instance.ServerState.PlayerProfiles.TryGetValue(playerIndex, out var profile))
                {
                    profile.IsEliminated = true;
                }
            }

            OnPlayerEliminatedByTimeout?.Invoke(playerIndex);

            // Si era su turno, se cierra (el dado en mano vuelve a la bolsa); si no, solo se difunde la eliminación
            if (NetworkGameManager.Instance != null && GameManager.Instance != null && GameManager.Instance.ServerState != null)
            {
                if (GameManager.Instance.ServerState.CurrentPlayerIndex == playerIndex)
                    NetworkGameManager.Instance.ServerForceEndTurn();
                else
                    NetworkGameManager.Instance.ServerPushState();
            }
        }
    }
}
