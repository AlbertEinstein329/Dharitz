using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    /// <summary>
    /// Botones Host/Cliente para probar el modo online en el Editor (Multiplayer Play Mode) o en builds de desarrollo,
    /// mientras el flujo de matchmaking no conecta la escena por sí solo.
    /// </summary>
    public class NetworkDebugLauncher : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            NetworkManager net = NetworkManager.Singleton;
            if (net == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 160));
            if (!net.IsClient && !net.IsServer)
            {
                if (GUILayout.Button("Start Host", GUILayout.Height(40))) net.StartHost();
                if (GUILayout.Button("Start Client", GUILayout.Height(40))) net.StartClient();
            }
            else
            {
                string role = net.IsHost ? "Host" : net.IsServer ? "Server" : "Client";
                int slot = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.LocalPlayerIndex : -1;
                GUILayout.Label($"{role} | ClientId {net.LocalClientId} | Jugador {slot + 1}");
                if (GUILayout.Button("Shutdown", GUILayout.Height(30))) net.Shutdown();
            }
            GUILayout.EndArea();
        }
#endif
    }
}
