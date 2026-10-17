using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

namespace MyGame.Networking
{
    /// <summary>
    /// Salidas de una partida online, compartidas por el menu de pausa y el panel de fin de partida:
    /// salir al menu (cierra la sesion de verdad) y jugar de nuevo (busqueda rapida: otra partida del mismo tipo;
    /// partida por IP: revancha con el mismo rival sin reconectar).
    /// </summary>
    public static class OnlineMatchFlow
    {
        private const int MenuSceneIndex = 1;

        public static bool IsOnline => GameManager.Instance != null && GameManager.Instance.IsOnlineMatch;

        /// <summary>Abandona la partida (sesion y red) y vuelve al menu principal.</summary>
        public static void LeaveToMenu()
        {
            Disconnect();
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuSceneIndex);
        }

        /// <summary>Jugar de nuevo desde el panel de fin de partida.</summary>
        public static void PlayAgain(Button source)
        {
            if (OnlineLaunchRequest.LastRole == OnlineRole.QuickMatch)
            {
                // Busqueda rapida: se deja la sala actual y se busca otra del mismo tipo (2 o 4 jugadores)
                MatchMode mode = OnlineLaunchRequest.LastMode;
                Disconnect();
                OnlineLaunchRequest.SetQuickMatch(mode);
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            // Partida por IP: revancha con el mismo rival; el servidor reinicia cuando todos la piden
            NetworkGameManager game = NetworkGameManager.Instance;
            if (game == null || !game.IsSpawned)
            {
                LeaveToMenu();
                return;
            }

            if (source != null)
            {
                var feedback = source.GetComponent<RematchButtonFeedback>();
                if (feedback != null) feedback.MarkPressed();
            }
            game.RequestRematchServerRpc();
        }

        private static void Disconnect()
        {
            if (MultiplayerMatchmaker.Instance != null) _ = MultiplayerMatchmaker.Instance.LeaveSessionAsync();

            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.Disconnect();
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
        }
    }
}
