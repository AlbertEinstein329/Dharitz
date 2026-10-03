using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace MyGame.Networking
{
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        [Header("Configuración de Red")]
        public string defaultIpAddress = "127.0.0.1";
        public ushort defaultPort = 7777;

        public bool IsServicesInitialized { get; private set; }
        public bool IsAuthenticated => AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;

        public event Action OnInitialized;
        public event Action<string> OnAuthenticationFailed;

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
            await InitializeServicesAsync();

#if UNITY_SERVER
            // En build headless de Multiplay (Game Server Hosting), arrancamos servidor autoritativo automáticamente
            StartDedicatedServer();
#endif
        }

        public async Task InitializeServicesAsync()
        {
            if (IsServicesInitialized) return;

            try
            {
                var options = new InitializationOptions();
                // En builds locales simultáneas, asignar profile id único para testing multi-instancia
#if UNITY_EDITOR
                string profile = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
                options.SetProfile(profile);
#endif
                await UnityServices.InitializeAsync(options);

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[NetworkBootstrap] Autenticado en UGS con Player ID: {AuthenticationService.Instance.PlayerId}");
                }

                IsServicesInitialized = true;
                OnInitialized?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkBootstrap] Error al inicializar UGS: {ex.Message}");
                OnAuthenticationFailed?.Invoke(ex.Message);
            }
        }

        public void ConfigureTransport(string ip, ushort port)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
                Debug.Log($"[NetworkBootstrap] Transport configurado a {ip}:{port}");
            }
            else
            {
                Debug.LogError("[NetworkBootstrap] UnityTransport no encontrado en NetworkManager");
            }
        }

        public bool StartClient(string ip = null, ushort port = 0)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager.Singleton es nulo");
                return false;
            }

            string targetIp = string.IsNullOrEmpty(ip) ? defaultIpAddress : ip;
            ushort targetPort = port == 0 ? defaultPort : port;

            ConfigureTransport(targetIp, targetPort);
            return NetworkManager.Singleton.StartClient();
        }

        public bool StartDedicatedServer(ushort port = 0)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager.Singleton es nulo");
                return false;
            }

            ushort targetPort = port == 0 ? defaultPort : port;
            ConfigureTransport("0.0.0.0", targetPort);
            return NetworkManager.Singleton.StartServer();
        }

        public bool StartHost()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager.Singleton es nulo");
                return false;
            }

            ConfigureTransport(defaultIpAddress, defaultPort);
            return NetworkManager.Singleton.StartHost();
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[NetworkBootstrap] Red desconectada correctamente.");
            }
        }
    }
}
