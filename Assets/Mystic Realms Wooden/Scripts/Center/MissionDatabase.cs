using UnityEngine;

namespace LapKan
{
    [CreateAssetMenu(fileName = "MissionDatabase", menuName = "Game/Mission Database", order = 2)]
    public class MissionDatabase : ScriptableObject
    {
        public MissionData[] missions;

        public MissionData GetMissionByID(int id)
        {
            foreach (var mission in missions)
            {
                if (mission.missionID == id)
                    return mission;
            }
            return null;
        }

        public MissionData GetMissionByIndex(int index)
        {
            if (index >= 0 && index < missions.Length)
                return missions[index];
            return null;
        }
    }
}