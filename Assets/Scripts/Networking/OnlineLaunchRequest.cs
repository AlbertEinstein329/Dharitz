using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace MyGame.Networking
{
    public enum OnlineRole
    {
        None,
        Host,
        Client
    }

    /// <summary>
    /// Puente entre el menu y la escena Tablero: el menu deja aqui que quiere hacer el jugador
    /// (crear sala o unirse) y OnlineAutoConnect lo ejecuta cuando el NetworkManager de Tablero ya existe.
    /// </summary>
    public static class OnlineLaunchRequest
    {
        public static OnlineRole Role { get; private set; } = OnlineRole.None;
        public static string Ip { get; private set; } = "127.0.0.1";
        public static ushort Port { get; private set; } = 7777;

        public static void Set(OnlineRole role, string ip, ushort port)
        {
            Role = role;
            Ip = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
            Port = port == 0 ? (ushort)7777 : port;
        }

        public static void Clear() => Role = OnlineRole.None;
    }

    /// <summary>
    /// Vive en Tablero. Si el menu dejo una peticion pendiente, arranca Host o Client contra el NetworkManager de la escena.
    /// </summary>
    public class OnlineAutoConnect : MonoBehaviour
    {
        private IEnumerator Start()
        {
            if (OnlineLaunchRequest.Role == OnlineRole.None) yield break;
            if (GameManager.Instance == null || !GameManager.Instance.IsOnlineMatch) yield break;

            // Un frame para que NetworkManager.Singleton y los objetos de red de la escena esten listos
            yield return null;

            OnlineRole role = OnlineLaunchRequest.Role;
            string ip = OnlineLaunchRequest.Ip;
            ushort port = OnlineLaunchRequest.Port;
            OnlineLaunchRequest.Clear();

            NetworkManager net = NetworkManager.Singleton;
            if (net == null)
            {
                Debug.LogError("[OnlineAutoConnect] No hay NetworkManager en la escena.");
                yield break;
            }

            if (net.IsListening) yield break;

            NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
            if (bootstrap == null)
            {
                Debug.LogError("[OnlineAutoConnect] Falta NetworkBootstrap en la escena.");
                yield break;
            }

            bootstrap.defaultIpAddress = ip;
            bootstrap.defaultPort = port;

            bool ok = role == OnlineRole.Host ? bootstrap.StartHost() : bootstrap.StartClient(ip, port);
            Debug.Log($"[OnlineAutoConnect] {role} en {ip}:{port} -> {(ok ? "iniciado" : "FALLO")}");
        }
    }
}
