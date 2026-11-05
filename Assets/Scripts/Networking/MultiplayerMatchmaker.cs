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
        private const int PrivateJoinTimeoutSeconds = 15;
        private bool cancelledByUser;
        private const string ModePropertyKey = "dharitzMode";

        private static string ModeProperty(int players) => $"{players}p";

        public event Action<string> OnStatusUpdated;
        public event Action<ISession> OnMatchFound;
        public event Action<string> OnMatchFailed;

        /// <summary>Se dispara cuando una sala privada esta creada y ya hay codigo que compartir.</summary>
        public event Action<string> OnRoomCodeReady;

        /// <summary>Codigo de la sala privada para compartir (por ejemplo K7M2QX-2); vacio si no hay.</summary>
        public string RoomCode { get; private set; } = string.Empty;

        /// <summary>Clave de texto del ultimo fallo al crear o unirse a una sala privada.</summary>
        public string LastFailureKey { get; private set; } = string.Empty;

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


        // =========================================================
        // SALAS PRIVADAS POR CODIGO (Relay): sin IP, sin puertos, funcionan entre redes distintas
        // =========================================================

        /// <summary>Crea una sala privada de <paramref name="mode"/> jugadores y deja su codigo en <see cref="RoomCode"/>.</summary>
        public async Task CreatePrivateRoomAsync(MatchMode mode)
        {
            if (IsSearching)
            {
                Debug.LogWarning("[Matchmaker] Ya hay una operacion en curso.");
                return;
            }

            IsSearching = true;
            LastFailureKey = string.Empty;
            RoomCode = string.Empty;
            int players = (int)mode;
            SetStatus("mm_creating_room_code");

            try
            {
                await EnsureAuthenticatedAsync();

                var options = new SessionOptions { MaxPlayers = players, IsPrivate = true }.WithRelayNetwork();
                ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);

                LastSessionId = ActiveSession.Id;
                RoomCode = MyGame.Core.RoomCodes.Format(ActiveSession.Code, players);
                IsSearching = false;
                SetStatus("mm_room_created", players);
                Debug.Log($"[Matchmaker] Sala privada {ActiveSession.Id} creada, codigo {RoomCode}");
                OnRoomCodeReady?.Invoke(RoomCode);
                OnMatchFound?.Invoke(ActiveSession);
            }
            catch (Exception ex)
            {
                IsSearching = false;
                Debug.LogError($"[Matchmaker] No se pudo crear la sala privada: {ex.Message}");
                Fail("mm_create_failed", ex.Message);
            }
        }

        /// <summary>Entra en la sala privada cuyo codigo de sesion es <paramref name="sessionCode"/> (sin el sufijo de jugadores).</summary>
        public async Task JoinPrivateRoomAsync(string sessionCode)
        {
            if (IsSearching)
            {
                Debug.LogWarning("[Matchmaker] Ya hay una operacion en curso.");
                return;
            }

            IsSearching = true;
            LastFailureKey = string.Empty;
            cancelledByUser = false;
            searchCts = new CancellationTokenSource();
            searchCts.CancelAfter(TimeSpan.FromSeconds(PrivateJoinTimeoutSeconds));
            CancellationToken token = searchCts.Token;
            SetStatus("mm_joining_code");

            try
            {
                await EnsureAuthenticatedAsync();

                ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode, new JoinSessionOptions(), token);

                LastSessionId = ActiveSession.Id;
                IsSearching = false;
                SetStatus("mm_match_found");
                Debug.Log($"[Matchmaker] Unido a la sala privada {ActiveSession.Id}");
                OnMatchFound?.Invoke(ActiveSession);
            }
            catch (OperationCanceledException)
            {
                IsSearching = false;
                // Cancelado por el jugador o por el tiempo de espera: el tiempo agotado se avisa como tal
                Fail(cancelledByUser ? "mm_cancelled" : "mm_join_timeout", "cancelado");
            }
            catch (Exception ex)
            {
                IsSearching = false;
                Debug.LogWarning($"[Matchmaker] No se pudo entrar en la sala: {ex.Message}");
                Fail(JoinFailureKey(ex), ex.Message);
            }
            finally
            {
                searchCts?.Dispose();
                searchCts = null;
            }
        }

        private static string JoinFailureKey(Exception ex)
        {
            // El SDK valida el alfabeto del codigo antes de salir a la red ("contains an invalid character")
            if (ex is ArgumentException || ex.Message.IndexOf("invalid character", StringComparison.OrdinalIgnoreCase) >= 0) return "mm_code_invalid";
            if (ex is SessionException sessionEx && sessionEx.Error == SessionError.SessionNotFound) return "mm_code_not_found";
            if (ex.Message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0) return "mm_room_full";
            return "mm_join_failed";
        }

        private void Fail(string key, string detail)
        {
            LastFailureKey = key;
            SetStatus(key);
            OnMatchFailed?.Invoke(detail);
        }

        private static async Task EnsureAuthenticatedAsync()
        {
            if (NetworkBootstrap.Instance == null)
                throw new InvalidOperationException("Falta NetworkBootstrap en la escena.");

            if (!NetworkBootstrap.Instance.IsAuthenticated)
                await NetworkBootstrap.Instance.InitializeServicesAsync();

            if (!NetworkBootstrap.Instance.IsAuthenticated)
                throw new InvalidOperationException("No se pudo iniciar sesion en Unity Services.");
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
            cancelledByUser = true;
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
            RoomCode = string.Empty;
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
