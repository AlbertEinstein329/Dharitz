using UnityEngine;
//using DG.Tweening;

namespace LapKan
{
    public class NodeSprite : MonoBehaviour
    {
        public NodeObj nodeObj;
        public float moveSpeed = 5f;

        private float cellSize;

        public void Init(NodeObj nodeObj, float cellSize)
        {
            this.nodeObj = nodeObj;
            this.cellSize = cellSize;
            nodeObj.OnDestroyEvent += HandleDestroyEffect;
        }

        void Update()
        {
            if (nodeObj == null) return;

            //Vector3 targetPos = nodeObj.GetWorldPos();
            //if (Vector3.Distance(transform.position, targetPos) > 0.01f)
            //{
            //    transform.position = Vector3.MoveTowards(
            //        transform.position,
            //        targetPos,
            //        moveSpeed * Time.deltaTime
            //    );
            //}
        }

        private void HandleDestroyEffect()
        {
            // Đảm bảo sprite tới vị trí nodeObj trước khi phá
            //transform.DOMove(nodeObj.GetWorldPos(), 0.2f).OnComplete(() =>
            //{
            //    transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 5, 0.5f)
            //             .OnComplete(() =>
            //             {
            //                 transform.DOScale(Vector3.zero, 0.2f)
            //                          .OnComplete(() => Destroy(gameObject));
            //             });
            //});

            nodeObj = null; // Ngắt tham chiếu
        }
    }
}