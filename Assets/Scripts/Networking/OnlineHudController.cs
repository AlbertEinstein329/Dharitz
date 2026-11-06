using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;
using SpankyBoy.JuiceUI.Free;

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

        private static readonly Color OnlineLabelColor = new Color(0.882f, 0.796f, 0.549f, 1f);

        private GameObject root;
        private TextMeshProUGUI waitingLabel;
        private TextMeshProUGUI turnLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI bannerLabel;
        private GameObject reconnectButton;
        private GameObject confirmButton;
        private Animate_Shake_Free bannerShake;
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
                else if (!connected) waitingLabel.text = Loc.T("hud_no_connection");
                else if (net.IsServer) waitingLabel.text = Loc.T("hud_waiting_players", net.ConnectedClientsIds.Count, GameManager.Instance.numPlayers);
                else waitingLabel.text = Loc.T("hud_connected_waiting");
            }

            bool inPlay = matchEverStarted && game != null;
            turnLabel.gameObject.SetActive(inPlay);
            timerLabel.gameObject.SetActive(inPlay);

            if (inPlay)
            {
                int current = game.NetworkCurrentPlayerIndex.Value;
                bool gameOver = game.NetworkMatchPhase.Value == (int)Core.MatchPhase.GameOver;
                if (gameOver) turnLabel.text = Loc.T("hud_match_over");
                else turnLabel.text = current == game.LocalPlayerIndex ? "<color=#7CFC00>" + Loc.T("hud_your_turn") + "</color>" : Loc.T("hud_player_turn", current + 1);

                if (NetworkTurnManager.Instance != null && !gameOver)
                {
                    float t = Mathf.Max(0f, NetworkTurnManager.Instance.NetworkTimeRemaining.Value);
                    timerLabel.text = Mathf.CeilToInt(t).ToString();
                    timerLabel.color = t <= 10f ? Color.red : OnlineLabelColor;
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

        private void HandleDisconnected(int slot) => ShowBanner(Loc.T("hud_player_disconnected", slot + 1), 6f);
        private void HandleReconnected(int slot) => ShowBanner(Loc.T("hud_player_reconnected", slot + 1), 3f);
        private void HandleEliminated(int slot) => ShowBanner(Loc.T("hud_player_eliminated", slot + 1), 4f);
        private void HandleEmoji(int slot, int emojiId) => ShowBanner(Loc.T("hud_player_emoji", slot + 1, Loc.T("emoji_" + Mathf.Clamp(emojiId, 0, EmojiLabels.Length - 1))), 2f);
        private void HandleEmojiError(string message) => ShowBanner(message, 2f);

        private void ShowBanner(string message, float seconds)
        {
            if (bannerLabel == null) return;
            bannerLabel.text = message;
            bannerLabel.gameObject.SetActive(true);
            bannerHideTime = Time.time + seconds;
            if (bannerShake != null) bannerShake.Animate();
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
            if (!ok) ShowBanner(Loc.T("hud_reconnect_failed"), 4f);
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
            bannerShake = bannerLabel.gameObject.AddComponent<Animate_Shake_Free>();
            bannerShake.duration = 0.35f;
            bannerShake.strength = 12f;
            bannerLabel.gameObject.SetActive(false);

            confirmButton = CreateButton(OnlineUiKit.ButtonStyle.Primary, "Confirm", "CONFIRMAR TURNO", new Vector2(0.5f, 0.13f), new Vector2(560, 130), ConfirmTurn);
            confirmButton.AddComponent<Animate_Pulse_Free>().pulseScale = 1.06f; // llama la atencion mientras haya que confirmar
            confirmButton.SetActive(false);

            reconnectButton = CreateButton(OnlineUiKit.ButtonStyle.Secondary, "Reconnect", "RECONECTAR", new Vector2(0.5f, 0.55f), new Vector2(520, 130), Reconnect);
            reconnectButton.SetActive(false);

            CreateButton(OnlineUiKit.ButtonStyle.Danger, "Leave", "SALIR", new Vector2(0.14f, 0.115f), new Vector2(200, 90), LeaveMatch);

            for (int i = 0; i < EmojiLabels.Length; i++)
            {
                int id = i;
                CreateButton(OnlineUiKit.ButtonStyle.Neutral, "Emoji" + i, EmojiLabels[i], new Vector2(0.075f + i * 0.125f, 0.035f), new Vector2(125, 90), () => SendEmoji(id));
            }
        }

        private TextMeshProUGUI CreateLabel(string name, Vector2 anchor, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            TMP_Text text = OnlineUiKit.Instance.CreateLabel(root.transform, string.Empty, anchor, size, fontSize, alignment);
            text.name = name;
            text.color = OnlineLabelColor;
            return (TextMeshProUGUI)text;
        }

        private GameObject CreateButton(OnlineUiKit.ButtonStyle style, string name, string label, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            Button button = OnlineUiKit.Instance.CreateButton(root.transform, style, label, anchor, size, onClick);
            button.name = name;
            return button.gameObject;
        }
    }
}
