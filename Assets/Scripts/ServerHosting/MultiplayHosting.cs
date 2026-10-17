using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Multiplay;

namespace MyGame.ServerHosting
{
    /// <summary>
    /// Pegamento con el SDK de Game Server Hosting (Multiplay). Vive en su propio ensamblado porque
    /// com.unity.services.multiplay solo existe en Editor y en Standalone: el codigo del juego (que tambien se compila
    /// para Android) lo usa unicamente tras un <c>#if UNITY_EDITOR || UNITY_STANDALONE</c> y sin depender de sus tipos.
    /// </summary>
    public static class MultiplayHosting
    {
        private static IServerQueryHandler queryHandler;

        /// <summary>True cuando el servidor se registro en Multiplay (hay server.json de la asignacion).</summary>
        public static bool IsRegistered { get; private set; }

        public static string AllocationId { get; private set; } = string.Empty;

        /// <summary>
        /// Registra el servidor en Multiplay: arranca el responder de consultas (SQP) y lo marca como "listo para jugadores".
        /// Fuera de Multiplay (pruebas locales) no hay server.json: devuelve false y el servidor sigue funcionando por IP directa.
        /// </summary>
        public static async Task<bool> TryRegisterAsync(ushort maxPlayers, string serverName, string gameType, string map)
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                ServerConfig config = MultiplayService.Instance.ServerConfig;
                if (config == null) return false;

                AllocationId = config.AllocationId;
                queryHandler = await MultiplayService.Instance.StartServerQueryHandlerAsync(maxPlayers, serverName, gameType, Application.version, map);
                await MultiplayService.Instance.ReadyServerForPlayersAsync();

                IsRegistered = true;
                Debug.Log($"[MultiplayHosting] Servidor listo. Asignacion {AllocationId}, puerto {config.Port}, query {config.QueryPort}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultiplayHosting] Sin registro en Multiplay (servidor local o sin asignacion): {ex.Message}");
                return false;
            }
        }

        /// <summary>Debe llamarse con frecuencia: responde a las consultas del orquestador y publica los jugadores conectados.</summary>
        public static void Tick(int currentPlayers)
        {
            if (queryHandler == null) return;

            queryHandler.CurrentPlayers = (ushort)Mathf.Clamp(currentPlayers, 0, ushort.MaxValue);
            queryHandler.UpdateServerCheck();
        }

        /// <summary>Avisa a Multiplay de que el servidor ya no admite jugadores y libera el responder SQP.</summary>
        public static async Task ShutdownAsync()
        {
            try
            {
                if (IsRegistered) await MultiplayService.Instance.UnreadyServerAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultiplayHosting] No se pudo marcar el servidor como no listo: {ex.Message}");
            }

            queryHandler?.Dispose();
            queryHandler = null;
            IsRegistered = false;
        }
    }
}
