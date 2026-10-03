using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    /// <summary>
    /// Gestiona el ciclo de vida del Servidor Dedicado en Unity Multiplay / Game Server Hosting.
    /// Ejecuta automáticamente solo en builds de servidor autoritativo (#if UNITY_SERVER o headless).
    /// </summary>
    public class MultiplayServerManager : MonoBehaviour
    {
        public static MultiplayServerManager Instance { get; private set; }

        public ushort ServerPort { get; private set; } = 7777;
        public ushort QueryPort { get; private set; } = 7778;
        public int MaxPlayers { get; private set; } = 4;
        public bool IsAllocated { get; private set; }

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

        private async void Start()
        {
#if UNITY_SERVER || DEDICATED_SERVER
            Debug.Log("[MultiplayServerManager] Servidor dedicado detectado. Parseando argumentos del contenedor...");
            ParseCommandLineArgs();
            await InitializeMultiplayServerAsync();
#endif
        }

        private void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-port" && i + 1 < args.Length)
                {
                    if (ushort.TryParse(args[i + 1], out ushort parsedPort)) ServerPort = parsedPort;
                }
                else if (args[i] == "-queryport" && i + 1 < args.Length)
                {
                    if (ushort.TryParse(args[i + 1], out ushort parsedQuery)) QueryPort = parsedQuery;
                }
                else if (args[i] == "-maxPlayers" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int parsedMax)) MaxPlayers = parsedMax;
                }
            }

            Debug.Log($"[MultiplayServerManager] Puerto Servidor: {ServerPort}, Puerto Query: {QueryPort}, MaxJugadores: {MaxPlayers}");
        }

        public async Task InitializeMultiplayServerAsync()
        {
            try
            {
                Debug.Log("[MultiplayServerManager] Arrancando Netcode Server autoritativo...");
                bool serverStarted = NetworkBootstrap.Instance.StartDedicatedServer(ServerPort);

                if (serverStarted)
                {
                    Debug.Log("[MultiplayServerManager] Servidor Netcode escuchando. Notificando ReadyForPlayers a Multiplay...");
                    IsAllocated = true;
                    // Notificar disponibilidad a Multiplay Game Server Hosting
                }
                else
                {
                    Debug.LogError("[MultiplayServerManager] Falló el arranque del servidor Netcode.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayServerManager] Error en la inicialización de Multiplay: {ex.Message}");
            }

            await Task.CompletedTask;
        }
    }
}
