using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Sesion del jugador con Unity Gaming Services. Ya no decide por si sola a donde ir: la pantalla de Login
/// (<see cref="LoginController"/>) llama a <see cref="SignInWithGooglePlayAsync"/> (o a <see cref="ResumeCachedSessionAsync"/> si ya hay sesion) y
/// carga el menu cuando termina. Persiste entre escenas. El acceso al juego es con Google Play Games (sin invitados ni modo sin conexion).
/// </summary>
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    /// <summary>Resultado de un intento de inicio de sesion.</summary>
    public enum AuthResult { Success, NoConnection, Failed, Cancelled, NotSupported }

    public event Action OnAuthenticationComplete;

    private Task initTask;
    private bool eventsHooked;

    public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
    public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

    /// <summary>Hay una sesion guardada de una visita anterior (se puede entrar sin que el jugador pulse nada).</summary>
    public bool HasCachedSession => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.SessionTokenExists;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ------------------------------------------------------------------ Inicializacion

    /// <summary>Inicializa Unity Services (idempotente). Devuelve false si no hay conexion o falla.</summary>
    public async Task<bool> InitializeAsync()
    {
        // Un servidor dedicado no es un jugador: no inicia sesion ni descarga progreso de Cloud Save
        if (MyGame.Networking.DedicatedServerLauncher.IsDedicatedServer) return false;
        if (Application.internetReachability == NetworkReachability.NotReachable) return false;

        initTask ??= InitializeCoreAsync();
        try
        {
            await initTask;
            return UnityServices.State == ServicesInitializationState.Initialized;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] No se pudo inicializar Unity Services: {ex.Message}");
            initTask = null; // permite reintentar
            return false;
        }
    }

    private async Task InitializeCoreAsync()
    {
        await UnityServices.InitializeAsync();
        if (eventsHooked) return;
        eventsHooked = true;

        AuthenticationService.Instance.SignedIn += () => Debug.Log($"[Auth] Sesion iniciada. ID: {AuthenticationService.Instance.PlayerId}");
        AuthenticationService.Instance.SignInFailed += err => Debug.LogWarning($"[Auth] Fallo al iniciar sesion: {err.Message}");
        AuthenticationService.Instance.SignedOut += () => Debug.Log("[Auth] Sesion cerrada.");
    }

    // ------------------------------------------------------------------ Inicio de sesion

    /// <summary>
    /// Reanuda la sesion guardada de una visita anterior (la de Google Play o la de desarrollo): con una sesion en cache,
    /// UGS devuelve al mismo jugador sin pedir nada.
    /// </summary>
    public async Task<AuthResult> ResumeCachedSessionAsync()
    {
        if (!await InitializeAsync()) return Application.internetReachability == NetworkReachability.NotReachable ? AuthResult.NoConnection : AuthResult.Failed;
        if (!AuthenticationService.Instance.SessionTokenExists && !AuthenticationService.Instance.IsSignedIn) return AuthResult.Failed;
        return await SignInAnonymousCoreAsync();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Entrada sin cuenta, solo para probar en el Editor y en builds de desarrollo (el juego publicado entra con Google Play).</summary>
    public async Task<AuthResult> SignInForDevelopmentAsync()
    {
        if (!await InitializeAsync()) return Application.internetReachability == NetworkReachability.NotReachable ? AuthResult.NoConnection : AuthResult.Failed;
        return await SignInAnonymousCoreAsync();
    }
#endif

    private async Task<AuthResult> SignInAnonymousCoreAsync()
    {
        try
        {
            // Con sesion en cache reutiliza al mismo jugador
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            await FinishSignInAsync();
            return AuthResult.Success;
        }
        catch (RequestFailedException ex)
        {
            Debug.LogWarning($"[Auth] Sesion: {ex.Message}");
            return ex.ErrorCode == CommonErrorCodes.TransportError || ex.ErrorCode == CommonErrorCodes.Timeout ? AuthResult.NoConnection : AuthResult.Failed;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] Sesion: {ex.Message}");
            return AuthResult.Failed;
        }
    }

    /// <summary>Entra con Google Play Games (solo Android). Si hay un invitado, vincula; si la cuenta ya existe, entra con ella.</summary>
    public async Task<AuthResult> SignInWithGooglePlayAsync()
    {
#if UNITY_ANDROID
        if (!await InitializeAsync()) return Application.internetReachability == NetworkReachability.NotReachable ? AuthResult.NoConnection : AuthResult.Failed;

        string authCode = await RequestGooglePlayAuthCodeAsync();
        if (string.IsNullOrEmpty(authCode)) return AuthResult.Cancelled;

        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
            }
            else
            {
                try
                {
                    await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(authCode);
                }
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // Esa cuenta de Play Games ya tiene progreso en otro jugador: se entra con ella
                    AuthenticationService.Instance.SignOut();
                    await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
                }
            }

            await FinishSignInAsync();
            return AuthResult.Success;
        }
        catch (RequestFailedException ex)
        {
            Debug.LogWarning($"[Auth] Google Play: {ex.Message}");
            return ex.ErrorCode == CommonErrorCodes.TransportError || ex.ErrorCode == CommonErrorCodes.Timeout ? AuthResult.NoConnection : AuthResult.Failed;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auth] Google Play: {ex.Message}");
            return AuthResult.Failed;
        }
#else
        // Google Play Games solo existe en Android (el SDK no compila en Windows, Linux ni en el servidor dedicado)
        await Task.CompletedTask;
        return AuthResult.NotSupported;
#endif
    }

#if UNITY_ANDROID
    private static Task<string> RequestGooglePlayAuthCodeAsync()
    {
        var completion = new TaskCompletionSource<string>();
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate((SignInStatus status) =>
        {
            if (status != SignInStatus.Success)
            {
                Debug.LogWarning($"[Auth] Play Games: {status}");
                completion.TrySetResult(null);
                return;
            }

            PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => completion.TrySetResult(code));
        });
        return completion.Task;
    }
#endif

    public void SignOut()
    {
        if (IsSignedIn) AuthenticationService.Instance.SignOut(true); // true: olvida tambien la sesion en cache
    }

    // Descarga el progreso de la nube; no bloquea el inicio de sesion si falla
    private async Task FinishSignInAsync()
    {
        if (CloudSaveManager.Instance != null)
        {
            try { await CloudSaveManager.Instance.LoadMetaProgress(); }
            catch (Exception ex) { Debug.LogWarning($"[Auth] No se pudo cargar el progreso: {ex.Message}"); }
        }
        OnAuthenticationComplete?.Invoke();
    }
}
