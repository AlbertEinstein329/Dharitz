using UnityEngine;

namespace LapKan
{
    public class NodeObj : MonoBehaviour
    {
        public int col;
        public int row;

        private BoardManager board;

        public void Init(int col, int row, BoardManager board)
        {
            this.col = col;
            this.row = row;
            this.board = board;
        }

        public Vector3 GetWorldPos()
        {
            return board.GetWorldPos(col, row);
        }

        public void MoveTo(int newCol, int newRow)
        {
            col = newCol;
            row = newRow;
        }

        public void DestroyWithEffect()
        {
            // Logic để báo NodeSprite biết chuẩn bị hủy
            OnDestroyEvent?.Invoke();
        }

        public event System.Action OnDestroyEvent;
    }
}