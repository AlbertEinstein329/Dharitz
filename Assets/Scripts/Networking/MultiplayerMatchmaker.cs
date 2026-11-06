using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Multiplayer;

namespace MyGame.Networking
{
    public enum MatchMode
    {
        TwoPlayers = 2,
        FourPlayers = 4
    }

    /// <summary>
    /// Busqueda rapida de partida con Unity Multiplayer Services (Sessions + Relay).
    /// Une al jugador a una sesion publica con el mismo numero de plazas o, si no hay ninguna, crea una y espera.
    /// El SDK arranca solo el Host/Client de Netcode sobre NetworkManager.Singleton (por eso se usa en la escena Tablero)
    /// y gestiona el transporte por Relay: no hace falta abrir puertos ni conocer la IP del rival.
    /// </summary>
    public class MultiplayerMatchmaker : MonoBehaviour
    {
        public static MultiplayerMatchmaker Instance { get; private set; }

        private const int QuickJoinTimeoutSeconds = 8;
        private const string ModePropertyKey = "dharitzMode";

        private static string ModeProperty(int players) => $"{players}p";

        public event Action<string> OnStatusUpdated;
        public event Action<ISession> OnMatchFound;
        public event Action<string> OnMatchFailed;

        public bool IsSearching { get; private set; }
        private string statusKey;
        private object[] statusArgs;

        /// <summary>Ultimo estado, siempre en el idioma actual.</summary>
        public string LastStatus => statusKey == null ? string.Empty : Loc.T(statusKey, statusArgs);
        public ISession ActiveSession { get; private set; }

        // Id de la ultima sesion: permite volver a ella tras una caida de conexion
        public string LastSessionId { get; private set; }

        private CancellationTokenSource searchCts;

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

        /// <summary>
        /// Busca una sesion de <paramref name="mode"/> jugadores y la une; si no existe, la crea como anfitrion.
        /// </summary>
        public async Task StartMatchmakingAsync(MatchMode mode)
        {
            if (IsSearching)
            {
                Debug.LogWarning("[Matchmaker] Busqueda ya activa.");
                return;
            }

            IsSearching = true;
            searchCts = new CancellationTokenSource();
            int players = (int)mode;
            SetStatus("mm_searching", players);

            try
            {
                if (NetworkBootstrap.Instance == null)
                    throw new InvalidOperationException("Falta NetworkBootstrap en la escena.");

                if (!NetworkBootstrap.Instance.IsAuthenticated)
                    await NetworkBootstrap.Instance.InitializeServicesAsync();

                if (!NetworkBootstrap.Instance.IsAuthenticated)
                    throw new InvalidOperationException("No se pudo iniciar sesion en Unity Services.");

                var quickJoin = new QuickJoinOptions
                {
                    Timeout = TimeSpan.FromSeconds(QuickJoinTimeoutSeconds),
                    CreateSession = true
                };
                // MaxPlayers no es filtrable en quick join: se indexa el modo como propiedad de la sesion
                quickJoin.Filters.Add(new FilterOption(FilterField.StringIndex1, ModeProperty(players), FilterOperation.Equal));

                var sessionOptions = new SessionOptions
                {
                    MaxPlayers = players,
                    IsPrivate = false,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { ModePropertyKey, new SessionProperty(ModeProperty(players), VisibilityPropertyOptions.Public, PropertyIndex.String1) }
                    }
                }.WithRelayNetwork();

                ActiveSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions, searchCts.Token);

                IsSearching = false;
                if (ActiveSession.IsHost) SetStatus("mm_room_created", players);
                else SetStatus("mm_match_found");
                LastSessionId = ActiveSession.Id;
                Debug.Log($"[Matchmaker] Sesion {ActiveSession.Id} (codigo {ActiveSession.Code}) host={ActiveSession.IsHost}");
                OnMatchFound?.Invoke(ActiveSession);
            }
            catch (OperationCanceledException)
            {
                IsSearching = false;
                SetStatus("mm_cancelled");
            }
            catch (Exception ex)
            {
                IsSearching = false;
                Debug.LogError($"[Matchmaker] Error en matchmaking: {ex.Message}");
                SetStatus("mm_not_found");
                OnMatchFailed?.Invoke(ex.Message);
            }
            finally
            {
                searchCts?.Dispose();
                searchCts = null;
            }
        }

        /// <summary>
        /// Vuelve a la ultima sesion tras una desconexion. El servidor reconoce al jugador por su PlayerId
        /// de UGS y le devuelve su slot y un snapshot completo (ver NetworkGameManager.RegisterPlayerServerRpc).
        /// </summary>
        public async Task<bool> ReconnectAsync()
        {
            if (string.IsNullOrEmpty(LastSessionId) || IsSearching) return false;

            IsSearching = true;
            SetStatus("mm_reconnecting");
            try
            {
                ActiveSession = await MultiplayerService.Instance.ReconnectToSessionAsync(LastSessionId);
                IsSearching = false;
                SetStatus("mm_reconnected");
                return true;
            }
            catch (Exception ex)
            {
                IsSearching = false;
                Debug.LogWarning($"[Matchmaker] No se pudo reconectar: {ex.Message}");
                SetStatus("mm_reconnect_failed");
                return false;
            }
        }

        public void CancelMatchmaking()
        {
            if (!IsSearching) return;
            searchCts?.Cancel();
            Debug.Log("[Matchmaker] Busqueda cancelada por el jugador.");
        }

        /// <summary>
        /// Sale de la sesion activa: el anfitrion la borra y los demas la abandonan.
        /// </summary>
        public async Task LeaveSessionAsync()
        {
            CancelMatchmaking();

            ISession session = ActiveSession;
            ActiveSession = null;
            LastSessionId = null;
            if (session == null) return;

            try
            {
                if (session.IsHost) await session.AsHost().DeleteAsync();
                else await session.LeaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Matchmaker] No se pudo cerrar la sesion limpiamente: {ex.Message}");
            }
        }

        private void SetStatus(string key, params object[] args)
        {
            statusKey = key;
            statusArgs = args;
            OnStatusUpdated?.Invoke(LastStatus);
        }
    }
}
