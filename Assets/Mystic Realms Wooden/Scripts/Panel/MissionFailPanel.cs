using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class MissionFailPanel : BasePanel
    {
        [SerializeField] private Button restartButton;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private Button reviveButton;

        private void Start()
        {
            restartButton.onClick.AddListener(OnRestart);
            backToMenuButton.onClick.AddListener(OnBackToMenu);
            reviveButton.onClick.AddListener(OnRevive);
        }

        private void OnRestart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnBackToMenu()
        {
            ConfirmPanel.Instance.Show("Back to Menu?", () =>
            {
                SceneManager.LoadScene("MainMenu");
            });
        }

        private void OnRevive()
        {
            if (CurrencyCenter.Instance.SpendCoins(100))
            {
                Hide();
                //MissionCenter.Instance.RevivePlayer();
            }
            else
            {
                Debug.Log("Not enough coins to revive!");
            }
        }
    }
}