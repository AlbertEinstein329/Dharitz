using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    /// <summary>
    /// Ciclo de vida del servidor dedicado, independiente del proveedor de hosting: apaga el proceso cuando ya no
    /// sirve (partida terminada, sala abandonada o nadie llego) para que el orquestador (Agones, Edgegap, GameLift,
    /// Rocket Science Multiplay, un script propio...) libere la maquina.
    /// Solo actua cuando el ejecutable es un servidor (build de servidor o argumento -server).
    ///
    /// Multiplay Hosting y su paquete com.unity.services.multiplay estan deprecados: ya no se usa. El registro con el
    /// orquestador (readiness, consulta de jugadores) es especifico de cada proveedor y se anade en su integracion.
    /// </summary>
    public class DedicatedServerLifecycle : MonoBehaviour
    {
        public static DedicatedServerLifecycle Instance { get; private set; }

        [Header("Apagado automatico (segundos)")]
        [Tooltip("Nadie se registro en la sala tras arrancar: el servidor se libera.")]
        public float noPlayersTimeout = 300f;
        [Tooltip("La partida ya empezo y todos los jugadores se fueron.")]
        public float abandonedTimeout = 60f;
        [Tooltip("Tiempo que se mantiene el servidor tras GameOver para que los clientes reciban el resultado final.")]
        public float postMatchDelay = 20f;

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
                Debug.Log("[DedicatedServerLifecycle] Servidor en marcha. Esperando jugadores.");
            }

            if (Time.realtimeSinceStartup - lastTick < TickInterval) return;
            lastTick = Time.realtimeSinceStartup;

            EvaluateLifecycle(net.ConnectedClientsIds.Count);
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

        private void Shutdown(string reason)
        {
            if (shuttingDown) return;
            shuttingDown = true;

            Debug.Log($"[DedicatedServerLifecycle] Apagando servidor: {reason}.");

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

#if UNITY_EDITOR
            Debug.Log("[DedicatedServerLifecycle] (Editor) Application.Quit omitido para no cerrar el Editor.");
#else
            Application.Quit();
#endif
        }
    }
}
