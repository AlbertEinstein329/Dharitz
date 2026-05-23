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

    public List<DieData[,]> allBoardsLogic;
    public List<CellComponent[,]> allCellsVisual;
    private GameObject[] boardRoots;
    private int currentlyViewedPlayer = 0;
    public int CurrentlyViewedPlayer => currentlyViewedPlayer;
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
                    // Mantenemos la configuración vital para que el multijugador sepa quién es el dueño
                    cellScript.Setup(r, c, playerIndex, this, GameManager.Instance, GameManager.Instance);
                    cellScript.gridCoordinate = new Vector2Int(c, r);
                    allCellsVisual[playerIndex][r, c] = cellScript;
                }
            }
        }
    }

    public void SwitchViewTo(int playerIndex)
    {
        // Validación de seguridad para no salirnos de los límites del array
        if (playerIndex < 0 || playerIndex >= boardRoots.Length) return;

        currentlyViewedPlayer = playerIndex; // Actualizamos el índice de seguimiento

        for (int i = 0; i < boardRoots.Length; i++)
        {
            // Activamos solo el tablero que queremos ver
            boardRoots[i].SetActive(i == playerIndex);
        }

        // =========================================================================
        // SINCRONIZACIÓN DE UI: Obligamos al HUD a mostrar los datos del tablero actual
        // =========================================================================
        if (GameManager.Instance != null && GameManager.Instance.players != null)
        {
            PlayerData p = GameManager.Instance.players[playerIndex];

            if (UIManager.Instance != null)
            {
                // Actualizamos el score en pantalla
                UIManager.Instance.UpdateScore(p.score);

                // (Opcional): Si tienes una función para mostrar el nombre del jugador, 
                // el avatar, o sus monedas, llámala también aquí. Por ejemplo:
                // UIManager.Instance.UpdatePlayerName(p.playerName);
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
        int next = (currentlyViewedPlayer + 1) % boardRoots.Length;
        SwitchViewTo(next);
    }

    public void ViewPreviousBoard()
    {
        int prev = (currentlyViewedPlayer - 1 + boardRoots.Length) % boardRoots.Length;
        SwitchViewTo(prev);
    }

    // --- DELEGATION TO PLACEMENT VALIDATOR ---
    public bool TryPlaceDie(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {

        if (!PlacementValidator.CanBotPlaceHere(allBoardsLogic[pIndex], rows, cols, r, c, color, groupId, number, GameManager.Instance.players[pIndex], GameManager.Instance.currentVariant))
            return false;

        BoardLogic boardLogic = FindFirstObjectByType<BoardLogic>();
        if (boardLogic != null)
        {
            boardLogic.occupiedCells.Add(new Vector2Int(c, r));
        }
        else
        {
            Debug.LogError("[Arquitectura] GridManager no pudo encontrar BoardLogic para sincronizar el dado.");
        }


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


        BoardLogic boardLogic = FindFirstObjectByType<BoardLogic>();
        if (boardLogic != null)
        {
            boardLogic.occupiedCells.Add(new Vector2Int(c, r));
        }
        else
        {
            Debug.LogError("[Arquitectura] GridManager no pudo encontrar BoardLogic para sincronizar el dado.");
        }

        if (GridInteractionManager.Instance != null)
        {
            GridInteractionManager.Instance.ClearBoardHighlights();
        }

        // TAREA DE ARQUITECTURA: Si tienes un sistema que ilumina el tablero para el 
        // SIGUIENTE dado en la mano, debes llamarlo justo aquí, DESPUÉS de apagar las luces.
    
    }

    public void RemoveDie(int pIndex, int r, int c)
    {
        // 1. Limpieza lógica
        allBoardsLogic[pIndex][r, c] = null;

        BoardLogic boardLogic = FindFirstObjectByType<BoardLogic>();
        if (boardLogic != null)
        {
            boardLogic.occupiedCells.Remove(new Vector2Int(c, r));
        }

        // 2. SOLUCIÓN VISUAL: Búsqueda por Identidad en lugar de Posición Flotante
        foreach (Transform child in boardRoots[pIndex].transform)
        {
            CellComponent dieComp = child.GetComponent<CellComponent>();

            // Verificamos que el objeto tenga el componente, que sus coordenadas coincidan 
            // exactamente con lo que queremos borrar, y que sea un dado (z < -1f)
            if (dieComp != null && dieComp.gridCoordinate.x == c && dieComp.gridCoordinate.y == r && child.position.z < -1f)
            {
                Destroy(child.gameObject);
                break; // Encontramos el dado correcto, lo destruimos y salimos del bucle
            }
        }

        // 3. Forzamos actualización visual de luces
        if (GridInteractionManager.Instance != null)
        {
            GridInteractionManager.Instance.ClearBoardHighlights();
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

        // (Si el dado usa otro script, cambia "CellComponent" por el nombre de tu script).
        CellComponent dieScript = nuevoDado.GetComponent<CellComponent>();
        if (dieScript != null)
        {
            dieScript.gridCoordinate = new Vector2Int(c, r);

            dieScript.Setup(r, c, pIndex, this, GameManager.Instance, GameManager.Instance);
        }
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