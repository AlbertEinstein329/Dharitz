using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class PanelPauseController : BasePanel
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button backToMenuButton;

        private void Start()
        {
            resumeButton.onClick.AddListener(() => Hide());
            optionsButton.onClick.AddListener(() => OptionsPanel.Instance.Show(this));
            restartButton.onClick.AddListener(() =>
                ConfirmPanel.Instance.Show("Restart Mission?", () =>
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                })
            );
            backToMenuButton.onClick.AddListener(() =>
                ConfirmPanel.Instance.Show("Back to Menu?", () =>
                {
                    SceneManager.LoadScene("Scene 1 Menu");
                })
            );
        }
    }
}