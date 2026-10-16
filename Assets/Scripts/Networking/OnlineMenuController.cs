using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Net;
using System.Net.Sockets;

namespace MyGame.Networking
{
    /// <summary>
    /// Panel online del menu principal, construido por codigo (no hay que cablear botones en la escena).
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
        private Text statusLabel;
        private InputField ipField;
        private Font font;

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
        }

        private void CreateRoom(int players)
        {
            string ip = GetLocalIp();
            if (!Configure(players)) return;

            OnlineLaunchRequest.Set(OnlineRole.Host, "0.0.0.0", 7777);
            statusLabel.text = $"Creando sala de {players} jugadores...\nIP para unirse: {ip}";
            SceneManager.LoadScene(boardSceneIndex);
        }

        private void QuickMatch(MatchMode mode)
        {
            if (!Configure((int)mode)) return;

            OnlineLaunchRequest.SetQuickMatch(mode);
            statusLabel.text = $"Buscando partida de {(int)mode} jugadores...";
            SceneManager.LoadScene(boardSceneIndex);
        }

        private void JoinRoom()
        {
            // El numero de jugadores lo decide el host; el cliente solo necesita saber que es una partida online
            if (!Configure(2)) return;

            OnlineLaunchRequest.Set(OnlineRole.Client, ipField.text, 7777);
            statusLabel.text = $"Conectando a {OnlineLaunchRequest.Ip}...";
            SceneManager.LoadScene(boardSceneIndex);
        }

        private bool Configure(int players)
        {
            if (sessionConfig == null)
            {
                statusLabel.text = "Falta asignar SessionConfig en OnlineMenuController.";
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
            var canvasGo = new GameObject("OnlineMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // Boton de entrada, siempre visible en una esquina
            MakeButton(canvasGo.transform, "OnlineOpen", "ONLINE", new Vector2(0.85f, 0.05f), new Vector2(260, 90), () => panel.SetActive(true));

            panel = new GameObject("OnlinePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900, 1000);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.96f);

            MakeLabel(panel.transform, "Title", "JUGAR ONLINE", new Vector2(0.5f, 0.92f), new Vector2(800, 90), 56);
            MakeButton(panel.transform, "Quick2", "PARTIDA RAPIDA 2 JUGADORES", new Vector2(0.5f, 0.82f), new Vector2(760, 100), () => QuickMatch(MatchMode.TwoPlayers));
            MakeButton(panel.transform, "Quick4", "PARTIDA RAPIDA 4 JUGADORES", new Vector2(0.5f, 0.70f), new Vector2(760, 100), () => QuickMatch(MatchMode.FourPlayers));

            MakeLabel(panel.transform, "LanLabel", "Red local (sin matchmaking)", new Vector2(0.5f, 0.59f), new Vector2(760, 50), 30);
            MakeButton(panel.transform, "Create2", "CREAR SALA 2J", new Vector2(0.27f, 0.51f), new Vector2(370, 90), () => CreateRoom(2));
            MakeButton(panel.transform, "Create4", "CREAR SALA 4J", new Vector2(0.73f, 0.51f), new Vector2(370, 90), () => CreateRoom(4));

            ipField = MakeInput(panel.transform, "IpField", "127.0.0.1", new Vector2(0.37f, 0.39f), new Vector2(480, 90));
            MakeButton(panel.transform, "Join", "UNIRSE POR IP", new Vector2(0.79f, 0.39f), new Vector2(240, 90), JoinRoom);

            statusLabel = MakeLabel(panel.transform, "Status", "Partida rapida: te une a una sala libre o crea una nueva y espera rivales.", new Vector2(0.5f, 0.2f), new Vector2(800, 150), 30);
            MakeButton(panel.transform, "Close", "VOLVER", new Vector2(0.5f, 0.05f), new Vector2(400, 90), () => panel.SetActive(false));

            panel.SetActive(false);
        }

        private Text MakeLabel(Transform parent, string name, string text, Vector2 anchor, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;

            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private void MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;

            go.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.85f, 1f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            Text label = MakeLabel(go.transform, name + "Label", text, new Vector2(0.5f, 0.5f), size, 36);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
        }

        private InputField MakeInput(Transform parent, string name, string defaultText, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            Text text = MakeLabel(go.transform, name + "Text", string.Empty, new Vector2(0.5f, 0.5f), size, 40);
            text.raycastTarget = true;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = new Vector2(-20, 0);

            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.text = defaultText;
            input.contentType = InputField.ContentType.Standard;
            return input;
        }
    }
}
