using UnityEngine;

namespace LapKan
{
    [CreateAssetMenu(fileName = "MissionData", menuName = "Game/Mission Data", order = 1)]
    public class MissionData : ScriptableObject
    {
        [Header("Mission Info")]
        public int missionID;
        public string missionName;

        [Header("Mission InfoStatus")]
        public bool isCompleted;
        public bool isAvailable;

        [Header("Target Conditions")]
        public int targetScore;
        public int moveLimit;   // -1 nếu không giới hạn
        public float timeLimit; // -1 nếu không giới hạn

        [Header("Reward")]
        public int rewardCoins;
        public int rewardGems;

        [Header("Extra")]
        [TextArea] public string description;
    }
}