using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Dynamic Grid Settings")]
    public float screenPadding = 0.5f; // Padding en unidades del mundo
    public float bottomUIOffset = 1.5f; // Espacio que reservamos abajo para los botones

    [Header("Responsive Grid Positioning")]
    [SerializeField] private float bottomVerticalOffset = 1.5f;

    [Header("Prefabs y Referencias")]
    public GameObject cellPrefab;
    public GameObject redPrefab, bluePrefab, whitePrefab, blackPrefab;

    private Dictionary<DieColor, GameObject> prefabDict;

    public List<DieData[,]> allBoardsLogic;
    public List<CellComponent[,]> allCellsVisual;
    private GameObject[] boardRoots;
    private int currentlyViewedPlayer = 0;
    public int CurrentlyViewedPlayer => currentlyViewedPlayer;

    [Header("Animation References")]
    [Tooltip("Drag your Draw Button / UI Bag here from the Canvas")]
    public RectTransform drawButtonUI;
    private bool isAnimatingPlacement = false;

    [Header("AAA Highlight System")]
    [Tooltip("Drag your PatternLightFX Prefab here")]
    public GameObject patternLightPrefab;

    [Tooltip("How much the dice zoom into the camera (1.10 = 10% bigger)")]
    public float highlightZoomMultiplier = 1.10f;

    [Tooltip("How fast the light spins behind the dice")]
    public float lightRotationSpeed = 3f;


    public float dragScaleMultiplier = 1.15f;
    //private bool isDraggingDie = false;

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
        CalculateDynamicGridSize();

        //startX = -((cols - 1) * cellSize) / 2f;
        //startY = -((rows - 1) * cellSize) / 2f;

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

    private void CalculateDynamicGridSize()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found! Cannot calculate dynamic grid size.");
            return;
        }

        // 1. Get screen dimensions in world units based on orthographic size
        float screenHeight = mainCamera.orthographicSize * 2f;
        float screenWidth = screenHeight * mainCamera.aspect;

        // 2. Calculate cell size based on screen width and padding
        float availableWidth = screenWidth - (screenPadding * 2f);
        cellSize = availableWidth / cols;

        // 3. Align horizontally (Centered with left/right padding)
        startX = (-screenWidth / 2f) + screenPadding + (cellSize / 2f);

        // 4. Align vertically (Anchored strictly to the bottom edge of the camera view)
        startY = -mainCamera.orthographicSize + bottomVerticalOffset + (cellSize / 2f);
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

        GameObject NewDice = Instantiate(prefabAUsar, position, Quaternion.identity, boardRoots[pIndex].transform);
        NewDice.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        SpriteRenderer renderer = NewDice.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = UIManager.Instance.GetSprite(color, number);

        // (Si el dado usa otro script, cambia "CellComponent" por el nombre de tu script).
        CellComponent dieScript = NewDice.GetComponent<CellComponent>();
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

        HighlightCompletedPattern(pIndex, cells);

        foreach (var cellPos in cells)
        {
            CellComponent cellComp = allCellsVisual[pIndex][cellPos.x, cellPos.y];
            if (cellComp != null)
            {
                cellComp.ToggleCompletionSprite(true);
            }
        }
    }

    // --------------------------------------------------------
    // DRAG AND DROP ELASTIC SYSTEM
    // --------------------------------------------------------

    public void AnimateAndPlaceDie(int row, int col, DieColor color, int number, Vector3 targetCellPos, IPlacementExecutor executor)
    {
        // 1. Prevent spam-clicking if a die is already flying
        if (isAnimatingPlacement) return;
        isAnimatingPlacement = true;

        Vector3 startWorldPos = Vector3.zero;
        CanvasGroup buttonCanvasGroup = null;

        // 2. Hide the UI Button and calculate its exact screen position
        if (drawButtonUI != null)
        {
            // Use CanvasGroup to hide the UI without deactivating the GameObject
            buttonCanvasGroup = drawButtonUI.GetComponent<CanvasGroup>();
            if (buttonCanvasGroup == null) buttonCanvasGroup = drawButtonUI.gameObject.AddComponent<CanvasGroup>();

            buttonCanvasGroup.alpha = 0f; // Make UI invisible!
            buttonCanvasGroup.blocksRaycasts = false; // Prevent accidental UI clicks

            // Convert UI Canvas position to 2D World Space
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, drawButtonUI.position);
            startWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Camera.main.nearClipPlane));
        }

        startWorldPos.z = -9f; // Force it extremely close to the camera

        // 3. Create the temporary flying die
        GameObject flyingDie = Instantiate(GetPrefabByColor(color), startWorldPos, Quaternion.identity);

        Collider2D col2D = flyingDie.GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = false;

        float scaleFactor = cellSize;
        SpriteRenderer renderer = flyingDie.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = UIManager.Instance.GetSprite(color, number);
            renderer.sortingOrder = 32000;

            if (renderer.sprite != null)
            {
                scaleFactor = cellSize / renderer.sprite.bounds.size.x;
            }
        }
        flyingDie.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

        // 4. Trigger DOTween Animation
        flyingDie.transform.DOMove(targetCellPos, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            flyingDie.transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.15f, 5, 1f).OnComplete(() =>
            {
                // Execute the actual placement logic and destroy the fake flying die
                executor.BeginPlacement(row, col);
                Destroy(flyingDie);

                // 5. Unhide the UI Button for the next turn!
                if (buttonCanvasGroup != null)
                {
                    buttonCanvasGroup.alpha = 1f;
                    buttonCanvasGroup.blocksRaycasts = true;
                }

                // Unlock the board for the next interaction
                isAnimatingPlacement = false;
            });
        });
    }

    // --------------------------------------------------------
    // AAA CELEBRATION SYSTEM
    // --------------------------------------------------------

    public void HighlightCompletedPattern(int playerIndex, List<Vector2Int> patternCoordinates)
    {
        if (patternCoordinates == null || patternCoordinates.Count == 0) return;

        // Obtenemos la raíz donde viven todos los objetos del tablero de este jugador
        Transform boardRoot = boardRoots[playerIndex].transform;

        foreach (Vector2Int coord in patternCoordinates)
        {
            // 1. Obtenemos la celda de fondo
            CellComponent visualCell = allCellsVisual[playerIndex][coord.x, coord.y];
            if (visualCell == null) continue;

            // 2. 🛠️ RADAR ESPACIAL: Buscamos el dado independiente que está sobre esta celda
            Transform dieTransform = null;
            SpriteRenderer dieRenderer = null;

            foreach (Transform child in boardRoot)
            {
                // Ignoramos la propia celda de fondo
                if (child.gameObject == visualCell.gameObject) continue;

                // Medimos la distancia 2D (ignorando el eje Z) entre el objeto y la celda
                float distance = Vector2.Distance(child.position, visualCell.transform.position);

                // Si están a menos de 0.1 unidades, ¡hemos encontrado el dado que está encima!
                if (distance < 0.1f)
                {
                    SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        dieTransform = child;
                        dieRenderer = sr;
                        break;
                    }
                }
            }

            // Si por alguna razón no hay dado en esa celda, saltamos la animación
            if (dieTransform == null || dieRenderer == null) continue;

            // 3. ANIMAMOS EXCLUSIVAMENTE EL DADO
            int originalSortingOrder = dieRenderer.sortingOrder;
            dieRenderer.sortingOrder = 500; // Al frente de todo

            Vector3 originalPos = dieTransform.position;
            Vector3 originalScale = dieTransform.localScale;
            Vector3 targetScale = originalScale * highlightZoomMultiplier;

            Sequence popSequence = DOTween.Sequence();

            // Fase de salida
            popSequence.Append(dieTransform.DOMoveZ(-4f, 0.6f).SetEase(Ease.OutQuad));
            popSequence.Join(dieTransform.DOScale(targetScale, 0.6f).SetEase(Ease.OutBack, 0.8f));

            // Pausa en el aire
            popSequence.AppendInterval(1.2f);

            // Fase de retorno
            popSequence.Append(dieTransform.DOMoveZ(originalPos.z, 0.5f).SetEase(Ease.InOutSine));
            popSequence.Join(dieTransform.DOScale(originalScale, 0.5f).SetEase(Ease.InOutSine));

            popSequence.OnComplete(() =>
            {
                if (dieRenderer != null) dieRenderer.sortingOrder = originalSortingOrder;
            });

            // 4. SPAWN AND CLEAN UP THE LIGHT FX
            if (patternLightPrefab != null)
            {
                Vector3 lightPos = new Vector3(visualCell.transform.position.x, visualCell.transform.position.y, -3f);
                GameObject lightFX = Instantiate(patternLightPrefab, lightPos, Quaternion.identity, visualCell.transform);

                lightFX.transform.localScale = Vector3.zero;
                float targetLightScale = cellSize * 1.2f;

                SpriteRenderer lightRenderer = lightFX.GetComponent<SpriteRenderer>();
                if (lightRenderer != null)
                {
                    lightRenderer.sortingOrder = 450;

                    //Añadido .SetLink(lightFX) para limpiar el fade al destruirse
                    lightRenderer.DOFade(0.4f, 0.8f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetLink(lightFX);

                    lightRenderer.DOFade(0f, 0.4f).SetDelay(1.9f).SetLink(lightFX);
                }

                //Añadido .SetLink(lightFX) a la escala
                lightFX.transform.DOScale(new Vector3(targetLightScale, targetLightScale, 1f), 0.6f)
                    .SetEase(Ease.OutBack, 0.8f)
                    .SetLink(lightFX);

                //Añadido .SetLink(lightFX) a la rotación infinita
                lightFX.transform.DORotate(new Vector3(0, 0, -360), lightRotationSpeed, RotateMode.FastBeyond360)
                    .SetLoops(-1, LoopType.Restart)
                    .SetEase(Ease.Linear)
                    .SetLink(lightFX);

                // El objeto se destruye limpiamente en 2.3 segundos sin dejar procesos huérfanos
                Destroy(lightFX, 2.3f);
            }
        }
    }

}