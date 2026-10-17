using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Net;
using System.Net.Sockets;

namespace MyGame.Networking
{
    /// <summary>
    /// Panel online del menu principal, construido por codigo (no hay que cablear botones en la escena). Se abre desde el boton ONLINE del menu.
    /// Permite crear una sala (2 o 4 jugadores) o unirse a una por IP. Despues carga Tablero, donde
    /// OnlineAutoConnect arranca Host/Client. El matchmaking por cola queda pendiente del paquete
    /// com.unity.services.multiplayer (ver Plan de trabajo Online.md, fase 3).
    /// </summary>
    public class OnlineMenuController : MonoBehaviour
    {
        [Tooltip("SessionConfig compartido (el mismo que usa MainMenuManager)")]
        public SessionConfig sessionConfig;

        [Tooltip("Indice de la escena Tablero en Build Settings")]
        public int boardSceneIndex = 2;

        private GameObject panel;
        private TMP_Text statusLabel;
        private TMP_InputField ipField;

        private void Start()
        {
            BuildUi();
        }

        private void CreateRoom(int players)
        {
            string ip = GetLocalIp();
            if (!Configure(players)) return;

            OnlineLaunchRequest.Set(OnlineRole.Host, "0.0.0.0", 7777);
            statusLabel.text = Loc.T("menu_creating_room", players, ip);
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
            // El numero de jugadores lo decide el host; el cliente solo necesita saber que es una partida online
            if (!Configure(2)) return;

            OnlineLaunchRequest.Set(OnlineRole.Client, ipField.text, 7777);
            statusLabel.text = Loc.T("menu_connecting", OnlineLaunchRequest.Ip);
            SceneManager.LoadScene(boardSceneIndex);
        }

        // Copia el fondo del menu principal para que esta pantalla no quede sobre el color de la camara
        private static void AddMenuBackground(Transform parent)
        {
            Image source = MainMenuManager.Instance != null && MainMenuManager.Instance.mainPanel != null
                ? MainMenuManager.Instance.mainPanel.GetComponentInChildren<Image>(true)
                : null;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            var rect = (RectTransform)bg.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = bg.GetComponent<Image>();
            if (source != null)
            {
                image.sprite = source.sprite;
                image.type = source.type;
                image.color = source.color;
                image.material = source.material;
            }
            else image.color = new Color(0.08f, 0.1f, 0.14f, 1f);
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

        private static string GetLocalIp()
        {
            try
            {
                foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork) return address.ToString();
                }
            }
            catch { /* sin red: se muestra localhost */ }
            return "127.0.0.1";
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

            kit.CreateLabel(p, "Red local (sin matchmaking)", new Vector2(0.5f, 0.575f), new Vector2(700, 60), 34, TextAlignmentOptions.Center);
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Secondary, "CREAR SALA 2J", new Vector2(0.30f, 0.49f), new Vector2(340, 110), () => CreateRoom(2));
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Secondary, "CREAR SALA 4J", new Vector2(0.70f, 0.49f), new Vector2(340, 110), () => CreateRoom(4));

            ipField = kit.CreateInputField(p, "127.0.0.1", "IP del anfitrión", new Vector2(0.36f, 0.37f), new Vector2(400, 100));
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Neutral, "UNIRSE POR IP", new Vector2(0.74f, 0.37f), new Vector2(290, 110), JoinRoom);

            statusLabel = kit.CreateLabel(p, "Partida rápida: te une a una sala libre o crea una nueva y espera rivales.", new Vector2(0.5f, 0.23f), new Vector2(660, 150), 32, TextAlignmentOptions.Center);
            kit.CreateButton(p, OnlineUiKit.ButtonStyle.Danger, "VOLVER", new Vector2(0.5f, 0.10f), new Vector2(360, 110), Close);

            panel.SetActive(false);
            if (UiRoot.Instance != null) UiRoot.Instance.KeepLayersOnTop();
            if (MainMenuManager.Instance != null) MainMenuManager.Instance.RegisterOnlinePanel(panel);
        }
    }
}
