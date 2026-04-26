using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LapKan
{
    public class ConfirmPanel : BasePanel
    {
        public static ConfirmPanel Instance;

        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private System.Action onConfirm;

        protected override void Awake()
        {
            Instance = this;
            base.Awake();
            yesButton.onClick.AddListener(() => { onConfirm?.Invoke(); Hide(); });
            noButton.onClick.AddListener(() => Hide());
        }

        public void Show(string message, System.Action confirmAction)
        {
            messageText.text = message;
            onConfirm = confirmAction;
            base.Show();
        }
    }
}