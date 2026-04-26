using UnityEngine;

namespace LapKan
{
    public class MissionCenter : MonoBehaviour
    {
        public static MissionCenter Instance { get; private set; }

        [Header("Mission Database")]
        [SerializeField] private MissionDatabase missionDatabase;

        public MissionData CurrentMission { get; private set; }

        public int TargetScore => CurrentMission != null ? CurrentMission.targetScore : 0;

        public bool IsMissionActive { get; private set; }
        public bool IsMissionCompleted { get; private set; }

        public event System.Action OnMissionStart;
        public event System.Action OnMissionCompleted;
        public event System.Action OnMissionFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartMissionByID(int missionID)
        {
            CurrentMission = missionDatabase.GetMissionByID(missionID);
            if (CurrentMission == null)
            {
                Debug.LogError($"Mission {missionID} not found!");
                return;
            }

            IsMissionActive = true;
            IsMissionCompleted = false;

            ScoreCenter.Instance.ResetScore();

            Debug.Log($"Mission {missionID} started: {CurrentMission.missionName}");

            OnMissionStart?.Invoke();
        }

        public bool CheckMissionProgress()
        {
            if (!IsMissionActive || CurrentMission == null) return false;

            if (ScoreCenter.Instance.CurrentScore >= CurrentMission.targetScore)
            {
                CompleteMission();
                return true;
            }
            return false;
            // Logic fail mission nếu hết lượt hoặc hết thời gian (xử lý ngoài Update)
        }

        private void CompleteMission()
        {
            IsMissionCompleted = true;
            IsMissionActive = false;

            // TODO: Add reward to player profile
            Debug.Log($"Mission {CurrentMission.missionID} completed! Reward: {CurrentMission.rewardCoins} coins");

            OnMissionCompleted?.Invoke();
        }

        public void FailMission()
        {
            IsMissionActive = false;
            IsMissionCompleted = false;

            Debug.Log($"Mission {CurrentMission.missionID} failed!");

            OnMissionFailed?.Invoke();
        }
    }
}