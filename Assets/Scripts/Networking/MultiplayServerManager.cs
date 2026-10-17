using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
#if UNITY_EDITOR || UNITY_STANDALONE
using MyGame.ServerHosting;
#endif

namespace MyGame.Networking
{
    /// <summary>
    /// Ciclo de vida del servidor dedicado: se registra en Multiplay (Game Server Hosting), publica los jugadores
    /// conectados y apaga el proceso cuando ya no sirve (partida terminada, sala abandonada o nadie llego).
    /// Solo actua cuando el ejecutable es un servidor (build de servidor o argumento -server).
    ///
    /// Fuera de Multiplay (pruebas locales) no hay asignacion: el servidor funciona igual por IP directa.
    /// </summary>
    public class MultiplayServerManager : MonoBehaviour
    {
        public static MultiplayServerManager Instance { get; private set; }

        [Header("Apagado automatico (segundos)")]
        [Tooltip("Nadie se registro en la sala tras arrancar: el servidor se libera.")]
        public float noPlayersTimeout = 300f;
        [Tooltip("La partida ya empezo y todos los jugadores se fueron.")]
        public float abandonedTimeout = 60f;
        [Tooltip("Tiempo que se mantiene el servidor tras GameOver para que los clientes reciban el resultado final.")]
        public float postMatchDelay = 20f;

        public bool IsRegisteredInMultiplay { get; private set; }

        private const float TickInterval = 0.5f;

        private float startTime;
        private float lastTick;
        private float emptySince = -1f;
        private float gameOverSince = -1f;
        private bool serverUp;
        private bool matchSeenWithPlayers;
        private bool shuttingDown;

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

        private void Start()
        {
            if (!DedicatedServerLauncher.IsDedicatedServer) enabled = false;
            startTime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (shuttingDown) return;

            NetworkManager net = NetworkManager.Singleton;
            if (net == null || !net.IsServer) return;

            if (!serverUp)
            {
                serverUp = true;
                startTime = Time.realtimeSinceStartup;
                _ = RegisterInMultiplayAsync();
            }

            if (Time.realtimeSinceStartup - lastTick < TickInterval) return;
            lastTick = Time.realtimeSinceStartup;

            int players = net.ConnectedClientsIds.Count;
            TickMultiplay(players);
            EvaluateLifecycle(players);
        }

        private async Task RegisterInMultiplayAsync()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            IsRegisteredInMultiplay = await MultiplayHosting.TryRegisterAsync(
                (ushort)DedicatedServerLauncher.MaxPlayers, "Dharitz", $"{DedicatedServerLauncher.MaxPlayers}p", "Tablero");
#else
            await Task.CompletedTask;
#endif
        }

        private void TickMultiplay(int players)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (IsRegisteredInMultiplay) MultiplayHosting.Tick(players);
#endif
        }

        private void EvaluateLifecycle(int players)
        {
            float now = Time.realtimeSinceStartup;
            NetworkGameManager game = NetworkGameManager.Instance;

            // 1) Partida terminada: se espera un margen para que los clientes reciban el ultimo snapshot y se apaga
            if (game != null && game.NetworkMatchPhase.Value == (int)Core.MatchPhase.GameOver)
            {
                if (gameOverSince < 0f) gameOverSince = now;
                if (now - gameOverSince >= postMatchDelay) Shutdown("partida terminada");
                return;
            }

            if (players > 0)
            {
                matchSeenWithPlayers = true;
                emptySince = -1f;
                return;
            }

            // 2) Sin jugadores: si nadie llego nunca, o todos se fueron a media partida, se libera el servidor
            if (emptySince < 0f) emptySince = now;

            float limit = matchSeenWithPlayers ? abandonedTimeout : noPlayersTimeout;
            float reference = matchSeenWithPlayers ? emptySince : startTime;
            if (now - reference >= limit)
                Shutdown(matchSeenWithPlayers ? "todos los jugadores abandonaron" : "nadie se conecto");
        }

        private async void Shutdown(string reason)
        {
            if (shuttingDown) return;
            shuttingDown = true;

            Debug.Log($"[MultiplayServerManager] Apagando servidor: {reason}.");

#if UNITY_EDITOR || UNITY_STANDALONE
            await MultiplayHosting.ShutdownAsync();
#else
            await Task.CompletedTask;
#endif

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

#if UNITY_EDITOR
            Debug.Log("[MultiplayServerManager] (Editor) Application.Quit omitido para no cerrar el Editor.");
#else
            Application.Quit();
#endif
        }
    }
}
