using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

namespace MyGame.Networking
{
    /// <summary>
    /// HUD del modo online construido por código (sin cablear nada a mano en la escena):
    /// sala de espera, turno actual, temporizador, aviso de desconexión, emojis y botón de abandonar.
    /// Solo se muestra cuando SessionConfig.isOnlineMatch está activo.
    /// </summary>
    public class OnlineHudController : MonoBehaviour
    {
        [Tooltip("Indice de escena a cargar al abandonar la partida (MenuPrincipal)")]
        public int menuSceneIndex = 1;

        private static readonly string[] EmojiLabels = { "GG", "WOW", "UPS", "HOLA" };

        private GameObject root;
        private TextMeshProUGUI waitingLabel;
        private TextMeshProUGUI turnLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI bannerLabel;
        private GameObject reconnectButton;
        private GameObject confirmButton;
        private float bannerHideTime;

        private bool subscribed;
        private bool matchEverStarted;

        private void Start()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsOnlineMatch)
            {
                enabled = false;
                return;
            }

            BuildUi();

            // Sonidos de la partida online (tu turno, cuenta atras, rechazos...); no requiere tocar la escena
            if (GetComponent<OnlineAudioFeedback>() == null) gameObject.AddComponent<OnlineAudioFeedback>();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (root == null) return;

            TrySubscribe();

            NetworkManager net = NetworkManager.Singleton;
            NetworkGameManager game = NetworkGameManager.Instance;
            bool connected = net != null && net.IsListening;
            bool started = connected && game != null && (game.LocalPlayerIndex >= 0 || game.IsMatchStarted);
            if (started) matchEverStarted = true;

            waitingLabel.gameObject.SetActive(!matchEverStarted);
            if (!matchEverStarted)
            {
                MultiplayerMatchmaker mm = MultiplayerMatchmaker.Instance;
                if (!connected && mm != null && !string.IsNullOrEmpty(mm.LastStatus)) waitingLabel.text = mm.LastStatus;
                else if (!connected) waitingLabel.text = "Sin conexion.\nInicia Host / Client desde el panel de pruebas.";
                else if (net.IsServer) waitingLabel.text = $"Esperando jugadores ({net.ConnectedClientsIds.Count}/{GameManager.Instance.numPlayers})...";
                else waitingLabel.text = "Conectado. Esperando al inicio de la partida...";
            }

            bool inPlay = matchEverStarted && game != null;
            turnLabel.gameObject.SetActive(inPlay);
            timerLabel.gameObject.SetActive(inPlay);

            if (inPlay)
            {
                int current = game.NetworkCurrentPlayerIndex.Value;
                bool gameOver = game.NetworkMatchPhase.Value == (int)Core.MatchPhase.GameOver;
                if (gameOver) turnLabel.text = "Partida terminada";
                else turnLabel.text = current == game.LocalPlayerIndex ? "<color=#7CFC00>TU TURNO</color>" : $"Turno de Jugador {current + 1}";

                if (NetworkTurnManager.Instance != null && !gameOver)
                {
                    float t = Mathf.Max(0f, NetworkTurnManager.Instance.NetworkTimeRemaining.Value);
                    timerLabel.text = Mathf.CeilToInt(t).ToString();
                    timerLabel.color = t <= 10f ? Color.red : Color.white;
                }
                else
                {
                    timerLabel.text = string.Empty;
                }
            }

            // Fase de confirmar: el dado ya esta colocado y el turno sigue abierto hasta pulsar CONFIRMAR
            bool showConfirm = inPlay && game.HasSnapshot && game.LastSnapshot.TurnPlaced
                && game.LastSnapshot.CurrentPlayerIndex == game.LocalPlayerIndex
                && game.LastSnapshot.Phase == (byte)Core.MatchPhase.PlayerTurn;
            confirmButton.SetActive(showConfirm);

            // Caida de conexion en mitad de la partida: se ofrece volver a la sesion
            MultiplayerMatchmaker matchmaker = MultiplayerMatchmaker.Instance;
            bool canReconnect = matchEverStarted && !connected && matchmaker != null
                && !string.IsNullOrEmpty(matchmaker.LastSessionId) && !matchmaker.IsSearching;
            reconnectButton.SetActive(canReconnect);

