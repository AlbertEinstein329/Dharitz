using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
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
    public GameObject prefabRojo, prefabAzul, prefabBlanco, prefabNegro;

    // --- SISTEMA MULTITABLERO ---
    private List<DieData[,]> allBoardsLogic;
    private List<CellComponent[,]> allCellsVisual;
    private GameObject[] boardRoots; // Los objetos "Padre" de cada tablero
    public int currentlyViewedPlayer = 0;

    private float startX;
    private float startY;

    void Awake()
    {
        allBoardsLogic = new List<DieData[,]>();
        allCellsVisual = new List<CellComponent[,]>();
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
                // Instanciamos la celda DENTRO del "boardRoot" del jugador correspondiente
                GameObject newCell = Instantiate(cellPrefab, position, Quaternion.identity, boardRoots[playerIndex].transform);

                CellComponent cellScript = newCell.GetComponent<CellComponent>();
                if (cellScript != null)
                {
                    cellScript.Setup(r, c, playerIndex, this);
                    allCellsVisual[playerIndex][r, c] = cellScript; // <-- Añade esto
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

        // NUEVA LÓGICA: Si el juego ya terminó, actualiza el panel de puntajes 
        // para que muestre la información del jugador que acabamos de poner en pantalla
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            UIManager.Instance.MostrarResultadosFinales(playerIndex);
        }
    }

    public void VerSiguienteTablero()
    {
        int next = (currentlyViewedPlayer + 1) % GameManager.Instance.numPlayers;
        SwitchViewTo(next);
    }

    public void VerTableroAnterior()
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

        GameObject prefabAUsar = GetPrefabByColor(color);
        Vector3 position = new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2);

        // El dado se instancia dentro del tablero de este jugador
        GameObject nuevoDado = Instantiate(prefabAUsar, position, Quaternion.identity, boardRoots[pIndex].transform);
        nuevoDado.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        SpriteRenderer renderer = nuevoDado.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = UIManager.Instance.GetSprite(color, number);

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

        bool isBoardEmpty = (player.dadosColocados == 0);

        // 1. ESTADO DEL GRUPO: ¿Ya hay dados de este patrón en el tablero?
        bool hasDiceInGroup = false;
        if (player.activeGroups.ContainsKey(color) && player.activeGroups[color] != null)
        {
            hasDiceInGroup = player.activeGroups[color].occupiedCells.Count > 0;
        }

        bool touchesOwnGroup = false;
        bool touchesAnyDie = false;

        // --- 2. ESCANEO DEL ÁREA 3x3 ---
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
                        // Regla estricta del 1 (Aislamiento penalizado por mismo color)
                        if (number == 1 && neighbor.value == 1 && neighbor.color == color)
                        {
                            return false;
                        }

                        // Verificamos si tocamos a un hermano del mismo patrón (Ortogonal o Diagonal)
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

        // --- 3. RESOLUCIÓN DE CONECTIVIDAD ---

        // Excepción A: Si el tablero está vacío, todo está permitido
        if (isBoardEmpty) return true;

        // REGLA CRÍTICA NUEVA: Si el grupo ya empezó, DEBE tocar a su familia en el 3x3
        if (hasDiceInGroup && !touchesOwnGroup)
        {
            Debug.Log("Movimiento inválido: El dado debe continuar la formación de su patrón (3x3).");
            return false;
        }

        // Excepción B: Si el grupo es nuevo, debe anclarse a cualquier dado válido existente
        if (!touchesAnyDie)
        {
            Debug.Log("Movimiento inválido: El dado debe estar en contacto con al menos otro dado.");
            return false;
        }

        // --- 4. SISTEMA DE SUPERVIVENCIA (RESERVAS) ---
        return ValidarSupervivencia(pIndex, r, c, color, currentGroupId, number, player);
    }

    private GameObject GetPrefabByColor(DieColor color)
    {
        switch (color)
        {
            case DieColor.Rojo: return prefabRojo;
            case DieColor.Azul: return prefabAzul;
            case DieColor.Blanco: return prefabBlanco;
            case DieColor.Negro: return prefabNegro;
            default: return prefabBlanco;
        }
    }

    // Adaptado para calcular huecos por jugador específico
    public int ObtenerPenalizacionesPorHuecos(int pIndex)
    {
        DieData[,] currentLogic = allBoardsLogic[pIndex];
        bool[,] visitado = new bool[rows, cols];
        int huecosEncerrados = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if ((r == 0 || r == rows - 1 || c == 0 || c == cols - 1) && currentLogic[r, c] == null && !visitado[r, c])
                {
                    FloodFillBordes(r, c, visitado, currentLogic);
                }
            }
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (currentLogic[r, c] == null && !visitado[r, c]) huecosEncerrados++;
            }
        }
        return huecosEncerrados * 5;
    }

    private void FloodFillBordes(int r, int c, bool[,] visitado, DieData[,] currentLogic)
    {
        if (r < 0 || r >= rows || c < 0 || c >= cols || visitado[r, c] || currentLogic[r, c] != null) return;
        visitado[r, c] = true;
        FloodFillBordes(r + 1, c, visitado, currentLogic);
        FloodFillBordes(r - 1, c, visitado, currentLogic);
        FloodFillBordes(r, c + 1, visitado, currentLogic);
        FloodFillBordes(r, c - 1, visitado, currentLogic);
    }

    // Añade este método en GridManager.cs
    public void ContarLineasCompletas(int pIndex, out int filasCompletas, out int columnasCompletas)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        filasCompletas = 0;
        columnasCompletas = 0;

        // 1. Revisar filas (Horizontales)
        for (int r = 0; r < rows; r++)
        {
            bool estaLlena = true;
            for (int c = 0; c < cols; c++)
            {
                if (logic[r, c] == null) { estaLlena = false; break; }
            }
            if (estaLlena) filasCompletas++;
        }

        // 2. Revisar columnas (Verticales)
        for (int c = 0; c < cols; c++)
        {
            bool estaLlena = true;
            for (int r = 0; r < rows; r++)
            {
                if (logic[r, c] == null) { estaLlena = false; break; }
            }
            if (estaLlena) columnasCompletas++;
        }
    }

    // Escanea el tablero del jugador y aplica -1 por cada dado de valor 1 que toque a otro 1
    public int ObtenerPenalizacionesPorUnos(int pIndex)
    {
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

    private bool ValidarSupervivencia(int pIndex, int r, int c, DieColor color, int newGroupId, int newTargetSize, PlayerData player)
    {
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
        bool esValido = AnalizarTopologia(pIndex, logic, dadosFaltantes);

        // 4. REVERSIÓN DE LA SIMULACIÓN
        logic[r, c] = null;

        return esValido;
    }

    private bool AnalizarTopologia(int pIndex, DieData[,] logic, Dictionary<int, int> dadosFaltantes)
    {
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        foreach (var kvp in dadosFaltantes)
        {
            int gId = kvp.Key;
            int requeridos = kvp.Value;

            if (requeridos <= 0) continue;

            int vaciosAlcanzables = 0;
            bool[,] visitado = new bool[rows, cols];
            Queue<Vector2Int> cola = new Queue<Vector2Int>();

            // NUEVO: Necesitamos extraer el color de este grupo para poder identificar sus "Zonas Muertas"
            DieColor colorDelGrupo = DieColor.Blanco;
            bool colorEncontrado = false;

            // 1. Encontrar todos los dados de ESTE grupo
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (logic[r, c] != null && logic[r, c].groupId == gId)
                    {
                        if (!colorEncontrado)
                        {
                            colorDelGrupo = logic[r, c].color;
                            colorEncontrado = true;
                        }
                        cola.Enqueue(new Vector2Int(r, c));
                        visitado[r, c] = true;
                    }
                }
            }

            // 2. Expandir el Flood-Fill con detección de colisión de color
            while (cola.Count > 0)
            {
                Vector2Int actual = cola.Dequeue();

                for (int d = 0; d < 4; d++)
                {
                    int nr = actual.x + dr[d];
                    int nc = actual.y + dc[d];

                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        if (logic[nr, nc] == null && !visitado[nr, nc])
                        {
                            // --- LA NUEVA LÓGICA SENIOR: Detección de Zona Muerta ---
                            // Antes de asumir que este espacio es útil, revisamos a SUS vecinos ortogonales
                            bool esZonaMuerta = false;
                            for (int d2 = 0; d2 < 4; d2++)
                            {
                                int nnr = nr + dr[d2];
                                int nnc = nc + dc[d2];

                                if (nnr >= 0 && nnr < rows && nnc >= 0 && nnc < cols)
                                {
                                    DieData vecinoDelVacio = logic[nnr, nnc];

                                    // Si el vecino existe, es de tu mismo color, pero pertenece a OTRA familia...
                                    // Ese espacio es ilegal para ti.
                                    if (vecinoDelVacio != null && vecinoDelVacio.color == colorDelGrupo && vecinoDelVacio.groupId != gId)
                                    {
                                        esZonaMuerta = true;
                                        break; // Rompemos el ciclo, ya sabemos que está bloqueado
                                    }
                                }
                            }

                            // Solo encolamos y contamos el espacio si es seguro ocuparlo
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
                Debug.Log($"Bloqueo Topológico: El grupo {gId} necesita {requeridos} espacios, pero choca con muros o Zonas Muertas de su propio color y solo alcanza {vaciosAlcanzables}.");
                return false;
            }
        }

        return true;
    }

    // Evalúa todo el tablero y resalta las celdas válidas
    public void MostrarMovimientosValidos(int pIndex, DieColor color, int groupId, int targetSize)
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
    public void LimpiarResaltados(int pIndex)
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

}