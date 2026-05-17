using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour, IGridValidator
{
    [System.Serializable]
    public class DieData
    {
        public DieColor color;
        public int groupId;
        public int value;

        public DieData(DieColor c, int g, int v) { color = c; groupId = g; value = v; }
    }

    [Header("Configuracion del Tablero")]
    public int rows = 8;
    public int cols = 10;
    public float cellSize = 1.1f;

    [Header("Prefabs y Referencias")]
    public GameObject cellPrefab;
    public GameObject redPrefab, bluePrefab, whitePrefab, blackPrefab;

    private Dictionary<DieColor, GameObject> prefabDict;

    private List<DieData[,]> allBoardsLogic;
    private List<CellComponent[,]> allCellsVisual;
    private GameObject[] boardRoots;
    public int currentlyViewedPlayer = 0;
    private GameObject temporaryDie;

    private float startX;
    private float startY;

    void Awake()
    {
        allBoardsLogic = new List<DieData[,]>();
        allCellsVisual = new List<CellComponent[,]>();

        prefabDict = new Dictionary<DieColor, GameObject>
        {
            { DieColor.Red, redPrefab },
            { DieColor.Blue, bluePrefab },
            { DieColor.White, whitePrefab },
            { DieColor.Black, blackPrefab }
        };
    }

    void Start()
    {
        startX = -((cols - 1) * cellSize) / 2f;
        startY = -((rows - 1) * cellSize) / 2f;

        int numPlayers = GameManager.Instance.numPlayers;
        boardRoots = new GameObject[numPlayers];

        for (int p = 0; p < numPlayers; p++)
        {
            allBoardsLogic.Add(new DieData[rows, cols]);
            allCellsVisual.Add(new CellComponent[rows, cols]);

            boardRoots[p] = new GameObject($"Tablero_Jugador_{p + 1}");
            boardRoots[p].transform.SetParent(this.transform);

            GenerateGridForPlayer(p);
        }

        SwitchViewTo(0);
    }

    void GenerateGridForPlayer(int playerIndex)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector3 position = new Vector3(startX + (c * cellSize), startY + (r * cellSize), 0);
                GameObject newCell = Instantiate(cellPrefab, position, Quaternion.identity, boardRoots[playerIndex].transform);

                CellComponent cellScript = newCell.GetComponent<CellComponent>();
                if (cellScript != null)
                {
                    cellScript.Setup(r, c, playerIndex, this, GameManager.Instance, GameManager.Instance);
                    allCellsVisual[playerIndex][r, c] = cellScript;
                }
            }
        }
    }

    public void SwitchViewTo(int playerIndex)
    {
        currentlyViewedPlayer = playerIndex;
        for (int i = 0; i < boardRoots.Length; i++)
        {
            if (boardRoots[i] != null)
            {
                boardRoots[i].SetActive(i == currentlyViewedPlayer);
            }
        }

        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.isGameOver && GameManager.Instance.players.Count > playerIndex)
            {
                int scoreDelJugador = GameManager.Instance.players[playerIndex].score;
                UIManager.Instance.UpdateScore(scoreDelJugador);
            }

            if (GameManager.Instance.isGameOver)
            {
                UIManager.Instance.ShowFinalResults(playerIndex);
            }
        }
    }

    private bool IsValidPlayerIndex(int pIndex)
    {
        if (allBoardsLogic == null || pIndex < 0 || pIndex >= allBoardsLogic.Count)
        {
            Debug.LogError($"Invalid player index: {pIndex}");
            return false;
        }
        return true;
    }

    public void ViewNextBoard()
    {
        int next = (currentlyViewedPlayer + 1) % GameManager.Instance.numPlayers;
        SwitchViewTo(next);
    }

    public void ViewPreviousBoard()
    {
        int prev = currentlyViewedPlayer - 1;
        if (prev < 0) prev = GameManager.Instance.numPlayers - 1;
        SwitchViewTo(prev);
    }

    // --- DELEGATION TO PLACEMENT VALIDATOR ---
    public bool TryPlaceDie(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        if (!PlacementValidator.CanBotPlaceHere(allBoardsLogic[pIndex], rows, cols, r, c, color, groupId, number, GameManager.Instance.players[pIndex], GameManager.Instance.currentVariant))
            return false;

        InstantiateDieVisual(pIndex, r, c, color, number);
        allBoardsLogic[pIndex][r, c] = new DieData(color, groupId, number);
        return true;
    }

    public bool CanBotPlaceHere(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        return PlacementValidator.CanBotPlaceHere(allBoardsLogic[pIndex], rows, cols, r, c, color, groupId, number, GameManager.Instance.players[pIndex], GameManager.Instance.currentVariant);
    }

    public bool IsValidPlacement(int pIndex, int r, int c, DieColor color, int currentGroupId, int number)
    {
        return PlacementValidator.IsValidPlacement(allBoardsLogic[pIndex], rows, cols, r, c, color, currentGroupId, number, GameManager.Instance.players[pIndex], GameManager.Instance.currentVariant);
    }

    // --- DELEGATION TO TOPOLOGY CALCULATOR ---
    public int CalculateGapPenalty(int pIndex, bool showPopups = false)
    {
        if (!IsValidPlayerIndex(pIndex)) return 0;
        return TopologyCalculator.CalculateGapPenalty(allBoardsLogic[pIndex], rows, cols, out _);
    }

    public System.Collections.IEnumerator AnimateGapPenaltiesFlow(int pIndex, System.Action onComplete)
    {
        if (!IsValidPlayerIndex(pIndex)) yield break;

        TopologyCalculator.CalculateGapPenalty(allBoardsLogic[pIndex], rows, cols, out List<List<Vector2Int>> enclosedGaps);
        bool foundAnyGap = enclosedGaps.Count > 0;

        foreach (var gapCells in enclosedGaps)
        {
            int penalty = TopologyCalculator.GetPenaltyForGapSize(gapCells.Count);

            foreach (Vector2Int cell in gapCells)
            {
                CellComponent cellVisual = allCellsVisual[pIndex][cell.x, cell.y];
                if (cellVisual != null) cellVisual.HighlightGapColor();
            }

            Vector2Int centerCell = gapCells[gapCells.Count / 2];
            Vector3 popupPos = allCellsVisual[pIndex][centerCell.x, centerCell.y].transform.position;
            PopUpManager.Instance.ShowPopUp(popupPos, $"{penalty}", Color.red);

            yield return new WaitForSeconds(0.5f);
        }

        if (foundAnyGap) yield return new WaitForSeconds(0.5f);
        onComplete?.Invoke();
    }

    // --- DELEGATION TO SCORE CALCULATOR ---
    public int EvaluateAndApplyCombos(int pIndex)
    {
        return ScoreCalculator.EvaluateAndApplyCombos(allBoardsLogic[pIndex], rows, cols, GameManager.Instance.players[pIndex]);
    }

    public int Count3x3Contacts(int pIndex, int r, int c, int valorDado, out int contactosDiagonales)
    {
        return ScoreCalculator.Count3x3Contacts(allBoardsLogic[pIndex], rows, cols, r, c, valorDado, out contactosDiagonales);
    }

    public int ScanNewDiagonalConnections(int pIndex, int r, int c, DieColor color, int groupId)
    {
        return ScoreCalculator.ScanNewDiagonalConnections(allBoardsLogic[pIndex], rows, cols, r, c, color, groupId);
    }

    public int GetOnesPenalties(int pIndex)
    {
        if (!IsValidPlayerIndex(pIndex)) return 0;
        return ScoreCalculator.GetOnesPenalties(allBoardsLogic[pIndex], rows, cols);
    }

    // --- VISUAL & INTERNAL LOGIC ---
    public void ShowValidMoves(int pIndex, DieColor color, int groupId, int targetSize)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool isValid = false;
                if (allBoardsLogic[pIndex][r, c] == null)
                {
                    isValid = IsValidPlacement(pIndex, r, c, color, groupId, targetSize);
                }
                allCellsVisual[pIndex][r, c].SetHighlight(isValid);
            }
        }
    }

    public void ClearHighlights(int pIndex)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (allCellsVisual[pIndex][r, c] != null)
                {
                    allCellsVisual[pIndex][r, c].SetHighlight(false);
                }
            }
        }
    }

    public void CommitDieToLogic(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        DieData[,] currentLogic = allBoardsLogic[pIndex];
        currentLogic[r, c] = new DieData(color, groupId, number);

        InstantiateDieVisual(pIndex, r, c, color, number);
    }

    public void RemoveDie(int pIndex, int r, int c)
    {
        allBoardsLogic[pIndex][r, c] = null;
        if (allCellsVisual[pIndex][r, c] != null)
        {
            // The visual die is instantiated as a child of the board root, but its position corresponds to r,c.
            // Wait, we don't keep a direct reference to the visual die GameObject in the grid logic. 
            // We should find it by position or keep track of it, or the CellComponent can destroy its child.
            // Let's iterate through the boardRoot's children and destroy the one at the cell's position.
            Vector3 pos = GetWorldPosition(pIndex, r, c);
            foreach (Transform child in boardRoots[pIndex].transform)
            {
                // Skip the cells themselves (they might be at Z=0). Dice are at Z=-2
                if (Mathf.Abs(child.position.x - pos.x) < 0.1f && Mathf.Abs(child.position.y - pos.y) < 0.1f && child.position.z < -1f)
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }
    }

    public DieData[,] GetBoardLogic(int pIndex)
    {
        return allBoardsLogic[pIndex];
    }

    private void InstantiateDieVisual(int pIndex, int r, int c, DieColor color, int number)
    {
        GameObject prefabAUsar = GetPrefabByColor(color);
        Vector3 position = new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2);

        GameObject nuevoDado = Instantiate(prefabAUsar, position, Quaternion.identity, boardRoots[pIndex].transform);
        nuevoDado.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        SpriteRenderer renderer = nuevoDado.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = UIManager.Instance.GetSprite(color, number);
    }

    private GameObject GetPrefabByColor(DieColor color)
    {
        if (prefabDict.TryGetValue(color, out GameObject prefab))
        {
            return prefab;
        }
        return whitePrefab; 
    }

    public Vector3 GetWorldPosition(int pIndex, int r, int c)
    {
        return new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2);
    }

    public void MarkGroupAsCompleted(int pIndex, List<Vector2Int> cells)
    {
        if (pIndex < 0 || pIndex >= allCellsVisual.Count) return;

        foreach (var cellPos in cells)
        {
            CellComponent cellComp = allCellsVisual[pIndex][cellPos.x, cellPos.y];
            if (cellComp != null)
            {
                cellComp.ToggleCompletionSprite(true);
            }
        }
    }
}