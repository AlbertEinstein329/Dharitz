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
        private GameObject copyIpButton;

        private const float TextRefreshInterval = 0.25f;
        private float nextTextRefresh;
        private int lastTimerSeconds = -1;
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

            // Los textos se recalculan 4 veces por segundo y solo se asignan si cambian: asignar un TMP regenera su malla
            bool refreshText = Time.unscaledTime >= nextTextRefresh;
            if (refreshText) nextTextRefresh = Time.unscaledTime + TextRefreshInterval;

            waitingLabel.gameObject.SetActive(!matchEverStarted);
            // La IP de la sala sigue en pantalla mientras falte alguien; el boton de copiar solo aparece entonces
            copyIpButton.SetActive(!matchEverStarted && ShouldShowHostIp(NetworkManager.Singleton));
            if (!matchEverStarted && refreshText)
            {
                MultiplayerMatchmaker mm = MultiplayerMatchmaker.Instance;
                string status = mm != null && !connected ? mm.LastStatus : null;
                if (!connected && !string.IsNullOrEmpty(status)) SetText(waitingLabel, status);
                else if (!connected) SetText(waitingLabel, Loc.T("hud_no_connection"));
                else if (net.IsServer) SetText(waitingLabel, WaitingForPlayersText(net));
                else SetText(waitingLabel, Loc.T("hud_connected_waiting"));
            }

            bool inPlay = matchEverStarted && game != null;
            turnLabel.gameObject.SetActive(inPlay);
            timerLabel.gameObject.SetActive(inPlay);

            if (inPlay)
            {
                int current = game.NetworkCurrentPlayerIndex.Value;
                bool gameOver = game.NetworkMatchPhase.Value == (int)Core.MatchPhase.GameOver;
                if (refreshText)
                {
                    if (gameOver) SetText(turnLabel, Loc.T("hud_match_over"));
                    else SetText(turnLabel, current == game.LocalPlayerIndex ? "<color=#7CFC00>" + Loc.T("hud_your_turn") + "</color>" : Loc.T("hud_player_turn", current + 1));
                }

                if (NetworkTurnManager.Instance != null && !gameOver)
                {
                    float t = Mathf.Max(0f, NetworkTurnManager.Instance.NetworkTimeRemaining.Value);
                    int seconds = Mathf.CeilToInt(t);
                    if (seconds != lastTimerSeconds)
                    {
                        // Solo cuando cambia el segundo: sin string nuevo ni malla nueva el resto de fotogramas
                        lastTimerSeconds = seconds;
                        timerLabel.text = seconds.ToString();
                        timerLabel.color = t <= 10f ? Color.red : OnlineLabelColor;
                    }
                }
                else if (lastTimerSeconds != -1)
                {
                    lastTimerSeconds = -1;
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

        // Asigna el texto solo si cambia (TMP regenera la malla en cada asignacion, aunque sea el mismo valor)
        private static void SetText(TMP_Text label, string text)
        {
            if (!string.Equals(label.text, text)) label.text = text;
        }

        // Sala creada por IP en esta maquina: hay que ensenar la IP para que el rival pueda unirse
        private static bool ShouldShowHostIp(NetworkManager net)
        {
            return net != null && net.IsListening && net.IsServer && OnlineLaunchRequest.LastRole == OnlineRole.Host
                && !string.IsNullOrEmpty(OnlineLaunchRequest.HostDisplayIp);
        }

        private static string WaitingForPlayersText(NetworkManager net)
        {
            string text = Loc.T("hud_waiting_players", net.ConnectedClientsIds.Count, GameManager.Instance.numPlayers);
            if (ShouldShowHostIp(net))
            {
                text += "\n" + Loc.T("hud_share_ip") + "\n<size=76><b>" + OnlineLaunchRequest.HostDisplayIp + "</b></size>\n<size=34>" + Loc.T("hud_port", OnlineLaunchRequest.Port) + "</size>";
            }
            return text;
        }

        private void CopyHostIp()
        {
            GUIUtility.systemCopyBuffer = OnlineLaunchRequest.HostDisplayIp;
            ShowBanner(Loc.T("hud_ip_copied"), 2f);
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
            // El HUD online cuelga del canvas unico del tablero, por debajo de pausa y fin de partida
            Transform parent = UiRoot.GetScreensParent("OnlineHudCanvas");
            root = new GameObject("OnlineHud", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            PlaceBelowModals(rootRect);
            root.AddComponent<SafeAreaFitter>(); // botones y textos dentro del area segura (notch, barra de gestos)

            waitingLabel = CreateLabel("Waiting", new Vector2(0.5f, 0.6f), new Vector2(980, 420), 48, TextAlignmentOptions.Center);
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

            // Salir de la partida vive solo en el menu de pausa (boton Main Menu), no durante el juego

            // Quien crea una sala por IP ve la suya sobre el tablero, grande y con boton de copiar, hasta que entre el rival
            copyIpButton = CreateButton(OnlineUiKit.ButtonStyle.Neutral, "CopyIp", "COPIAR IP", new Vector2(0.5f, 0.45f), new Vector2(520, 120), CopyHostIp);
            LocalizedText.Bind(copyIpButton.GetComponentInChildren<TMP_Text>(true), "copy_ip");
            copyIpButton.SetActive(false);

            for (int i = 0; i < EmojiLabels.Length; i++)
            {
                int id = i;
                CreateButton(OnlineUiKit.ButtonStyle.Neutral, "Emoji" + i, EmojiLabels[i], new Vector2(0.075f + i * 0.125f, 0.035f), new Vector2(125, 90), () => SendEmoji(id));
            }
        }

        // Pausa y fin de partida deben quedar por encima del HUD online (orden de hermanos = orden de dibujo)
        private static void PlaceBelowModals(RectTransform hud)
        {
            Transform canvasRoot = hud.parent;
            int index = hud.GetSiblingIndex();

            // El panel de pausa es un hijo directo del canvas llamado PauseMenu (PanelPauseController vive en otro objeto)
            Transform pauseMenu = canvasRoot.Find("PauseMenu");
            if (pauseMenu != null) index = Mathf.Min(index, pauseMenu.GetSiblingIndex());

            UIManager ui = FindFirstObjectByType<UIManager>();
            if (ui != null && ui.panelGameOver != null) index = Mathf.Min(index, ChildIndexUnder(canvasRoot, ui.panelGameOver.transform));

            hud.SetSiblingIndex(index);
        }

        private static int ChildIndexUnder(Transform canvasRoot, Transform descendant)
        {
            while (descendant != null && descendant.parent != canvasRoot) descendant = descendant.parent;
            return descendant != null ? descendant.GetSiblingIndex() : int.MaxValue;
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
