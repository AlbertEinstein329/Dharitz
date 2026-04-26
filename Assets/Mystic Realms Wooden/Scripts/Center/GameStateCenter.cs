using UnityEngine;
using System;

namespace LapKan
{
    public enum GameState
    {
        ReadyChecking,   // Show mission info, chờ người chơi bấm Start
        Preprepare,      // Chuẩn bị board/gameplay trước khi chơi
        Playing,         // Đang chơi
        Pause,           // Đang tạm dừng
        Revive,          // Cơ hội hồi sinh (match-3 hay roguelite thường có)
        Win,             // Thắng cuộc
        Lose             // Thua cuộc
    }

    public class GameStateCenter : MonoBehaviour
    {
        public static GameStateCenter Instance { get; private set; }

        public GameState CurrentState { get; private set; }

        public event Action<GameState> OnStateChanged;

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

        private void Start()
        {
            // Khi mới vào scene, có thể cho game ở trạng thái ReadyChecking
            SetState(GameState.ReadyChecking);
        }

        public void SetState(GameState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;
            Debug.Log($"[GameStateCenter] State changed to: {newState}");

            OnStateChanged?.Invoke(newState);
        }

        // Một số hàm helper cho gọn code
        public bool IsPlaying() => CurrentState == GameState.Playing;
        public bool IsPaused() => CurrentState == GameState.Pause;
        public bool IsGameEnded() => CurrentState == GameState.Win || CurrentState == GameState.Lose;
    }
}