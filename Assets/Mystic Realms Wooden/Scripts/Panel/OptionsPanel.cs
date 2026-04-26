using UnityEngine;
using UnityEngine.UI;

namespace LapKan
{
    public class OptionsPanel : BasePanel
    {
        public static OptionsPanel Instance;

        [SerializeField] private Button closeButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button saveButton;
        private BasePanel previousPanel;

        protected override void Awake()
        {
            Instance = this;
            base.Awake();
            closeButton.onClick.AddListener(Close);
            cancelButton.onClick.AddListener(Close);
            saveButton.onClick.AddListener(Close);
        }

        public void Show(BasePanel prev)
        {
            previousPanel = prev;
            base.Show();
        }

        private void Close()
        {
            Dismiss();
            if (previousPanel != null)
                previousPanel.Show();
        }
    }
}