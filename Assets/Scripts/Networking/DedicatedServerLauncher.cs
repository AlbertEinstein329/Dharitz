using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.Networking
{
    /// <summary>
    /// Arranque headless del servidor dedicado. Si el ejecutable es un build de servidor (UNITY_SERVER) o se lanza con
    /// <c>-server</c>, salta el login y el menu, configura una partida online y carga directamente la escena Tablero,
    /// donde <see cref="OnlineAutoConnect"/> arranca Netcode como servidor.
    ///
    /// Argumentos (los mismos que pasa Multiplay): -port, -queryport, -maxPlayers (2 o 4; 1 solo con -allowSolo, para pruebas).
    ///   Dharitz.x86_64 -batchmode -nographics -port 7777 -queryport 7778 -maxPlayers 2
    /// </summary>
    public static class DedicatedServerLauncher
    {
        public const string SessionConfigResource = "DatosDeSesionActual";
        public const int BoardSceneIndex = 2;

        public static ushort Port { get; private set; } = 7777;
        public static ushort QueryPort { get; private set; } = 7778;
        public static int MaxPlayers { get; private set; } = 2;

        public static bool IsDedicatedServer
        {
            get
            {
#if UNITY_SERVER
                return true;
#elif UNITY_EDITOR
                // En el Editor se activa desde Tools > Dharitz > Server > Simular servidor dedicado
                return UnityEditor.EditorPrefs.GetBool("Dharitz.DedicatedServerSimulation", false);
#else
                return HasArg("-server");
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!IsDedicatedServer) return;

            ParseArguments();

            // Un servidor no necesita pintar ni sincronizar con la pantalla: se limita la CPU
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;

            var session = Resources.Load<SessionConfig>(SessionConfigResource);
            if (session == null)
            {
                Debug.LogError($"[DedicatedServer] No se encontro Resources/{SessionConfigResource}. No se puede configurar la partida.");
                Application.Quit(1);
                return;
            }

            // Cambios solo en memoria del proceso: no tocan el asset del proyecto
            session.isCampaignMode = false;
            session.isOnlineMatch = true;
            session.playerCount = MaxPlayers;

            OnlineLaunchRequest.SetDedicatedServer(Port);

            Debug.Log($"[DedicatedServer] Arrancando. Puerto {Port}, query {QueryPort}, jugadores {MaxPlayers}.");
            SceneManager.LoadScene(BoardSceneIndex);
        }

        private static void ParseArguments()
        {
            if (TryGetArg("-port", out string port) && ushort.TryParse(port, out ushort parsedPort) && parsedPort != 0) Port = parsedPort;
            if (TryGetArg("-queryport", out string query) && ushort.TryParse(query, out ushort parsedQuery) && parsedQuery != 0) QueryPort = parsedQuery;

            // Solo 2 o 4 jugadores son modos validos; 1 jugador solo con -allowSolo (pruebas con un unico cliente)
            if (TryGetArg("-maxPlayers", out string max) && int.TryParse(max, out int parsedMax)
                && (parsedMax == 2 || parsedMax == 4 || (parsedMax == 1 && HasArg("-allowSolo"))))
                MaxPlayers = parsedMax;
        }

        private static bool HasArg(string name)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool TryGetArg(string name, out string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
