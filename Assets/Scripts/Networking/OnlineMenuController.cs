using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace MyGame.Networking
{
    /// <summary>
    /// Panel online del menu principal, construido por codigo (no hay que cablear botones en la escena). Se abre desde el boton ONLINE del menu.
    /// Permite buscar partida rapida, crear una sala privada (2 o 4 jugadores) o unirse a una con su codigo.
    /// Despues carga Tablero, donde OnlineAutoConnect crea o une la sesion por Relay: no se usa ninguna IP.
    /// </summary>
    public class OnlineMenuController : MonoBehaviour
    {
        [Tooltip("SessionConfig compartido (el mismo que usa MainMenuManager)")]
        public SessionConfig sessionConfig;

        [Tooltip("Indice de la escena Tablero en Build Settings")]
        public int boardSceneIndex = 2;

        private GameObject panel;
        private TMP_Text statusLabel;
        private TMP_InputField codeField;

        private void Start()
        {
            BuildUi();

            // Si la sala anterior fallo (codigo inexistente, sala llena...), se vuelve aqui con el motivo a la vista
            if (!string.IsNullOrEmpty(OnlineLaunchRequest.MenuMessageKey) && statusLabel != null)
            {
                statusLabel.text = Loc.T(OnlineLaunchRequest.MenuMessageKey);
                OnlineLaunchRequest.MenuMessageKey = string.Empty;
                if (MainMenuManager.Instance != null) MainMenuManager.Instance.ShowOnlinePanel();
            }
        }

        private void CreateRoom(int players)
        {
            if (!Configure(players)) return;

            OnlineLaunchRequest.SetPrivateHost(players == 4 ? MatchMode.FourPlayers : MatchMode.TwoPlayers);
            statusLabel.text = Loc.T("menu_creating_room", players);
            SceneManager.LoadScene(boardSceneIndex);
        }

        private void QuickMatch(MatchMode mode)
        {
            if (!Configure((int)mode)) return;

            OnlineLaunchRequest.SetQuickMatch(mode);
            statusLabel.text = Loc.T("menu_searching", (int)mode);
            SceneManager.LoadScene(boardSceneIndex);
        }

        private void JoinRoom()
        {
            if (!MyGame.Core.RoomCodes.TryParse(codeField.text, out string sessionCode, out int players))
            {
                statusLabel.text = Loc.T("mm_code_invalid");
                return;
            }

            // El numero de jugadores va dentro del codigo: el tablero se construye antes de entrar en la sala
            if (!Configure(players)) return;

            OnlineLaunchRequest.SetPrivateJoin(sessionCode, players == 4 ? MatchMode.FourPlayers : MatchMode.TwoPlayers);
            statusLabel.text = Loc.T("menu_connecting", MyGame.Core.RoomCodes.Format(sessionCode, players));
            SceneManager.LoadScene(boardSceneIndex);
        }

        // Fondo del menu principal con su proporcion (cubre la pantalla sin deformarse)
        private static void AddMenuBackground(Transform parent)
        {
            Image source = MainMenuManager.Instance != null && MainMenuManager.Instance.mainPanel != null
                ? MainMenuManager.Instance.mainPanel.GetComponentInChildren<Image>(true)
                : null;

            if (source != null && source.sprite != null) BackgroundCover.Create(parent, source.sprite, source.color);
            else
            {
                var plain = new GameObject("Background", typeof(RectTransform), typeof(Image));
                plain.transform.SetParent(parent, false);
                var rect = (RectTransform)plain.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                plain.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 1f);
            }
        }

        private void Close()
        {
            if (MainMenuManager.Instance != null) MainMenuManager.Instance.ShowMainPanel();
            else panel.SetActive(false);
        }

        private bool Configure(int players)
        {
            if (sessionConfig == null)
            {
                statusLabel.text = Loc.T("menu_missing_config");
                return false;
            }

            sessionConfig.isCampaignMode = false;
            sessionConfig.isOnlineMatch = true;
            sessionConfig.playerCount = players;
            return true;
        }

        // =========================================================
        // UI
        // =========================================================

        private void BuildUi()
        {
            OnlineUiKit kit = OnlineUiKit.Instance;
            if (kit == null) return;

            // El panel es una pantalla mas del menu: cuelga del canvas unico y se muestra desde el boton ONLINE del menu principal
            Transform screens = UiRoot.GetScreensParent("OnlineMenuCanvas");

            // Pantalla completa propia (cada pantalla del menu lleva su fondo): fondo del menu principal + panel de madera
            var screen = new GameObject("OnlineScreen", typeof(RectTransform));
            screen.transform.SetParent(screens, false);
            var screenRect = (RectTransform)screen.transform;
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.offsetMin = screenRect.offsetMax = Vector2.zero;
            AddMenuBackground(screen.transform);
            panel = screen;

            RectTransform panelRect = kit.CreatePanel(screen.transform, new Vector2(900, 1100), "JUGAR ONLINE");
            Transform p = panelRect;

            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Primary, "PARTIDA RÁPIDA 2 JUGADORES", new Vector2(0.5f, 0.80f), new Vector2(700, 120), () => QuickMatch(MatchMode.TwoPlayers));
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Primary, "PARTIDA RÁPIDA 4 JUGADORES", new Vector2(0.5f, 0.68f), new Vector2(700, 120), () => QuickMatch(MatchMode.FourPlayers));

            kit.CreateLabel(p, "Sala privada con código", new Vector2(0.5f, 0.575f), new Vector2(700, 60), 34, TextAlignmentOptions.Center);
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Secondary, "CREAR SALA 2J", new Vector2(0.30f, 0.49f), new Vector2(340, 110), () => CreateRoom(2));
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Secondary, "CREAR SALA 4J", new Vector2(0.70f, 0.49f), new Vector2(340, 110), () => CreateRoom(4));

            codeField = kit.CreateInputField(p, string.Empty, "Código de sala (ej. K6M8FG-2)", new Vector2(0.36f, 0.37f), new Vector2(400, 100));
            codeField.characterLimit = 14;
            codeField.onValueChanged.AddListener(value =>
            {
                string upper = value.ToUpperInvariant();
                if (upper != value) codeField.text = upper;
            });
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Neutral, "UNIRSE CON CÓDIGO", new Vector2(0.74f, 0.37f), new Vector2(290, 110), JoinRoom);

            statusLabel = kit.CreateLabel(p, "Partida rápida: te une a una sala libre o crea una nueva y espera rivales.", new Vector2(0.5f, 0.23f), new Vector2(660, 150), 32, TextAlignmentOptions.Center);
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Danger, "VOLVER", new Vector2(0.5f, 0.10f), new Vector2(360, 110), Close);

            panel.SetActive(false);
            if (UiRoot.Instance != null) UiRoot.Instance.KeepLayersOnTop();
            if (MainMenuManager.Instance != null) MainMenuManager.Instance.RegisterOnlinePanel(panel);
        }
    }
}
