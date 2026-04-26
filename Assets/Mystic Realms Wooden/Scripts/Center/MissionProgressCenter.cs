using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LapKan
{
    public class MissionProgressCenter : MonoBehaviour
    {
        public static MissionProgressCenter Instance { get; private set; }

        [Header("Database chứa các mission trong 1 chapter")]
        [SerializeField] private MissionDatabase missionDatabase;

        public MissionData MissionCurrent { get; private set; }
        private HashSet<int> missionCompleted = new HashSet<int>(); // save id completed

        private const string SaveKey = "MissionCompleted";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadProgress();
        }

        /// <summary>
        /// Start a mission
        /// </summary>
        public void StartMission(MissionData mission)
        {
            if (mission == null || !missionDatabase.missions.Contains(mission))
            {
                Debug.LogError("Mission không hợp lệ!");
                return;
            }
            MissionCurrent = mission;
            Debug.Log($"Start Mission: {mission.missionID}");
        }

        /// <summary>
        /// Đánh dấu mission hiện tại hoàn thành
        /// </summary>
        public void CompleteMission()
        {
            if (MissionCurrent == null) return;

            missionCompleted.Add(MissionCurrent.missionID - 1);
            SaveProgress();

            Debug.Log($"Complete Mission: {MissionCurrent.missionID}");
        }

        /// <summary>
        /// Lấy danh sách mission đã hoàn tất
        /// </summary>
        public List<MissionData> GetCompletedMissions()
        {
            List<MissionData> result = new List<MissionData>();
            foreach (var mission in missionDatabase.missions)
            {
                if (missionCompleted.Contains(mission.missionID))
                    result.Add(mission);
            }
            return result;
        }

        /// <summary>
        /// Lấy danh sách mission có thể chơi (đã mở khoá)
        /// </summary>
        public List<MissionData> GetAvailableMissions()
        {
            // rule: mở mission 0 + những mission ngay sau mission đã hoàn thành
            List<MissionData> available = new List<MissionData>();

            for (int i = 0; i < missionDatabase.missions.Length; i++)
            {
                MissionData mission = missionDatabase.missions[i];

                if (i == 0 || missionCompleted.Contains(missionDatabase.missions[i - 1].missionID))
                {
                    if (!missionCompleted.Contains(mission.missionID))
                        available.Add(mission);
                }
            }

            return available;
        }

        private void SaveProgress()
        {
            string data = string.Join(",", missionCompleted);
            PlayerPrefs.SetString(SaveKey, data);
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            missionCompleted.Clear();
            if (!PlayerPrefs.HasKey(SaveKey)) return;

            string data = PlayerPrefs.GetString(SaveKey);
            if (string.IsNullOrEmpty(data)) return;

            string[] ids = data.Split(',');
            foreach (var id in ids)
            {
                if (int.TryParse(id, out int missionId))
                    missionCompleted.Add(missionId);
            }
        }

        public bool CheckAvailable(int missionNumb)
        {
            return missionNumb == 0 || missionCompleted.Contains(missionNumb - 1); // -1
        }

        public bool IsCompleted(int missionNumb)
        {
            return missionCompleted.Contains(missionNumb);
        }

        public List<MissionData> GetAllMissions()
        {
            return new List<MissionData>(missionDatabase.missions);
        }
    }
}