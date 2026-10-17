using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de Login: fondo del juego, titulo y tres opciones (invitado, Google Play en Android, sin conexion), con
/// estado de carga, mensaje de error y reintento. Si ya hay una sesion guardada entra sola tras un instante.
/// La UI se crea por codigo con el kit (botones de madera) sobre el canvas unico de la escena.
/// </summary>
public class LoginController : MonoBehaviour
{
    [SerializeField] private Sprite background;

    [Tooltip("Indice de la escena del menu principal en Build Settings.")]
    [SerializeField] private int menuSceneIndex = 1;

    private Button guestButton;
    private Button googleButton;
    private Button offlineButton;
    private Button retryButton;
    private Button failureOfflineButton;
    private TMP_Text statusLabel;
    private GameObject optionsGroup;
    private bool busy;

    private IEnumerator Start()
    {
        OnlineUiKit kit = OnlineUiKit.Instance;
        if (kit == null) yield break;

        BuildUi(kit);
        LocalizedText.AttachToAll();

        // Un servidor dedicado no pasa por el login
        if (MyGame.Networking.DedicatedServerLauncher.IsDedicatedServer) yield break;

        AuthManager auth = AuthManager.Instance;
        if (auth == null)
        {
            ShowStatus("login_error_generic");
            yield break;
        }

        SetBusy(true, "login_connecting");
        var init = auth.InitializeAsync();
        while (!init.IsCompleted) yield return null;

        if (!init.Result)
        {
            ShowFailure(Application.internetReachability == NetworkReachability.NotReachable ? "login_error_offline" : "login_error_generic");
            yield break;
        }

        // Sesion guardada: entra sola (con un respiro para ver el titulo)
        if (auth.HasCachedSession)
        {
            SetBusy(true, "login_entering");
            yield return new WaitForSeconds(0.6f);
            yield return RunSignIn(auth.SignInAsGuestAsync());
            yield break;
        }

        SetBusy(false, null);
    }

    // ------------------------------------------------------------------ Acciones

    private void OnGuest()
    {
        if (busy || AuthManager.Instance == null) return;
        StartCoroutine(SignIn(AuthManager.Instance.SignInAsGuestAsync, "login_entering"));
    }

    private void OnGoogle()
    {
        if (busy || AuthManager.Instance == null) return;
        StartCoroutine(SignIn(AuthManager.Instance.SignInWithGooglePlayAsync, "login_google_working"));
    }

    private void OnOffline()
    {
        if (busy) return;
        if (AuthManager.Instance != null) AuthManager.Instance.ContinueOffline();
        SceneManager.LoadScene(menuSceneIndex);
    }

    private void OnRetry()
    {
        if (busy) return;
        SetBusy(false, null);
    }

    private IEnumerator SignIn(System.Func<System.Threading.Tasks.Task<AuthManager.AuthResult>> signIn, string workingKey)
    {
        SetBusy(true, workingKey);
        yield return RunSignIn(signIn());
    }

    private IEnumerator RunSignIn(System.Threading.Tasks.Task<AuthManager.AuthResult> task)
    {
        while (!task.IsCompleted) yield return null;

        switch (task.Result)
        {
            case AuthManager.AuthResult.Success:
                SceneManager.LoadScene(menuSceneIndex);
                break;
            case AuthManager.AuthResult.NoConnection:
                ShowFailure("login_error_offline");
                break;
            case AuthManager.AuthResult.Cancelled:
                SetBusy(false, null); // el jugador cerro el cuadro de Play Games: vuelve a las opciones sin error
                break;
            default:
                ShowFailure("login_error_generic");
                break;
        }
    }

    // ------------------------------------------------------------------ Estado

    private void SetBusy(bool value, string statusKey)
    {
        busy = value;
        optionsGroup.SetActive(!value);
        retryButton.gameObject.SetActive(false);
        failureOfflineButton.gameObject.SetActive(false);
        statusLabel.gameObject.SetActive(value);
        if (value) ShowStatus(statusKey);
    }

