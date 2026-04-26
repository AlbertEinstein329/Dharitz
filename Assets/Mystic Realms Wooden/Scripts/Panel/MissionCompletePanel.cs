using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class MissionCompletePanel : BasePanel
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text highestScoreText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button menuButton;

        private void Start()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            if (menuButton != null)
                menuButton.onClick.AddListener(OnMenuClicked);

            MissionCenter.Instance.OnMissionCompleted += ShowMissionComplete;
            MissionCenter.Instance.OnMissionFailed += ShowMissionFailed;
        }

        private void OnDestroy()
        {
            if (MissionCenter.Instance != null)
            {
                MissionCenter.Instance.OnMissionCompleted -= ShowMissionComplete;
                MissionCenter.Instance.OnMissionFailed -= ShowMissionFailed;
            }
        }

        private void ShowMissionComplete()
        {
            if (resultText != null) resultText.text = "Mission Complete!";
            UpdateScoreUI();
            Show();
        }

        private void ShowMissionFailed()
        {
            if (resultText != null) resultText.text = "Mission Failed!";
            UpdateScoreUI();
            Show();
        }

        private void UpdateScoreUI()
        {
            int currentScore = ScoreCenter.Instance.CurrentScore;
            int highestScore = ScoreCenter.Instance.HighestScore;

            if (scoreText != null) scoreText.text = $"{currentScore}";
            if (highestScoreText != null) highestScoreText.text = $"{highestScore}";
        }

        private void OnContinueClicked()
        {
            Debug.Log("Continue Button Clicked - Go to next mission/menu!");
            Hide();

            int currentNumb = MissionProgressCenter.Instance.MissionCurrent.missionID;
            PlayerPrefs.SetInt("CurrentMissionID", currentNumb + 1);
            SceneManager.LoadScene("Scene 3 Tile Match");
        }

        private void OnMenuClicked()
        {
            Debug.Log("Go to menu!");
            Hide();
            SceneManager.LoadScene("Scene 2 Central");
        }
    }
}