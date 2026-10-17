using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class GridManager2D : MonoBehaviour
    {
        public static GridManager2D Instance;

        [Header("Board")]
        public int columns = 8;
        public int rows = 8;
        public float slotSize = 1f;            // world units per cell
        public GameObject gemPrefab;
        public Transform rootedBoard;           // tâm board (Empty GameObject)

        [Header("Backboard (optional)")]
        public Sprite backboardSprite;
        public int backboardMarginPixel = 80;

        [Header("Border (optional)")]
        public Sprite borderSprite;
        public int borderMarginPixel = 80;

        [Header("Animation")]
        public float swapDuration = 0.2f;
        public float fallDurationPerCell = 0.06f;   // rơi mỗi ô
        public float spawnDropExtra = 2f;           // spawn trên đỉnh bao nhiêu ô

        private GameObject backboardObj;
        private GameObject borderObj;

        private GemObj[,] grid;
        private GemObj firstSelected;
        public bool IsBusy { get; private set; } = false;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            ScoreCenter.Instance.ResetScore();
            int missionNumb = PlayerPrefs.GetInt("CurrentMissionID", 1);
            MissionCenter.Instance.StartMissionByID(missionNumb);
            MissionProgressCenter.Instance.StartMission(MissionCenter.Instance.CurrentMission);
            BuildStaticDecor();
            BuildGrid();
            StartCoroutine(FillInitialNoImmediateMatches());
        }

        // ================= Decor (backboard/border) =================
        void BuildStaticDecor()
        {
            if (rootedBoard == null) { Debug.LogError("RootedBoard missing"); return; }

            float gridW = columns * slotSize;
            float gridH = rows * slotSize;

            if (backboardSprite)
            {
                if (backboardObj) Destroy(backboardObj);
                backboardObj = new GameObject("Backboard");
                backboardObj.transform.SetParent(rootedBoard, false);
                backboardObj.transform.localPosition = Vector3.zero;

                var sr = backboardObj.AddComponent<SpriteRenderer>();
                sr.sprite = backboardSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.sortingOrder = -2;

                float marginWorld = backboardMarginPixel / backboardSprite.pixelsPerUnit;
                sr.size = new Vector2(gridW + marginWorld, gridH + marginWorld);
            }

            if (borderSprite)
            {
                if (borderObj) Destroy(borderObj);
                borderObj = new GameObject("Border");
                borderObj.transform.SetParent(rootedBoard, false);
                borderObj.transform.localPosition = Vector3.zero;

                var sr = borderObj.AddComponent<SpriteRenderer>();
                sr.sprite = borderSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.sortingOrder = -1;

                float marginWorld = borderMarginPixel / borderSprite.pixelsPerUnit;
                sr.size = new Vector2(gridW + marginWorld, gridH + marginWorld);
            }
        }

        // ================= Grid build =================
        void BuildGrid()
        {
            if (gemPrefab == null) { Debug.LogError("GemPrefab missing"); return; }
            grid = new GemObj[columns, rows];

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    SpawnGemAt(x, y, directPlace: true);
                }
            }
        }

        void SpawnGemAt(int x, int y, bool directPlace)
        {
            Vector3 pos = GridToWorld(x, y);
            GameObject go = Instantiate(gemPrefab, directPlace ? pos : pos + Vector3.up * (spawnDropExtra * slotSize), Quaternion.identity, rootedBoard);
            var gem = go.GetComponent<GemObj>();
            if (gem == null) { Debug.LogError("GemPrefab missing GemObj"); return; }

            // scale sprite to slot
            var sr = gem.tileBack;
            float spriteW = sr.bounds.size.x;
            if (spriteW != 0)
            {
                float scale = slotSize / spriteW;
                go.transform.localScale = Vector3.one * scale;
            }

            gem.InitRandomGem();
            gem.gx = x;
            gem.gy = y;
            grid[x, y] = gem;

            if (!directPlace)
            {
                // thả xuống vị trí đúng
                StartCoroutine(MoveGemTo(gem, pos, fallDurationPerCell * (int)(spawnDropExtra + 0.5f)));
            }
        }

        Vector3 GridToWorld(int x, int y)
        {
            float gridW = columns * slotSize;
            float gridH = rows * slotSize;

            Vector2 startOffset = new Vector2(-gridW / 2f + slotSize / 2f, -gridH / 2f + slotSize / 2f);
            Vector2 local = new Vector2(x * slotSize + startOffset.x, y * slotSize + startOffset.y);
            return rootedBoard.position + (Vector3)local;
        }

        // ================= Input & Swap =================
        public void SelectGem(GemObj g)
        {
            if (IsBusy) return;

            if (firstSelected == null)
            {
                firstSelected = g;
                firstSelected.SetHighlight(true);
            }
            else
            {
                if (g == firstSelected)
                {
                    firstSelected.SetHighlight(false);
                    firstSelected = null;
                    return;
                }

                //chỉ swap nếu liền kề
                if (IsAdjacent(firstSelected, g))
                {
                    firstSelected.SetHighlight(false);
                    StartCoroutine(SwapAndResolve(firstSelected, g));
                    firstSelected = null;
                }
                else
                {
                    // chuyển selection sang viên mới
                    firstSelected.SetHighlight(false);
                    firstSelected = g;
                    firstSelected.SetHighlight(true);
                }
            }
        }

        bool IsAdjacent(GemObj a, GemObj b)
        {
            return Mathf.Abs(a.gx - b.gx) + Mathf.Abs(a.gy - b.gy) == 1;
        }

        IEnumerator SwapAndResolve(GemObj a, GemObj b)
        {
            IsBusy = true;

            // swap visual với bounce
            Vector3 posA = GridToWorld(a.gx, a.gy);
            Vector3 posB = GridToWorld(b.gx, b.gy);
            yield return StartCoroutine(SwapWithBounce(a, b, posB, posA, swapDuration));

            // swap logic vị trí trong grid
            //SwapInGrid(a, b);

            var matched = FindAllMatches();
            if (matched.Count == 0)
            {
                // không match → swap lại
                yield return StartCoroutine(SwapWithBounce(a, b, posA, posB, swapDuration));
                //SwapInGrid(a, b); // trả về logic
                IsBusy = false;
                yield break;
            }

            // Có match → clear & cascade
            yield return StartCoroutine(ResolveMatchesLoop());

            IsBusy = false;
        }

        void SwapInGrid(GemObj a, GemObj b)
        {
            int ax = a.gx, ay = a.gy;
            int bx = b.gx, by = b.gy;

            grid[ax, ay] = b;
            grid[bx, by] = a;

            a.gx = bx; a.gy = by;
            b.gx = ax; b.gy = ay;
        }

        // ================= EASING & MOVE =================
        IEnumerator MoveGemTo(GemObj gem, Vector3 target, float duration)
        {
            Vector3 start = gem.transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                // dùng EaseOutBack (nảy nhẹ)
                float e = EaseOutBack(k, 1.1f); // 1.1 = overshoot
                if (gem != null) gem.transform.position = Vector3.LerpUnclamped(start, target, e);
                yield return null;
            }
            if (gem != null) gem.transform.position = target;
        }

        IEnumerator SwapWithBounce(GemObj a, GemObj b, Vector3 targetA, Vector3 targetB, float duration)
        {
            Vector3 startA = a.transform.position;
            Vector3 startB = b.transform.position;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = EaseOutBack(k, 1.1f);
                a.transform.position = Vector3.LerpUnclamped(startA, targetA, e);
                b.transform.position = Vector3.LerpUnclamped(startB, targetB, e);
                yield return null;
            }

            a.transform.position = targetA;
            b.transform.position = targetB;

            SwapInGrid(a, b);
        }

        // EaseOutBack: nảy nhẹ/overshoot rồi về đích
        float EaseOutBack(float t, float overshoot = 1.2f)
        {
            // công thức tiêu chuẩn (s = overshoot)
            float s = overshoot;
            t = t - 1f;
            return (t * t * ((s + 1f) * t + s) + 1f);
        }

        // ================= Match / Clear / Gravity / Refill =================
        HashSet<Vector2Int> FindAllMatches()
        {
            var matched = new HashSet<Vector2Int>();

            // Hàng
            for (int y = 0; y < rows; y++)
            {
                int run = 1;
                for (int x = 1; x < columns; x++)
                {
                    if (SameType(x, y, x - 1, y)) run++;
                    else
                    {
                        if (run >= 3)
                            for (int k = 0; k < run; k++) matched.Add(new Vector2Int(x - 1 - k, y));
                        run = 1;
                    }
                }
                if (run >= 3)
                    for (int k = 0; k < run; k++) matched.Add(new Vector2Int(columns - 1 - k, y));
            }

            // Cột
            for (int x = 0; x < columns; x++)
            {
                int run = 1;
                for (int y = 1; y < rows; y++)
                {
                    if (SameType(x, y, x, y - 1)) run++;
                    else
                    {
                        if (run >= 3)
                            for (int k = 0; k < run; k++) matched.Add(new Vector2Int(x, y - 1 - k));
                        run = 1;
                    }
                }
                if (run >= 3)
                    for (int k = 0; k < run; k++) matched.Add(new Vector2Int(x, rows - 1 - k));
            }

            return matched;
        }

        bool SameType(int x1, int y1, int x2, int y2)
        {
            var a = InGrid(x1, y1) ? grid[x1, y1] : null;
            var b = InGrid(x2, y2) ? grid[x2, y2] : null;
            if (a == null || b == null) return false;
            return a.gemType == b.gemType;
        }

        bool InGrid(int x, int y) => x >= 0 && x < columns && y >= 0 && y < rows;

        IEnumerator ResolveMatchesLoop()
        {
            while (true)
            {
                var matched = FindAllMatches();
                if (matched.Count == 0) yield break;

                // Clear (fade nhanh rồi destroy)
                yield return StartCoroutine(ClearMatched(matched));

                // Collapse + Refill
                yield return StartCoroutine(CollapseColumns());
                yield return StartCoroutine(RefillColumns());

                // lặp lại nếu còn match (cascade)
            }
        }

        IEnumerator ClearMatched(HashSet<Vector2Int> matched)
        {
            // fade nhanh
            float fadeTime = 0.12f;
            Dictionary<GemObj, float> startAlpha = new Dictionary<GemObj, float>();
            foreach (var p in matched)
            {
                var g = grid[p.x, p.y];
                if (g == null) continue;
                var sr = g.tileSymbol;
                startAlpha[g] = sr.color.a;
            }

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float k = t / fadeTime;
                foreach (var p in matched)
                {
                    var g = grid[p.x, p.y];
                    if (g == null) continue;
                    var sr = g.tileSymbol;
                    var c = sr.color;
                    c.a = Mathf.Lerp(startAlpha[g], 0f, k);
                    sr.color = c;
                }
                yield return null;
            }

            // destroy + null grid
            foreach (var p in matched)
            {
                var g = grid[p.x, p.y];
                if (g != null)
                {
                    Destroy(g.gameObject);
                    grid[p.x, p.y] = null;
                    ScoreCenter.Instance.AddScore(10);
                    // TODO: Score here
                }
            }
            bool completed = MissionCenter.Instance.CheckMissionProgress();
            if (completed) MissionProgressCenter.Instance.CompleteMission();
        }

        IEnumerator CollapseColumns()
        {
            // cho từng cột, kéo các gem rơi xuống lấp chỗ trống
            List<IEnumerator> motions = new List<IEnumerator>();

            for (int x = 0; x < columns; x++)
            {
                int writeY = 0; // vị trí thấp nhất để ghi gem
                for (int y = 0; y < rows; y++)
                {
                    if (grid[x, y] != null)
                    {
                        if (y != writeY)
                        {
                            var g = grid[x, y];
                            grid[x, y] = null;
                            grid[x, writeY] = g;
                            int dy = y - writeY;
                            g.gx = x; g.gy = writeY;

                            Vector3 target = GridToWorld(x, writeY);
                            motions.Add(MoveGemTo(g, target, Mathf.Max(0.08f, dy * fallDurationPerCell)));
                        }
                        writeY++;
                    }
                }
            }

            // chạy tất cả motion song song
            foreach (var m in motions) StartCoroutine(m);
            // chờ hết
            foreach (var _ in motions) yield return null;
            // đợi 1 frame để ổn định
            yield return null;
        }

        IEnumerator RefillColumns()
        {
            List<IEnumerator> motions = new List<IEnumerator>();

            for (int x = 0; x < columns; x++)
            {
                for (int y = rows - 1; y >= 0; y--)
                {
                    if (grid[x, y] == null)
                    {
                        // spawn ở trên và rơi xuống
                        Vector3 topSpawn = GridToWorld(x, rows - 1) + Vector3.up * (spawnDropExtra * slotSize);
                        GameObject go = Instantiate(gemPrefab, topSpawn, Quaternion.identity, rootedBoard);
                        var gem = go.GetComponent<GemObj>();
                        if (gem == null) { Debug.LogError("GemPrefab missing GemObj"); continue; }

                        // scale
                        var sr = go.GetComponent<GemObj>().tileBack;
                        float spriteW = sr.bounds.size.x;
                        if (spriteW != 0)
                        {
                            float scale = slotSize / spriteW;
                            go.transform.localScale = Vector3.one * scale;
                        }

                        // đảm bảo không tạo match ngay lập tức quá rõ ràng (optional)
                        // Ở đây cứ random rồi để hệ thống tự clear cascade.

                        gem.InitRandomGem();
                        gem.gx = x; gem.gy = y;
                        grid[x, y] = gem;

                        Vector3 target = GridToWorld(x, y);
                        float duration = Mathf.Max(0.1f, (spawnDropExtra + (rows - 1 - y)) * fallDurationPerCell);
                        motions.Add(MoveGemTo(gem, target, duration));
                    }
                }
            }

            foreach (var m in motions) StartCoroutine(m);
            foreach (var _ in motions) yield return null;
            yield return null;
        }

        // ================= Khởi tạo không match ngay lập tức (cho đẹp) =================
        IEnumerator FillInitialNoImmediateMatches()
        {
            // Sau khi build grid, nếu có match sẵn thì random lại các ô đó
            // Lặp vài lần cho đến khi ổn (tránh vòng lặp vô tận).
            int safety = 100;
            while (safety-- > 0)
            {
                var matched = FindAllMatches();
                if (matched.Count == 0) break;

                foreach (var p in matched)
                {
                    var g = grid[p.x, p.y];
                    if (g == null) continue;

                    // random lại khác type hiện tại
                    int cur = (int)g.gemType;
                    int newIdx = cur;
                    int tries = 10;
                    while (tries-- > 0 && newIdx == cur)
                    {
                        newIdx = Random.Range(0, 6);
                    }
                    g.SetTypeByIndex(newIdx);
                }
                yield return null;
            }
        }
    }
}