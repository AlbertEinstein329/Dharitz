using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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

    // --- SISTEMA MULTITABLERO ---
    private List<DieData[,]> allBoardsLogic;
    private List<CellComponent[,]> allCellsVisual;
    private GameObject[] boardRoots; // Los objetos "Padre" de cada tablero
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
            allCellsVisual.Add(new CellComponent[rows, cols]); // <-- Añade esto

            boardRoots[p] = new GameObject($"Tablero_Jugador_{p + 1}");
            boardRoots[p].transform.SetParent(this.transform);

            GenerateGridForPlayer(p);
        }

        // Al iniciar, mostramos el tablero del jugador 1
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
                    // INYECCIÓN DE DEPENDENCIAS (Dependency Injection)
                    // Pasa las interfaces (this como IGridValidator, GameManager como ITurnProvider y IPlacementExecutor)
                    cellScript.Setup(r, c, playerIndex, this, GameManager.Instance, GameManager.Instance);
                    allCellsVisual[playerIndex][r, c] = cellScript;
                }
            }
        }
    }

    // --- FUNCIONES DE CÁMARA / VISTA ---
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
            // --- NUEVO: Sincronizar el HUD de puntos al instante en tiempo real ---
            if (!GameManager.Instance.isGameOver && GameManager.Instance.players.Count > playerIndex)
            {
                int scoreDelJugador = GameManager.Instance.players[playerIndex].score;
                UIManager.Instance.UpdateScore(scoreDelJugador);
            }

            // Si el juego ya terminó, actualiza el panel de puntajes finales
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

    // --- LÓGICA MULTIJUGADOR ---
    public bool TryPlaceDie(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        if (r < 0 || r >= rows || c < 0 || c >= cols) return false;

        DieData[,] currentLogic = allBoardsLogic[pIndex];

        if (currentLogic[r, c] != null) return false;
        if (!IsValidPlacement(pIndex, r, c, color, groupId, number)) return false;

        InstantiateDieVisual(pIndex, r, c, color, number);

        currentLogic[r, c] = new DieData(color, groupId, number);
        return true;
    }

    public bool CanBotPlaceHere(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        if (r < 0 || r >= rows || c < 0 || c >= cols) return false;
        if (allBoardsLogic[pIndex][r, c] != null) return false;
        return IsValidPlacement(pIndex, r, c, color, groupId, number);
    }

    // Se requieren los 6 argumentos, incluyendo pIndex
    private bool IsValidPlacement(int pIndex, int r, int c, DieColor color, int currentGroupId, int number)
    {
        DieData[,] currentLogic = allBoardsLogic[pIndex];
        PlayerData player = GameManager.Instance.players[pIndex];
        bool isBoardEmpty = (player.placedDice == 0);

        // --- RULES INJECTION ---
        VariantData variante = GameManager.Instance.currentVariant;
        PatternData currentPattern = variante.GetPattern(number);

        bool hasDiceInGroup = false;
        if (player.activeGroups.ContainsKey(color) && player.activeGroups[color] != null)
        {
            hasDiceInGroup = player.activeGroups[color].occupiedCells.Count > 0;
        }

        bool touchesOwnGroup = false;
        bool touchesAnyDie = false;

        // Escaneo 3x3
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;

                int nr = r + i;
                int nc = c + j;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    DieData neighbor = currentLogic[nr, nc];

                    if (neighbor != null)
                    {
                        // 1. REGLAS DEL NÚMERO 1 DINÁMICAS
                        if (number == 1 && neighbor.value == 1)
                        {
                            // Si estamos jugando con la regla clásica (Variante 1), bloqueamos mismo color.
                            // Si es Variante 2 o 3, permitimos el contacto físico para que luego sume puntos extra.
                            if (currentPattern.specialRule == SpecialRule.PenalizeOnContact)
                            {
                                if (neighbor.color == color) return false;
                            }
                        }

                        // 2. CONECTIVIDAD DEL GRUPO
                        if (neighbor.groupId == currentGroupId)
                        {
                            touchesOwnGroup = true;
                        }

                        bool esOrtogonal = (i == 0 || j == 0);

                        if (esOrtogonal)
                        {
                            touchesAnyDie = true;
                            // Prohibición estricta de tocar otro grupo del mismo color ortogonalmente
                            if (neighbor.color == color && neighbor.groupId != currentGroupId)
                                return false;
                        }
                        else
                        {
                            // Contacto diagonal válido SOLO si pertenece a tu misma agrupación
                            if (neighbor.groupId == currentGroupId)
                            {
                                touchesAnyDie = true;
                            }
                        }
                    }
                }
            }
        }

        // Resoluciones
        if (isBoardEmpty) return true;
        if (hasDiceInGroup && !touchesOwnGroup) return false; // Obliga a seguir el patrón
        if (!touchesAnyDie) return false;

        // --- SISTEMA DE SUPERVIVENCIA ---
        return ValidateSurvival(pIndex, r, c, color, currentGroupId, number, player);
    }

    private GameObject GetPrefabByColor(DieColor color)
    {
        if (prefabDict.TryGetValue(color, out GameObject prefab))
        {
            return prefab;
        }
        return whitePrefab; // Fallback
    }

    private System.Collections.Generic.List<System.Collections.Generic.List<Vector2Int>> FindEnclosedGaps(int pIndex)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        bool[,] visited = new bool[rows, cols];
        var enclosedGaps = new System.Collections.Generic.List<System.Collections.Generic.List<Vector2Int>>();

        Vector2Int[] orthogonalDirections = new Vector2Int[] {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (logic[r, c] == null && !visited[r, c])
                {
                    bool isEnclosed = true;
                    var gapCells = new System.Collections.Generic.List<Vector2Int>();
                    System.Collections.Generic.Queue<Vector2Int> queue = new System.Collections.Generic.Queue<Vector2Int>();

                    queue.Enqueue(new Vector2Int(r, c));
                    visited[r, c] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        gapCells.Add(current);

                        foreach (Vector2Int dir in orthogonalDirections)
                        {
                            int nextR = current.x + dir.x;
                            int nextC = current.y + dir.y;

                            if (nextR < 0 || nextR >= rows || nextC < 0 || nextC >= cols)
                            {
                                isEnclosed = false;
                            }
                            else if (logic[nextR, nextC] == null && !visited[nextR, nextC])
                            {
                                visited[nextR, nextC] = true;
                                queue.Enqueue(new Vector2Int(nextR, nextC));
                            }
                        }
                    }

                    if (isEnclosed && gapCells.Count > 0)
                    {
                        enclosedGaps.Add(gapCells);
                    }
                }
            }
        }

        return enclosedGaps;
    }

    /// <summary>
    /// Evaluates enclosed gaps using a 4-way Orthogonal Flood Fill.
    /// Diagonals are ignored. Escaping to the board edges means the gap is NOT enclosed.
    /// </summary>
    public int CalculateGapPenalty(int pIndex, bool showPopups = false)
    {
        if (!IsValidPlayerIndex(pIndex)) return 0;
        int totalPenalty = 0;
        var enclosedGaps = FindEnclosedGaps(pIndex);

        foreach (var gapCells in enclosedGaps)
        {
            totalPenalty += GetPenaltyForGapSize(gapCells.Count);
        }

        return totalPenalty;
    }

    private int GetPenaltyForGapSize(int size)
    {
        if (size == 1) return -500;
        if (size == 2) return -700;
        if (size == 3) return -1000;

        return -(250 + ((size - 1) * 100)); // Si se penaliza por agrupacion
    }

    /// <summary>
    /// Animates the gap penalties sequentially. Colors cells red, spawns popups every 0.5s, 
    /// and triggers a callback when completely finished.
    /// </summary>
    public System.Collections.IEnumerator AnimateGapPenaltiesFlow(int pIndex, System.Action onComplete)
    {
        if (!IsValidPlayerIndex(pIndex)) yield break;
        var enclosedGaps = FindEnclosedGaps(pIndex);
        bool foundAnyGap = enclosedGaps.Count > 0;

        foreach (var gapCells in enclosedGaps)
        {
            int penalty = GetPenaltyForGapSize(gapCells.Count);

            // 1. Pintar todas las casillas del hueco en ROJO
            foreach (Vector2Int cell in gapCells)
            {
                CellComponent cellVisual = allCellsVisual[pIndex][cell.x, cell.y];
                if (cellVisual != null) cellVisual.HighlightGapColor();
            }

            // 2. Mostrar el PopUp en el centro del hueco
            Vector2Int centerCell = gapCells[gapCells.Count / 2];
            Vector3 popupPos = allCellsVisual[pIndex][centerCell.x, centerCell.y].transform.position;
            PopUpManager.Instance.ShowPopUp(popupPos, "${penalty}", Color.red);

            // 3. Esperar 0.5 segundos ANTES de buscar el siguiente hueco
            yield return new WaitForSeconds(0.5f);
        }

        // Si se mostro al menos un hueco, esperamos 0.5s extra antes de mostrar el panel final
        if (foundAnyGap) yield return new WaitForSeconds(0.5f);

        // Finalizamos la secuencia llamando al GameManager
        onComplete?.Invoke();
    }



    // Escanea el tablero del jugador y aplica -1 por cada dado de valor 1 que toque a otro 1
    public int GetOnesPenalties(int pIndex)
    {
        if (!IsValidPlayerIndex(pIndex)) return 0;
        // --- VARIANT INJECTION ---
        VariantData variante = GameManager.Instance.currentVariant;
        PatternData patron1 = variante.GetPattern(1);

        // Si el patrón 1 tiene la regla de premiar el contacto, ANULAMOS la penalización.
        if (patron1 != null && patron1.specialRule == SpecialRule.RewardOnContact)
        {
            return 0; // Se salva de la multa
        }

        
        DieData[,] logic = allBoardsLogic[pIndex];
        int penalizacionTotal = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                DieData currentDie = logic[r, c];

                // Si la celda tiene un dado y es un 1
                if (currentDie != null && currentDie.value == 1)
                {
                    bool tocaOtroUno = false;

                    // Revisar los 8 vecinos (3x3)
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (i == 0 && j == 0) continue;
                            int nr = r + i;
                            int nc = c + j;

                            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                            {
                                DieData neighbor = logic[nr, nc];
                                if (neighbor != null && neighbor.value == 1)
                                {
                                    tocaOtroUno = true;
                                    break;
                                }
                            }
                        }
                        if (tocaOtroUno) break; // Ya encontramos uno, no hace falta seguir buscando para este dado
                    }

                    // Si este dado de 1 toca al menos a otro dado de 1, recibe la penalización
                    if (tocaOtroUno)
                    {
                        penalizacionTotal += 1;
                    }
                }
            }
        }
        return penalizacionTotal;
    }

    private bool ValidateSurvival(int pIndex, int r, int c, DieColor color, int newGroupId, int newTargetSize, PlayerData player)
    {
        // FAST-EXIT FÍSICO. Escaneamos la matriz real del jugador.
        // Si no hay ningún dado de plástico en su tablero, es imposible que se encierre.
        bool tableroVacio = true;
        foreach (DieData dado in allBoardsLogic[pIndex])
        {
            if (dado != null) { tableroVacio = false; break; }
        }

        if (tableroVacio) return true;


        DieData[,] logic = allBoardsLogic[pIndex];

        // 1. MAPEO DE NECESIDADES: ¿Cuántos dados le faltan a cada grupo del tablero?
        Dictionary<int, int> dadosFaltantes = new Dictionary<int, int>();
        foreach (var group in player.activeGroups.Values)
        {
            if (group != null && !group.isClosed)
            {
                dadosFaltantes[group.id] = group.targetSize - group.occupiedCells.Count;
            }
        }

        // 2. SIMULACIÓN DE LA JUGADA
        logic[r, c] = new DieData(color, newGroupId, newTargetSize);

        // Ajustamos las necesidades basándonos en la simulación
        if (dadosFaltantes.ContainsKey(newGroupId))
        {
            dadosFaltantes[newGroupId] -= 1;
            if (dadosFaltantes[newGroupId] <= 0) dadosFaltantes.Remove(newGroupId);
        }
        else
        {
            // Es un grupo nuevo
            if (newTargetSize - 1 > 0)
            {
                dadosFaltantes[newGroupId] = newTargetSize - 1;
            }
        }

        // 3. ANÁLISIS DE TOPOLOGÍA GLOBAL
        bool esValido = AnalyzeTopology(pIndex, logic, dadosFaltantes, player); // <-- Añadimos 'player' al final

        // 4. REVERSIÓN DE LA SIMULACIÓN
        logic[r, c] = null;

        return esValido;
    }

    private bool AnalyzeTopology(int pIndex, DieData[,] logic, Dictionary<int, int> dadosFaltantes, PlayerData player)
    {
        VariantData variante = GameManager.Instance.currentVariant;

        // Array de 8 direcciones (Primeros 4 ortogonales, últimos 4 diagonales)
        int[] dr = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] dc = { 0, 0, -1, 1, -1, 1, -1, 1 };

        foreach (var kvp in dadosFaltantes)
        {
            int gId = kvp.Key;
            int requeridos = kvp.Value;

            if (requeridos <= 0) continue;

            // Identificar qué patrón estamos evaluando para saber cómo busca espacio
            GroupData grupoActivo = null;
            foreach (var g in player.activeGroups.Values) { if (g != null && g.id == gId) { grupoActivo = g; break; } }

            // --- BLINDAJE CONTRA NULOS (Grupo Hipotético) ---
            int sizeDelPatron = (grupoActivo != null) ? grupoActivo.targetSize : (requeridos + 1);

            PatternData patronDelGrupo = variante.GetPattern(sizeDelPatron);

            // Si el Inspector de Unity está incompleto, abortamos limpiamente sin crashear.
            if (patronDelGrupo == null)
            {
                Debug.LogError($"CRÍTICO: El juego intentó leer el patrón {sizeDelPatron}, pero NO EXISTE en la Variante '{variante.variantName}'. ¡Revisa tu ScriptableObject en el Inspector de Unity y asegúrate de asignar los 6 patrones!");
                return false;
            }

            // ¿Tiene permiso este grupo para "saltar" bloqueos en diagonal?
            bool puedeReservarDiagonal = patronDelGrupo.allowDiagonalReservation;
            int direccionesDeBusqueda = puedeReservarDiagonal ? 8 : 4; // Cambia la potencia del escáner

            int vaciosAlcanzables = 0;
            bool[,] visitado = new bool[rows, cols];
            Queue<Vector2Int> cola = new Queue<Vector2Int>();

            // Si el grupo es nuevo (null), atraparemos su color real leyendo el tablero hipotético más abajo
            DieColor colorDelGrupo = (grupoActivo != null) ? grupoActivo.color : DieColor.White;

            // 1. Encontrar todos los dados de ESTE grupo
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (logic[r, c] != null && logic[r, c].groupId == gId)
                    {
                        // Si era un grupo hipotético, aquí descubrimos de qué color era
                        colorDelGrupo = logic[r, c].color;

                        cola.Enqueue(new Vector2Int(r, c));
                        visitado[r, c] = true;
                    }
                }
            }

            // ... (A partir de aquí, el paso "2. Expandir el Flood-Fill" se mantiene exactamente igual que tu código original)

            // 2. Expandir el Flood-Fill con detección de colisión
            while (cola.Count > 0)
            {
                Vector2Int actual = cola.Dequeue();

                // Usamos 4 u 8 direcciones según las reglas del patrón
                for (int d = 0; d < direccionesDeBusqueda; d++)
                {
                    int nr = actual.x + dr[d];
                    int nc = actual.y + dc[d];

                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        if (logic[nr, nc] == null && !visitado[nr, nc])
                        {
                            // ZONA MUERTA: Verificamos los vecinos ortogonales del espacio vacío
                            bool esZonaMuerta = false;
                            for (int d2 = 0; d2 < 4; d2++) // Siempre 4 direcciones para zona muerta
                            {
                                int nnr = nr + dr[d2];
                                int nnc = nc + dc[d2];

                                if (nnr >= 0 && nnr < rows && nnc >= 0 && nnc < cols)
                                {
                                    DieData vecinoDelVacio = logic[nnr, nnc];
                                    if (vecinoDelVacio != null && vecinoDelVacio.color == colorDelGrupo && vecinoDelVacio.groupId != gId)
                                    {
                                        esZonaMuerta = true;
                                        break;
                                    }
                                }
                            }

                            if (!esZonaMuerta)
                            {
                                visitado[nr, nc] = true;
                                vaciosAlcanzables++;
                                cola.Enqueue(new Vector2Int(nr, nc));
                            }
                        }
                    }
                }
            }

            // 3. Veredicto Final
            if (vaciosAlcanzables < requeridos)
            {
                Debug.Log($"Bloqueo Topológico: El grupo {gId} necesita {requeridos} espacios, pero solo alcanza {vaciosAlcanzables} bajo reglas de {(puedeReservarDiagonal ? "búsqueda de 8 vías" : "búsqueda estricta de 4 vías")}.");
                return false;
            }
        }

        return true;
    }

    // Evalúa todo el tablero y resalta las celdas válidas
    public void ShowValidMoves(int pIndex, DieColor color, int groupId, int targetSize)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool isValid = false;

                // Solo revisamos si la celda está vacía
                if (allBoardsLogic[pIndex][r, c] == null)
                {
                    isValid = IsValidPlacement(pIndex, r, c, color, groupId, targetSize);
                }

                allCellsVisual[pIndex][r, c].SetHighlight(isValid);
            }
        }
    }

    // Apaga el resaltado de todas las celdas
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

    public int CalculateVariantBonuses(int pIndex, out string desgloseBonos)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        VariantData variante = GameManager.Instance.currentVariant;

        int totalBono = 0;
        desgloseBonos = "";

        int bonoUnos = 0;
        int contactosDiagonalesValidos = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                DieData dado = logic[r, c];
                if (dado == null) continue;

                PatternData patron = variante.GetPattern(dado.value);
                if (patron == null) continue;

                // --- REGLA: Contacto Diagonal Extra (Variante 1 para el 2) ---
                if (patron.specialRule == SpecialRule.ExtraDiagonalContact)
                {
                    // Escaneamos solo las 4 diagonales
                    int[] dr = { -1, -1, 1, 1 };
                    int[] dc = { -1, 1, -1, 1 };

                    for (int d = 0; d < 4; d++)
                    {
                        int nr = r + dr[d];
                        int nc = c + dc[d];
                        if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                        {
                            DieData vecino = logic[nr, nc];
                            // Si toca a uno igual, de su mismo color pero de OTRA familia...
                            if (vecino != null && vecino.value == dado.value && vecino.color == dado.color && vecino.groupId != dado.groupId)
                            {
                                contactosDiagonalesValidos++;
                            }
                        }
                    }
                }

                // --- REGLA: Aislamiento Premiado (Variante 3 para el 1) ---
                if (patron.specialRule == SpecialRule.RewardOnContact)
                {
                    bool tocaOtroUno = false;
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (i == 0 && j == 0) continue;
                            int nr = r + i, nc = c + j;
                            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                            {
                                DieData vecino = logic[nr, nc];
                                if (vecino != null && vecino.value == 1) tocaOtroUno = true;
                            }
                        }
                    }
                    if (tocaOtroUno) bonoUnos++;
                }
            }
        }

        // MATEMÁTICA SENIOR: Dividimos por 2 porque la topología contó cada conexión dos veces 
        // (El dado A vio al B, y más tarde el dado B vio al A).
        contactosDiagonalesValidos /= 2;

        if (contactosDiagonalesValidos > 0)
        {
            // Sumamos los puntos extra como pediste en tu GDD
            int pts = contactosDiagonalesValidos * 1;
            totalBono += pts;
            desgloseBonos += $"<color=green>Conexiones Diagonales (x{contactosDiagonalesValidos}): +{pts} pts</color>\n";
        }

        if (bonoUnos > 0)
        {
            totalBono += bonoUnos;
            desgloseBonos += $"<color=green>Unos en contacto (x{bonoUnos}): +{bonoUnos} pts</color>\n";
        }

        return totalBono;
    }

    // 1. Fija el dado definitivamente en la lógica y lo instancia visualmente
    public void CommitDieToLogic(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        DieData[,] currentLogic = allBoardsLogic[pIndex];
        currentLogic[r, c] = new DieData(color, groupId, number);

        InstantiateDieVisual(pIndex, r, c, color, number);
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

    // 2. Escanea solo el dado recién puesto para ver si toca en diagonal a otro de su misma especie
    public int ScanNewDiagonalConnections(int pIndex, int r, int c, DieColor color, int groupId)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        int conexionesNuevas = 0;

        int[] dr = { -1, -1, 1, 1 };
        int[] dc = { -1, 1, -1, 1 };

        for (int d = 0; d < 4; d++)
        {
            int nr = r + dr[d];
            int nc = c + dc[d];
            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
            {
                DieData vecino = logic[nr, nc];
                // Si hay vecino, es del mismo color, tiene el mismo número, PERO es de otra agrupación...
                if (vecino != null && vecino.color == color && vecino.value == logic[r, c].value && vecino.groupId != groupId)
                {
                    conexionesNuevas++;
                }
            }
        }
        return conexionesNuevas;
    }

    // (Opcional, usado para obtener dónde spawnear el Pop-Up)
    public Vector3 GetWorldPosition(int pIndex, int r, int c)
    {
        return new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2);
    }

    public void PlaceTemporaryDieVisual(int pIndex, int r, int c, DieColor color, int number)
    {
        // Por seguridad, si ya había un fantasma, lo destruimos
        RemoveTemporaryDieVisual();

        GameObject prefabAUsar = GetPrefabByColor(color);
        Vector3 position = new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2.1f); // Un poco más adelante

        temporaryDie = Instantiate(prefabAUsar, position, Quaternion.identity, boardRoots[pIndex].transform);
        temporaryDie.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        SpriteRenderer renderer = temporaryDie.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = UIManager.Instance.GetSprite(color, number);

            // EFECTO VISUAL SENIOR: Lo hacemos 50% transparente para que el jugador sepa que "no es definitivo"
            Color colorFantasma = renderer.color;
            colorFantasma.a = 0.5f;
            renderer.color = colorFantasma;
        }
    }

    public void RemoveTemporaryDieVisual()
    {
        if (temporaryDie != null)
        {
            Destroy(temporaryDie);
            temporaryDie = null;
        }
    }

    // --- NUEVO MOTOR DE COMBOS EN TIEMPO REAL ---
    public int EvaluateAndApplyCombos(int pIndex)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        PlayerData player = GameManager.Instance.players[pIndex];

        int completedRows = 0;
        int completedCols = 0;
        int maxConsecutiveRows = 0;
        int maxConsecutiveCols = 0;
        int intersections = 0;

        bool[] rowsFull = new bool[rows];
        bool[] colsFull = new bool[cols];

        // 1. Scan for completed lines (Rows)
        int currentConsecutive = 0;
        for (int r = 0; r < rows; r++)
        {
            rowsFull[r] = true;
            for (int c = 0; c < cols; c++) if (logic[r, c] == null) { rowsFull[r] = false; break; }

            if (rowsFull[r])
            {
                completedRows++;
                currentConsecutive++;
                maxConsecutiveRows = Mathf.Max(maxConsecutiveRows, currentConsecutive);
            }
            else currentConsecutive = 0;
        }

        // 1.b Scan for completed lines (Columns)
        currentConsecutive = 0;
        for (int c = 0; c < cols; c++)
        {
            colsFull[c] = true;
            for (int r = 0; r < rows; r++) if (logic[r, c] == null) { colsFull[c] = false; break; }

            if (colsFull[c])
            {
                completedCols++;
                currentConsecutive++;
                maxConsecutiveCols = Mathf.Max(maxConsecutiveCols, currentConsecutive);
            }
            else currentConsecutive = 0;
        }

        // 2. Intersections
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (rowsFull[r] && colsFull[c]) intersections++;
            }
        }

        // Si no hay líneas, no hay nada que calcular
        if (completedRows == 0 && completedCols == 0) return 0;

        // 3. Client Math: Base * Multipliers (POINT 9: Intersections separated)
        int rowBasePoints = completedRows * ScoreManager.ROW_COMPLETE_BONUS;
        int colBasePoints = completedCols * ScoreManager.COL_COMPLETE_BONUS;

        // Aislar los puntos de intersección
        int intersectionPoints = intersections * ScoreManager.INTERSECTION_BONUS;

        float multRow = completedRows > 0 ? ScoreManager.Instance.GetConsecutiveRowMultiplier(maxConsecutiveRows) : 0f;
        float multCol = completedCols > 0 ? ScoreManager.Instance.GetConsecutiveColMultiplier(maxConsecutiveCols) : 0f;

        float totalMultiplier = multRow + multCol;
        if (totalMultiplier == 0f) totalMultiplier = 1f; // Prevención de errores

        // APLICACIÓN DE REGLA 9: El multiplicador solo afecta a Filas y Columnas
        int lineScoreWithMultiplier = Mathf.FloorToInt((rowBasePoints + colBasePoints) * totalMultiplier);

        // Las intersecciones se suman planas (sin multiplicar)
        int currentTotalStructureScore = lineScoreWithMultiplier + intersectionPoints;

        // 4. Differential Logic (Restamos lo que ya se pagó en turnos anteriores)
        // NOTA: Asegúrate de renombrar 'accumulatedStructurePoints' a 'accumulatedStructurePoints' en PlayerData.cs
        int newPointsToEarn = currentTotalStructureScore - player.accumulatedStructurePoints;

        // Actualizamos la memoria del jugador
        player.accumulatedStructurePoints = currentTotalStructureScore;

        return newPointsToEarn;
    }

    // Cuenta cuántos dados tocan en 3x3 y cuántos de esos toques fueron en diagonal
    public int Count3x3Contacts(int pIndex, int r, int c, int valorDado, out int contactosDiagonales)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        int contactosTotales = 0;
        contactosDiagonales = 0;

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue; // Ignoramos el centro

                int nr = r + i;
                int nc = c + j;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    DieData vecino = logic[nr, nc];

                    if (vecino != null && vecino.value == valorDado)
                    {
                        contactosTotales++;

                        // Si nos movimos en X y también en Y, es un movimiento diagonal
                        if (i != 0 && j != 0)
                        {
                            contactosDiagonales++;
                        }
                    }
                }
            }
        }
        return contactosTotales;
    }




}