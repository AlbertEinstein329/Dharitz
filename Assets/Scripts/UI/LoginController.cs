using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de Login: fondo del juego, titulo y entrada con Google Play Games (unica via de acceso en el movil), con
/// estado de carga, mensaje de error y reintento. Si ya hay una sesion guardada entra sola tras un instante.
/// En el Editor y en builds de desarrollo aparece ademas un acceso de desarrollo sin cuenta para poder probar.
/// La UI se crea por codigo con el kit (botones de madera) sobre el canvas unico de la escena.
/// </summary>
public class LoginController : MonoBehaviour
{
    [SerializeField] private Sprite background;

    [Tooltip("Indice de la escena del menu principal en Build Settings.")]
    [SerializeField] private int menuSceneIndex = 1;

    private Button googleButton;
    private Button developmentButton;
    private Button retryButton;
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
            ShowFailure("login_error_generic");
            yield break;
        }

        SetBusy(true, "login_connecting");
        Task<bool> init = auth.InitializeAsync();
        while (!init.IsCompleted) yield return null;

        if (!init.Result)
        {
            ShowFailure(Application.internetReachability == NetworkReachability.NotReachable ? "login_error_offline" : "login_error_generic");
            yield break;
        }

        // Sesion guardada de una visita anterior: entra sola (con un respiro para ver el titulo)
        if (auth.HasCachedSession)
        {
            SetBusy(true, "login_entering");
            yield return new WaitForSeconds(0.6f);
            yield return RunSignIn(auth.ResumeCachedSessionAsync());
            yield break;
        }

        SetBusy(false, null);
    }

    // ------------------------------------------------------------------ Acciones

    private void OnGoogle()
    {
        if (busy || AuthManager.Instance == null) return;
        StartCoroutine(SignIn(AuthManager.Instance.SignInWithGooglePlayAsync(), "login_google_working"));
    }

    private void OnDevelopment()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (busy || AuthManager.Instance == null) return;
        StartCoroutine(SignIn(AuthManager.Instance.SignInForDevelopmentAsync(), "login_entering"));
#endif
    }

    private void OnRetry()
    {
        if (busy) return;
        SetBusy(false, null);
    }

    private IEnumerator SignIn(Task<AuthManager.AuthResult> task, string workingKey)
    {
        SetBusy(true, workingKey);
        yield return RunSignIn(task);
    }

    private IEnumerator RunSignIn(Task<AuthManager.AuthResult> task)
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
        statusLabel.gameObject.SetActive(value);
        if (value) ShowStatus(statusKey);
    }

    private void ShowFailure(string statusKey)
    {
        busy = false;
        optionsGroup.SetActive(false);
        statusLabel.gameObject.SetActive(true);
        retryButton.gameObject.SetActive(true);
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

#if UNITY_ANDROID
        googleButton = Make(kit, OnlineUiKit.ButtonStyle.Primary, "login_google", new Vector2(0.5f, 0.30f), new Vector2(820f, 150f), OnGoogle);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Acceso de desarrollo: sin cuenta, solo para probar en el Editor y en builds de desarrollo
        developmentButton = Make(kit, OnlineUiKit.ButtonStyle.Neutral, "login_dev", new Vector2(0.5f, googleButton != null ? 0.19f : 0.30f), new Vector2(820f, 150f), OnDevelopment);
#endif

        retryButton = kit.CreateButton(content.transform, OnlineUiKit.ButtonStyle.Primary, string.Empty, new Vector2(0.5f, 0.26f), new Vector2(520f, 130f), OnRetry);
        LocalizedText.Bind(retryButton.GetComponentInChildren<TMP_Text>(true), "login_retry");
        retryButton.gameObject.SetActive(false);

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