            if (bannerLabel.gameObject.activeSelf && Time.time > bannerHideTime)
                bannerLabel.gameObject.SetActive(false);
        }

        // =========================================================
        // EVENTOS DE RED
        // =========================================================

        private void TrySubscribe()
        {
            if (subscribed || ReconnectionManager.Instance == null || EmojiChatManager.Instance == null) return;

            ReconnectionManager.Instance.OnPlayerDisconnectedGracePeriod += HandleDisconnected;
            ReconnectionManager.Instance.OnPlayerReconnected += HandleReconnected;
            ReconnectionManager.Instance.OnPlayerEliminatedByTimeout += HandleEliminated;
            EmojiChatManager.Instance.OnEmojiReceived += HandleEmoji;
            EmojiChatManager.Instance.OnEmojiError += HandleEmojiError;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (ReconnectionManager.Instance != null)
            {
                ReconnectionManager.Instance.OnPlayerDisconnectedGracePeriod -= HandleDisconnected;
                ReconnectionManager.Instance.OnPlayerReconnected -= HandleReconnected;
                ReconnectionManager.Instance.OnPlayerEliminatedByTimeout -= HandleEliminated;
            }
            if (EmojiChatManager.Instance != null)
            {
                EmojiChatManager.Instance.OnEmojiReceived -= HandleEmoji;
                EmojiChatManager.Instance.OnEmojiError -= HandleEmojiError;
            }
            subscribed = false;
        }

        private void HandleDisconnected(int slot) => ShowBanner($"Jugador {slot + 1} desconectado. Esperando reconexion (30 s)...", 6f);
        private void HandleReconnected(int slot) => ShowBanner($"Jugador {slot + 1} se ha reconectado", 3f);
        private void HandleEliminated(int slot) => ShowBanner($"Jugador {slot + 1} eliminado por abandono", 4f);
        private void HandleEmoji(int slot, int emojiId) => ShowBanner($"Jugador {slot + 1}: {EmojiLabels[Mathf.Clamp(emojiId, 0, EmojiLabels.Length - 1)]}", 2f);
        private void HandleEmojiError(string message) => ShowBanner(message, 2f);

        private void ShowBanner(string message, float seconds)
        {
            if (bannerLabel == null) return;
            bannerLabel.text = message;
            bannerLabel.gameObject.SetActive(true);
            bannerHideTime = Time.time + seconds;
        }

        // =========================================================
        // ACCIONES DE UI
        // =========================================================

        private void LeaveMatch()
        {
            if (MultiplayerMatchmaker.Instance != null) _ = MultiplayerMatchmaker.Instance.LeaveSessionAsync();

            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.Disconnect();
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene(menuSceneIndex);
        }

        private void ConfirmTurn()
        {
            if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.RequestConfirmTurnServerRpc();
        }

        private async void Reconnect()
        {
            if (MultiplayerMatchmaker.Instance == null) return;

            bool ok = await MultiplayerMatchmaker.Instance.ReconnectAsync();
            if (!ok) ShowBanner("No se pudo reconectar. Puedes salir al menu.", 4f);
        }

        private void SendEmoji(int id)
        {
            if (EmojiChatManager.Instance != null) EmojiChatManager.Instance.SendEmoji(id);
        }

        // =========================================================
        // CONSTRUCCION DE LA UI
        // =========================================================

        private void BuildUi()
        {
            root = new GameObject("OnlineHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            waitingLabel = CreateLabel("Waiting", new Vector2(0.5f, 0.55f), new Vector2(900, 200), 48, TextAlignmentOptions.Center);
            turnLabel = CreateLabel("Turn", new Vector2(0.5f, 0.965f), new Vector2(700, 70), 44, TextAlignmentOptions.Center);
            timerLabel = CreateLabel("Timer", new Vector2(0.9f, 0.965f), new Vector2(160, 70), 54, TextAlignmentOptions.Center);
            bannerLabel = CreateLabel("Banner", new Vector2(0.5f, 0.9f), new Vector2(980, 90), 36, TextAlignmentOptions.Center);
            bannerLabel.color = new Color(1f, 0.8f, 0.2f);
            bannerLabel.gameObject.SetActive(false);

            confirmButton = CreateButton("Confirm", "CONFIRMAR TURNO", new Vector2(0.5f, 0.13f), new Vector2(560, 110), ConfirmTurn);
            confirmButton.SetActive(false);

            reconnectButton = CreateButton("Reconnect", "RECONECTAR", new Vector2(0.5f, 0.55f), new Vector2(520, 110), Reconnect);
            reconnectButton.SetActive(false);

            CreateButton("Leave", "SALIR", new Vector2(0.1f, 0.965f), new Vector2(180, 70), LeaveMatch);

            for (int i = 0; i < EmojiLabels.Length; i++)
            {
                int id = i;
                CreateButton("Emoji" + i, EmojiLabels[i], new Vector2(0.14f + i * 0.24f, 0.03f), new Vector2(190, 80), () => SendEmoji(id));
            }
        }

        private TextMeshProUGUI CreateLabel(string name, Vector2 anchor, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(root.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private GameObject CreateButton(string name, string label, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;

            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var text = CreateLabel(name + "Label", new Vector2(0.5f, 0.5f), size, 34, TextAlignmentOptions.Center);
            text.transform.SetParent(go.transform, false);
            text.text = label;
            return go;
        }
    }
}
