using UnityEngine;

namespace LapKan
{
    public class MissionSelectUI : MonoBehaviour
    {
        [SerializeField] private MissionButtonUI missionButtonPrefab;
        [SerializeField] private Transform gridParent;

        private void Start()
        {
            GenerateMissionButtons();
        }

        private void GenerateMissionButtons()
        {
            var missions = MissionProgressCenter.Instance.GetAllMissions();

            for (int i = 1; i <= missions.Count; i++)
            {
                var button = Instantiate(missionButtonPrefab, gridParent);

                bool available = MissionProgressCenter.Instance.CheckAvailable(i - 1);
                bool completed = MissionProgressCenter.Instance.IsCompleted(i - 1);

                button.Setup(i, available, completed);
            }
        }
    }
}