    private void ShowFailure(string statusKey)
    {
        busy = false;
        optionsGroup.SetActive(false);
        statusLabel.gameObject.SetActive(true);
        retryButton.gameObject.SetActive(true);
        failureOfflineButton.gameObject.SetActive(true);
        ShowStatus(statusKey);
    }

    private void ShowStatus(string key)
    {
        if (key == null) return;
        LocalizedText.Bind(statusLabel, key);
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi(OnlineUiKit kit)
    {
        Transform root = UiRoot.GetScreensParent("LoginCanvas");

        if (background != null) BackgroundCover.Create(root, background, Color.white);

        // Todo el contenido dentro del area segura
        var content = new GameObject("LoginContent", typeof(RectTransform), typeof(SafeAreaFitter));
        content.transform.SetParent(root, false);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

        TMP_Text title = kit.CreateLabel(content.transform, "DHARITZ", new Vector2(0.5f, 0.72f), new Vector2(900f, 220f), 160f, TextAlignmentOptions.Center);
        title.font = kit.buttonFont;
        title.color = new Color(1f, 0.93f, 0.7f);
        title.enableAutoSizing = false;
        title.outlineWidth = 0.25f;
        title.outlineColor = new Color32(70, 35, 10, 255);

        statusLabel = kit.CreateLabel(content.transform, string.Empty, new Vector2(0.5f, 0.34f), new Vector2(820f, 160f), 46f, TextAlignmentOptions.Center);

        optionsGroup = new GameObject("Options", typeof(RectTransform));
        optionsGroup.transform.SetParent(content.transform, false);
        var groupRect = (RectTransform)optionsGroup.transform;
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = groupRect.offsetMax = Vector2.zero;

        guestButton = Make(kit, OnlineUiKit.ButtonStyle.Primary, "login_guest", new Vector2(0.5f, 0.36f), new Vector2(760f, 140f), OnGuest);

#if UNITY_ANDROID
        googleButton = Make(kit, OnlineUiKit.ButtonStyle.Secondary, "login_google", new Vector2(0.5f, 0.26f), new Vector2(760f, 140f), OnGoogle);
#endif
        offlineButton = Make(kit, OnlineUiKit.ButtonStyle.Neutral, "login_offline", new Vector2(0.5f, googleButton != null ? 0.16f : 0.26f), new Vector2(760f, 140f), OnOffline);

        retryButton = kit.CreateButton(content.transform, OnlineUiKit.ButtonStyle.Primary, string.Empty, new Vector2(0.5f, 0.26f), new Vector2(520f, 130f), OnRetry);
        LocalizedText.Bind(retryButton.GetComponentInChildren<TMP_Text>(true), "login_retry");
        retryButton.gameObject.SetActive(false);

        // «Sin conexion» tambien disponible desde la pantalla de error
        failureOfflineButton = kit.CreateButton(content.transform, OnlineUiKit.ButtonStyle.Neutral, string.Empty, new Vector2(0.5f, 0.15f), new Vector2(520f, 130f), OnOffline);
        LocalizedText.Bind(failureOfflineButton.GetComponentInChildren<TMP_Text>(true), "login_offline");
        failureOfflineButton.gameObject.SetActive(false);

        TMP_Text version = kit.CreateLabel(content.transform, string.Empty, new Vector2(0.5f, 0.03f), new Vector2(500f, 60f), 30f, TextAlignmentOptions.Center);
        version.text = Loc.T("login_version", Application.version);
        version.color = new Color(1f, 1f, 1f, 0.75f);

        if (UiRoot.Instance != null) UiRoot.Instance.KeepLayersOnTop();
    }

    private Button Make(OnlineUiKit kit, OnlineUiKit.ButtonStyle style, string key, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Button button = kit.CreateButton(optionsGroup.transform, style, string.Empty, anchor, size, onClick);
        LocalizedText.Bind(button.GetComponentInChildren<TMP_Text>(true), key);
        return button;
    }
}
