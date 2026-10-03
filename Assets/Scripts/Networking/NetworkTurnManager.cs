using System;
using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    public class NetworkTurnManager : NetworkBehaviour
    {
        public static NetworkTurnManager Instance { get; private set; }

        public NetworkVariable<int> NetworkPlayerTurn = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> NetworkTimeRemaining = new NetworkVariable<float>(30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public const float DefaultTurnDuration = 30.0f;
        private bool isTimerRunning;

        public event Action<int> OnTurnChanged;
        public event Action<float> OnTimerTick;

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
            NetworkPlayerTurn.OnValueChanged += (oldVal, newVal) =>
            {
                Debug.Log($"[NetworkTurnManager] Cambio de turno detectado: Jugador {oldVal} -> Jugador {newVal}");
                OnTurnChanged?.Invoke(newVal);
            };

            NetworkTimeRemaining.OnValueChanged += (oldVal, newVal) =>
            {
                OnTimerTick?.Invoke(newVal);
            };

            if (IsServer)
            {
                StartTurnTimer();
            }
        }

        private void Update()
        {
            if (IsServer && isTimerRunning)
            {
                NetworkTimeRemaining.Value -= Time.deltaTime;
                if (NetworkTimeRemaining.Value <= 0f)
                {
                    NetworkTimeRemaining.Value = 0f;
                    isTimerRunning = false;
                    HandleTurnTimeout();
                }
            }
        }

        public void StartTurnTimer()
        {
            if (!IsServer) return;
            NetworkTimeRemaining.Value = DefaultTurnDuration;
            isTimerRunning = true;
        }

        private void HandleTurnTimeout()
        {
            if (!IsServer) return;

            int activePlayer = NetworkPlayerTurn.Value;
            Debug.LogWarning($"[NetworkTurnManager] Tiempo agotado para Jugador {activePlayer}. Forzando fin de turno.");

            // Si el jugador no colocó dado, el servidor ejecuta la penalización/quemado
            if (GameManager.Instance != null && GameManager.Instance.turnManager != null)
            {
                GameManager.Instance.turnManager.EndTurn();
                NetworkPlayerTurn.Value = GameManager.Instance.ServerState.CurrentPlayerIndex;
            }

            StartTurnTimer();
        }

        public void AdvanceTurn(int nextPlayerIndex)
        {
            if (!IsServer) return;
            NetworkPlayerTurn.Value = nextPlayerIndex;
            StartTurnTimer();
        }
    }
}
