using UnityEngine;
using TMPro;

namespace LapKan
{
    public class ScorePresent : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text currentScoreText;
        [SerializeField] private TMP_Text targetScoreText;

        [Header("Settings")]
        [SerializeField] private int stepScore = 5; // số điểm thay đổi mỗi frame update

        public int DisplayScore { get; private set; }

        private int CurrentScore => ScoreCenter.Instance.CurrentScore;
        private int TargetScore;// = MissionCenter.Instance.TargetScore; // cần MissionCenter

        private void Start()
        {
            // Khởi tạo giá trị ban đầu
            DisplayScore = CurrentScore;
            MissionCenter.Instance.OnMissionStart += UpdateData;
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (MissionCenter.Instance != null)
            {
                MissionCenter.Instance.OnMissionStart -= UpdateData;
            }
        }

        public void UpdateData()
        {
            DisplayScore = 0;
            TargetScore = MissionCenter.Instance.TargetScore;
        }

        private void Update()
        {
            UpdateDisplayScore();
            UpdateUI();
        }

        /// <summary>
        /// Tăng/giảm DisplayScore để dần đạt đến CurrentScore
        /// </summary>
        private void UpdateDisplayScore()
        {
            if (DisplayScore == CurrentScore) return;

            if (DisplayScore < CurrentScore)
            {
                DisplayScore += stepScore;
                if (DisplayScore > CurrentScore) DisplayScore = CurrentScore;
            }
            else if (DisplayScore > CurrentScore)
            {
                DisplayScore -= stepScore;
                if (DisplayScore < CurrentScore) DisplayScore = CurrentScore;
            }
        }

        /// <summary>
        /// Cập nhật UI TMP
        /// </summary>
        private void UpdateUI()
        {
            if (currentScoreText != null)
                currentScoreText.text = DisplayScore.ToString();

            if (targetScoreText != null)
                targetScoreText.text = TargetScore.ToString();
        }

        /// <summary>
        /// Cho phép chỉnh StepScore runtime (vd: hiệu ứng tăng tốc khi combo)
        /// </summary>
        public void SetStepScore(int step)
        {
            stepScore = Mathf.Max(1, step);
        }

        /// <summary>
        /// Cho phép gán TMP từ ngoài (nếu không drag từ inspector)
        /// </summary>
        public void SetUIRefs(TMP_Text current, TMP_Text target)
        {
            currentScoreText = current;
            targetScoreText = target;
        }
    }
}