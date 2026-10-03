using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    public class EmojiChatManager : NetworkBehaviour
    {
        public static EmojiChatManager Instance { get; private set; }

        public event Action<int, int> OnEmojiReceived; // playerIndex, emojiId
        public event Action<string> OnEmojiError;

        private readonly Dictionary<int, float> lastSentTimeByPlayer = new Dictionary<int, float>();
        private const float CooldownSeconds = 2.0f;

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

        public void SendEmoji(int emojiId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                Debug.LogWarning("[EmojiChat] No conectado a red.");
                return;
            }

            RequestSendEmojiServerRpc(emojiId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSendEmojiServerRpc(int emojiId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            int playerIndex = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.GetPlayerIndex(senderId) : 0;

            float now = Time.time;
            if (lastSentTimeByPlayer.TryGetValue(playerIndex, out float lastTime))
            {
                if (now - lastTime < CooldownSeconds)
                {
                    Debug.LogWarning($"[EmojiChat] Cooldown activo para Jugador {playerIndex}. Bloqueado.");
                    NotifyCooldownClientRpc(senderId);
                    return;
                }
            }

            lastSentTimeByPlayer[playerIndex] = now;
            BroadcastEmojiClientRpc(playerIndex, emojiId);
        }

        [ClientRpc]
        private void BroadcastEmojiClientRpc(int playerIndex, int emojiId)
        {
            Debug.Log($"[EmojiChat] Emoji {emojiId} recibido de Jugador {playerIndex}");
            OnEmojiReceived?.Invoke(playerIndex, emojiId);
        }

        [ClientRpc]
        private void NotifyCooldownClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                OnEmojiError?.Invoke("¡Espera 2 segundos antes de enviar otro emoji!");
            }
        }
    }
}
