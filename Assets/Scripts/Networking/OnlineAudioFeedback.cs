using UnityEngine;

namespace MyGame.Networking
{
    /// <summary>
    /// Sonidos de los eventos de la partida online: empieza tu turno, cuenta atras, tiempo agotado, confirmar, deshacer,
    /// jugada rechazada, emojis y desconexiones. Reutiliza la biblioteca de AudioManager (nombres de sonido existentes).
    /// Lo anade OnlineHudController al arrancar, no hace falta tocar la escena.
    /// </summary>
    public class OnlineAudioFeedback : MonoBehaviour
    {
        private const int CountdownFromSeconds = 5;

        private bool subscribed;
        private int lastPlayer = -1;
        private int lastTurnNumber = -1;
        private int lastCountdownSecond = int.MaxValue;

        private void Update()
        {
            TrySubscribe();

            NetworkGameManager game = NetworkGameManager.Instance;
            NetworkTurnManager turn = NetworkTurnManager.Instance;
            if (game == null || turn == null || !game.HasSnapshot || game.LocalPlayerIndex < 0) return;

            NetworkMatchSnapshot snap = game.LastSnapshot;
            bool gameOver = snap.Phase == (byte)Core.MatchPhase.GameOver;
            bool myTurn = !gameOver && snap.CurrentPlayerIndex == game.LocalPlayerIndex;

            // Empieza tu turno
            if (myTurn && (snap.TurnNumber != lastTurnNumber || snap.CurrentPlayerIndex != lastPlayer))
            {
                Play("Draw");
                lastCountdownSecond = int.MaxValue;
            }
            lastTurnNumber = snap.TurnNumber;
            lastPlayer = snap.CurrentPlayerIndex;

            // Cuenta atras de los ultimos segundos de TU turno; al llegar a 0 suena el tiempo agotado
            if (myTurn)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, turn.NetworkTimeRemaining.Value));
                if (seconds != lastCountdownSecond && seconds <= CountdownFromSeconds)
                {
                    Play(seconds > 0 ? "Tick" : "TimeOut");
                }
                lastCountdownSecond = seconds;
            }
        }

        private void TrySubscribe()
        {
            if (subscribed || NetworkGameManager.Instance == null || ReconnectionManager.Instance == null || EmojiChatManager.Instance == null) return;

            NetworkGameManager.Instance.OnSnapshotApplied += HandleSnapshot;
            NetworkGameManager.Instance.OnPlacementRejected += HandlePlacementRejected;
            ReconnectionManager.Instance.OnPlayerDisconnectedGracePeriod += HandleDisconnected;
            ReconnectionManager.Instance.OnPlayerReconnected += HandleReconnected;
            EmojiChatManager.Instance.OnEmojiReceived += HandleEmoji;
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (!subscribed) return;
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnSnapshotApplied -= HandleSnapshot;
                NetworkGameManager.Instance.OnPlacementRejected -= HandlePlacementRejected;
            }
            if (ReconnectionManager.Instance != null)
            {
                ReconnectionManager.Instance.OnPlayerDisconnectedGracePeriod -= HandleDisconnected;
                ReconnectionManager.Instance.OnPlayerReconnected -= HandleReconnected;
            }
            if (EmojiChatManager.Instance != null) EmojiChatManager.Instance.OnEmojiReceived -= HandleEmoji;
        }

        // Confirmar el turno (los demas eventos ya tienen su sonido en OnlineMatchPresenter: colocar, mover, deshacer)
        private void HandleSnapshot(NetworkMatchSnapshot snap)
        {
            NetworkActionType type = snap.LastAction.Type;
            if (type == NetworkActionType.TurnConfirmed) Play("Placed");
            else if (type == NetworkActionType.TurnForced) Play("TimeOut");
        }

        private void HandlePlacementRejected() => Play("Error");
        private void HandleDisconnected(int slot) => Play("Error");
        private void HandleReconnected(int slot) => Play("Reward");
        private void HandleEmoji(int slot, int emojiId) => Play("Click");

        private static void Play(string soundName)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(soundName);
        }
    }
}
