using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }



    private void Awake()
    {
        // Configuramos el Singleton para que este objeto no se destruya al cambiar de escena
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

    async void Start()
    {
        // Al arrancar el juego, intentamos iniciar sesión automáticamente
        await InitializeAndSignInAsync();
    }

    private async Task InitializeAndSignInAsync()
    {
        try
        {
            // 1. Inicializa el ecosistema de Unity Services (Obligatorio antes de hacer cualquier otra cosa)
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services inicializados correctamente.");

            // 2. Nos suscribimos a los eventos para saber si funcionó
            SetupEvents();

            // 3. Si ya tiene sesión iniciada, no hacemos nada. Si no, lo logueamos como invitado.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Intentando iniciar sesión como invitado...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (AuthenticationException ex)
        {
            // Atrapamos errores específicos de autenticación (ej: cuenta baneada)
            Debug.LogError($"Error de Autenticación: {ex.Message}");
        }
        catch (RequestFailedException ex)
        {
            // Atrapamos errores de red (ej: sin conexión a internet)
            Debug.LogError($"Error de Red/Servidor: {ex.Message}");
        }
    }

    private void SetupEvents()
    {
        AuthenticationService.Instance.SignedIn += async () => {
            Debug.Log($"¡Sesión iniciada con éxito! ID: {AuthenticationService.Instance.PlayerId}");

            // NUEVO: Apenas inicia sesión (ya sea invitado o Google), descarga su progreso.
            if (CloudSaveManager.Instance != null)
            {
                await CloudSaveManager.Instance.CargarProgresoMeta();
            }
        };

        AuthenticationService.Instance.SignInFailed += (err) => {
            Debug.LogError($"Fallo al iniciar sesión: {err}");
        };

        AuthenticationService.Instance.SignedOut += () => {
            Debug.Log("Sesión cerrada.");
        };
    }

    /// <summary>
    /// Inicia la conexión con la app de Google Play Games del teléfono.
    /// </summary>
    public void VincularCuentaGooglePlay()
    {
        Debug.Log("Iniciando Google Play Games...");

        // 1. Activamos la plataforma de GPGS
        PlayGamesPlatform.Activate();

        // 2. Intentamos autenticar al jugador (saldrá el cartel verde de Play Games por arriba)
        PlayGamesPlatform.Instance.Authenticate((SignInStatus status) =>
        {
            if (status == SignInStatus.Success)
            {
                Debug.Log("Login en Play Games exitoso. Solicitando código de acceso al servidor...");

                // 3. Pedimos el Auth Code. El 'true' significa que forzamos la obtención del código.
                PlayGamesPlatform.Instance.RequestServerSideAccess(true, async (authCode) =>
                {
                    try
                    {
                        Debug.Log("Código recibido. Vinculando con Unity Gaming Services...");

                        // OJO: Usamos el método específico para Play Games
                        await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(authCode);

                        Debug.Log("¡VINCULACIÓN EXITOSA! Ahora estás usando Google Play Games.");
                    }
                    catch (AuthenticationException ex)
                    {
                        Debug.LogError($"Error al vincular en UGS: {ex.Message}");
                    }
                });
            }
            else
            {
                Debug.LogError($"Fallo al iniciar sesión en Play Games: {status}");
            }
        });
    }


}