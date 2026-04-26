using UnityEngine;

namespace LapKan
{
    public class ScoreCenter : MonoBehaviour
    {
        public static ScoreCenter Instance { get; private set; }

        private const string HighestScoreKey = "HighestScore";
        private const string CurrentScoreKey = "CurrentScore";

        private int currentScore;

        public int CurrentScore => currentScore;
        public int HighestScore => PlayerPrefs.GetInt(HighestScoreKey, 0);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            currentScore = PlayerPrefs.GetInt(CurrentScoreKey, 0);
        }

        public void AddScore(int amount)
        {
            currentScore += amount;
            SaveCurrentScore();
            CheckHighestScore();
        }

        public void MinusScore(int amount)
        {
            currentScore -= amount;
            if (currentScore < 0) currentScore = 0;
            SaveCurrentScore();
            CheckHighestScore();
        }

        public void SetScore(int value)
        {
            currentScore = Mathf.Max(0, value);
            SaveCurrentScore();
            CheckHighestScore();
        }

        public void ResetScore()
        {
            currentScore = 0;
            SaveCurrentScore();
        }

        private void CheckHighestScore()
        {
            if (currentScore > HighestScore)
            {
                PlayerPrefs.SetInt(HighestScoreKey, currentScore);
                PlayerPrefs.Save();
            }
        }

        private void SaveCurrentScore()
        {
            PlayerPrefs.SetInt(CurrentScoreKey, currentScore);
            PlayerPrefs.Save();
        }

        public void SetHighestScore(int value)
        {
            PlayerPrefs.SetInt(HighestScoreKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }
}