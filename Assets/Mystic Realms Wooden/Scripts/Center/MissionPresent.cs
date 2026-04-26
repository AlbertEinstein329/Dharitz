using UnityEngine;
using TMPro;

namespace LapKan
{
    public class MissionPresent : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text missionNameText;
        [SerializeField] private TMP_Text targetScoreText;
        [SerializeField] private TMP_Text moveLimitText;
        [SerializeField] private TMP_Text timeLimitText;
        [SerializeField] private TMP_Text rewardText;

        private void Start()
        {
            // Đăng ký sự kiện
            MissionCenter.Instance.OnMissionStart += UpdateMissionUI;
            MissionCenter.Instance.OnMissionCompleted += ShowMissionCompleted;
            MissionCenter.Instance.OnMissionFailed += ShowMissionFailed;

            // Nếu mission đã chạy sẵn (vd: continue game)
            if (MissionCenter.Instance.IsMissionActive && MissionCenter.Instance.CurrentMission != null)
            {
                UpdateMissionUI();
            }
        }

        private void OnDestroy()
        {
            if (MissionCenter.Instance != null)
            {
                MissionCenter.Instance.OnMissionStart -= UpdateMissionUI;
                MissionCenter.Instance.OnMissionCompleted -= ShowMissionCompleted;
                MissionCenter.Instance.OnMissionFailed -= ShowMissionFailed;
            }
        }

        /// <summary>
        /// Cập nhật UI khi mission bắt đầu
        /// </summary>
        private void UpdateMissionUI()
        {
            var mission = MissionCenter.Instance.CurrentMission;
            if (mission == null) return;

            if (missionNameText != null) missionNameText.text = mission.missionName;
            if (targetScoreText != null) targetScoreText.text = $"{mission.targetScore}";

            if (moveLimitText != null)
                moveLimitText.text = mission.moveLimit > 0 ? $"Moves: {mission.moveLimit}" : "";

            if (timeLimitText != null)
                timeLimitText.text = mission.timeLimit > 0 ? $"Time: {mission.timeLimit:F0}s" : "";

            if (rewardText != null)
                rewardText.text = $"Reward: {mission.rewardCoins} Coins {mission.rewardGems} Gems";
        }

        /// <summary>
        /// Hiển thị thông báo khi hoàn thành
        /// </summary>
        private void ShowMissionCompleted()
        {
            Debug.Log("Mission Completed UI Update!");
            // Bạn có thể bật panel "Mission Completed" ở đây
        }

        /// <summary>
        /// Hiển thị thông báo khi thất bại
        /// </summary>
        private void ShowMissionFailed()
        {
            Debug.Log("Mission Failed UI Update!");
            // Bạn có thể bật panel "Mission Failed" ở đây
        }
    }
}