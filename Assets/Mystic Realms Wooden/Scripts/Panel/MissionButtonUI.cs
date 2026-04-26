using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class MissionButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text missionLabel;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject completeIcon; // ✅ icon hiển thị khi mission đã hoàn tất

        private int missionId;

        public void Setup(int missionId, bool available, bool completed)
        {
            this.missionId = missionId;
            missionLabel.text = $"{missionId}";

            // Set trạng thái nút
            button.interactable = available;
            lockIcon.SetActive(!available);
            completeIcon.SetActive(completed);

            // Gắn listener
            button.onClick.RemoveAllListeners();
            if (available)
            {
                button.onClick.AddListener(() => OnMissionClicked());
            }
        }

        private void OnMissionClicked()
        {
            Debug.Log($"Mission {missionId} selected!");
            PlayerPrefs.SetInt("CurrentMissionID", missionId);
            SceneManager.LoadScene("Match3Demo");
            //MissionProgressCenter.Instance.StartMission(missionId);
            // Load scene hoặc gọi MissionCenter.StartMission(...) tùy flow
        }
    }
}