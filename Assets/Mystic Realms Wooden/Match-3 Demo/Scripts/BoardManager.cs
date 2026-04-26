using UnityEngine;

namespace LapKan
{
    public class BoardManager : MonoBehaviour
    {
        public int columns = 8;
        public int rows = 8;
        public float cellSize = 1f;

        public GameObject nodeObjPrefab;
        public GameObject nodeSpritePrefab;

        public GameObject startedPoint;

        private NodeObj[,] board;

        void Start()
        {
            //board = new NodeObj[columns, rows];
            //CreateBoard();

            GenerateGrid();
        }

        void CreateBoard()
        {
            for (int col = 0; col < columns; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    // Tạo NodeObj (logic)
                    GameObject obj = Instantiate(nodeObjPrefab);
                    NodeObj nodeObj = obj.GetComponent<NodeObj>();
                    nodeObj.Init(col, row, this);
                    board[col, row] = nodeObj;
                    nodeObj.transform.position = GetWorldPos(col, row) + startedPoint.transform.position;

                    // Tạo NodeSprite (visual)
                    GameObject sprite = Instantiate(nodeSpritePrefab);
                    NodeSprite nodeSprite = sprite.GetComponent<NodeSprite>();
                    nodeSprite.transform.parent = startedPoint.transform;
                    nodeSprite.Init(nodeObj, cellSize);
                    nodeSprite.transform.position = GetWorldPos(col, row) + startedPoint.transform.position; // spawn đúng chỗ ban đầu
                }
            }
        }

        public Vector3 GetWorldPos(int col, int row)
        {
            return new Vector3(col * cellSize, row * cellSize, 0);
        }


        [Header("Board Settings")]
        //public int columns = 8;
        //public int rows = 8;
        public GameObject gemPrefab;
        public RectTransform rootedBoard; // UI hoặc empty object có RectTransform

        private float slotSize;
        private Vector2 startOffset;

        void GenerateGrid()
        {
            if (rootedBoard == null || gemPrefab == null) return;

            // Lấy size của RootedBoard (pixel hoặc world unit nếu RectTransform không trong Canvas)
            float boardWidth = rootedBoard.rect.width;
            float boardHeight = rootedBoard.rect.height;

            // Tính toán slotSize (fix theo chiều ngắn hơn để fit toàn bộ grid)
            float cellWidth = boardWidth / columns;
            float cellHeight = boardHeight / rows;
            slotSize = Mathf.Min(cellWidth, cellHeight);

            // Tính offset để grid nằm chính giữa RootedBoard
            float gridWidth = slotSize * columns;
            float gridHeight = slotSize * rows;
            startOffset = new Vector2(
                -gridWidth / 2f + slotSize / 2f,
                -gridHeight / 2f + slotSize / 2f
            );

            // Spawn gem
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector2 pos = new Vector2(x * slotSize + startOffset.x, y * slotSize + startOffset.y);
                    Vector3 worldPos = rootedBoard.TransformPoint(pos);

                    GameObject gem = Instantiate(gemPrefab, worldPos, Quaternion.identity, rootedBoard);
                    gem.transform.localScale = Vector3.one * (slotSize / 100f); // scale fit vào cell
                    gem.name = $"Gem_{x}_{y}";

                    // gọi random sprite cho GemObj
                    GemObj gemObj = gem.GetComponent<GemObj>();
                    if (gemObj != null)
                    {
                        gemObj.InitRandomGem();
                    }
                }
            }
        }
    }
}