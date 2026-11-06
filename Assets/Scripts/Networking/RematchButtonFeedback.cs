using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.Networking
{
    /// <summary>
    /// Texto del boton «Jugar de nuevo» en las partidas por IP: muestra cuantos jugadores quieren la revancha
    /// («JUGAR DE NUEVO (1/2)») y, una vez pulsado, «ESPERANDO AL RIVAL». Se anade solo desde PanelPauseController.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RematchButtonFeedback : MonoBehaviour
    {
        private Button button;
        private TMP_Text caption;
        private bool pressed;
        private bool subscribed;
        private int lastVotes, lastNeeded;

        private void Awake()
        {
            button = GetComponent<Button>();
            caption = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            pressed = false;
            lastVotes = 0;
            if (button != null) button.interactable = true;
            Refresh();
        }

        private void Update()
        {
            if (!subscribed && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnRematchVotes += HandleVotes;
                NetworkGameManager.Instance.OnRematchStarted += HandleStarted;
                subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (!subscribed || NetworkGameManager.Instance == null) return;
            NetworkGameManager.Instance.OnRematchVotes -= HandleVotes;
            NetworkGameManager.Instance.OnRematchStarted -= HandleStarted;
        }

        /// <summary>El jugador local ya pidio la revancha: el boton espera al rival.</summary>
        public void MarkPressed()
        {
            pressed = true;
            if (button != null) button.interactable = false;
            Refresh();
        }

        private void HandleVotes(int votes, int needed)
        {
            lastVotes = votes;
            lastNeeded = needed;
            Refresh();
        }

        private void HandleStarted()
        {
            pressed = false;
            lastVotes = 0;
            if (button != null) button.interactable = true;
            Refresh();
        }

        private void Refresh()
        {
            if (caption == null) return;

            // Se desactiva la traduccion automatica de este texto: lo gobierna este componente
            var localized = caption.GetComponent<LocalizedText>();
            if (localized != null && localized.enabled) localized.enabled = false;

            if (pressed) caption.text = Loc.T("rematch_waiting", Mathf.Max(1, lastVotes), Mathf.Max(2, lastNeeded));
            else if (lastVotes > 0) caption.text = Loc.T("rematch_votes", lastVotes, Mathf.Max(2, lastNeeded));
            else caption.text = Loc.T("play_again");
        }
    }
}
