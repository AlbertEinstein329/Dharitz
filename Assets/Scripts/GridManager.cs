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
    public GameObject prefabRojo, prefabAzul, prefabBlanco, prefabNegro;

    // --- SISTEMA MULTITABLERO ---
    private List<DieData[,]> allBoardsLogic;
    private List<CellComponent[,]> allCellsVisual;
    private GameObject[] boardRoots; // Los objetos "Padre" de cada tablero
    public int currentlyViewedPlayer = 0;
    private GameObject dadoTemporal;

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
                UIManager.Instance.ActualizarScore(scoreDelJugador);
            }

            // Si el juego ya terminó, actualiza el panel de puntajes finales
            if (GameManager.Instance.isGameOver)
            {
                UIManager.Instance.MostrarResultadosFinales(playerIndex);
            }
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

        // --- INYECCIÓN DE REGLAS ---
        VariantData variante = GameManager.Instance.varianteActual;
        PatternData patronActual = variante.ObtenerPatron(number);

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
                            if (patronActual.reglaEspecial == PatternData.SpecialRule.PenalizeOnContact)
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

    // --- NUEVO SISTEMA DE DETECCIÓN DE HUECOS POR CLUSTERS ---
    public int CalcularPenalizacionHuecos(int pIndex, out int cantidadTotalHuecos)
    {
        DieData[,] currentLogic = allBoardsLogic[pIndex];
        bool[,] visitado = new bool[rows, cols];

        cantidadTotalHuecos = 0;
        int penalizacionTotal = 0;

        // 1. INUNDAR DESDE LOS BORDES (Marcar el "Exterior")
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if ((r == 0 || r == rows - 1 || c == 0 || c == cols - 1) && currentLogic[r, c] == null && !visitado[r, c])
                {
                    MarcarExterior(r, c, visitado, currentLogic);
                }
            }
        }

        // 2. ESCANEAR EL INTERIOR (Detectar y medir cada "Cluster" aislado)
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (currentLogic[r, c] == null && !visitado[r, c])
                {
                    // Encontramos un hueco encerrado. Vamos a medir de qué tamaño es este cluster.
                    int tamanoCluster = MedirClusterEncerrado(r, c, visitado, currentLogic);

                    cantidadTotalHuecos += tamanoCluster;

                    // Cobramos la multa exacta para el tamaño de ESTE cluster específico
                    penalizacionTotal += ScoreManager.Instance.GetHolePenalty(tamanoCluster);
                }
            }
        }

        return penalizacionTotal;
    }

    private void MarcarExterior(int r, int c, bool[,] visitado, DieData[,] currentLogic)
    {
        // El FloodFill de 8 direcciones (diagonal) evita que una pared de dados en diagonal 
        // declare todo el tablero como "encerrado" erróneamente.
        if (r < 0 || r >= rows || c < 0 || c >= cols || visitado[r, c] || currentLogic[r, c] != null) return;
        visitado[r, c] = true;

        // Búsqueda en 8 direcciones para que el "aire" se cuele por las esquinas
        int[] dr = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] dc = { 0, 0, -1, 1, -1, 1, -1, 1 };
        for (int i = 0; i < 8; i++)
        {
            MarcarExterior(r + dr[i], c + dc[i], visitado, currentLogic);
        }
    }

    private int MedirClusterEncerrado(int r, int c, bool[,] visitado, DieData[,] currentLogic)
    {
        // Los huecos interiores solo se conectan de forma ortogonal
        if (r < 0 || r >= rows || c < 0 || c >= cols || visitado[r, c] || currentLogic[r, c] != null) return 0;

        visitado[r, c] = true;
        int count = 1; // Este espacio cuenta como 1

        count += MedirClusterEncerrado(r + 1, c, visitado, currentLogic);
        count += MedirClusterEncerrado(r - 1, c, visitado, currentLogic);
        count += MedirClusterEncerrado(r, c + 1, visitado, currentLogic);
        count += MedirClusterEncerrado(r, c - 1, visitado, currentLogic);

        return count;
    }

    private int CalcularBonoLineas(bool[] lines, int baseBonus, bool isRow)
    {
        int total = 0;
        int consecutive = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i])
            {
                consecutive++;
                float mult = isRow ? ScoreManager.Instance.GetConsecutiveRowMultiplier(consecutive) : ScoreManager.Instance.GetConsecutiveColMultiplier(consecutive);
                total += Mathf.FloorToInt(baseBonus * mult);
            }
            else
            {
                consecutive = 0; // Se rompe el combo si hay un hueco 
            }
        }
        return total;
    }

    // Escanea el tablero del jugador y aplica -1 por cada dado de valor 1 que toque a otro 1
    public int ObtenerPenalizacionesPorUnos(int pIndex)
    {
        // --- INYECCIÓN DE VARIANTE ---
        VariantData variante = GameManager.Instance.varianteActual;
        PatternData patron1 = variante.ObtenerPatron(1);

        // Si el patrón 1 tiene la regla de premiar el contacto, ANULAMOS la penalización.
        if (patron1 != null && patron1.reglaEspecial == PatternData.SpecialRule.RewardOnContact)
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
        bool esValido = AnalizarTopologia(pIndex, logic, dadosFaltantes, player); // <-- Añadimos 'player' al final

        // 4. REVERSIÓN DE LA SIMULACIÓN
        logic[r, c] = null;

        return esValido;
    }

    private bool AnalizarTopologia(int pIndex, DieData[,] logic, Dictionary<int, int> dadosFaltantes, PlayerData player)
    {
        VariantData variante = GameManager.Instance.varianteActual;

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

            PatternData patronDelGrupo = variante.ObtenerPatron(grupoActivo.targetSize);

            // ¿Tiene permiso este grupo para "saltar" bloqueos en diagonal?
            bool puedeReservarDiagonal = patronDelGrupo.reglaEspecial == PatternData.SpecialRule.ExtraDiagonalContact || variante.reservasDiagonalesPermitidas;
            int direccionesDeBusqueda = puedeReservarDiagonal ? 8 : 4; // Cambia la potencia del escáner

            int vaciosAlcanzables = 0;
            bool[,] visitado = new bool[rows, cols];
            Queue<Vector2Int> cola = new Queue<Vector2Int>();

            DieColor colorDelGrupo = grupoActivo.color;

            // 1. Encontrar todos los dados de ESTE grupo
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (logic[r, c] != null && logic[r, c].groupId == gId)
                    {
                        cola.Enqueue(new Vector2Int(r, c));
                        visitado[r, c] = true;
                    }
                }
            }

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

    public int CalcularBonosDeVariante(int pIndex, out string desgloseBonos)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        VariantData variante = GameManager.Instance.varianteActual;

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

                PatternData patron = variante.ObtenerPatron(dado.value);
                if (patron == null) continue;

                // --- REGLA: Contacto Diagonal Extra (Variante 1 para el 2) ---
                if (patron.reglaEspecial == PatternData.SpecialRule.ExtraDiagonalContact)
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
                if (patron.reglaEspecial == PatternData.SpecialRule.RewardOnContact)
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
    public void FijarDadoEnLogica(int pIndex, int r, int c, DieColor color, int groupId, int number)
    {
        DieData[,] currentLogic = allBoardsLogic[pIndex];
        currentLogic[r, c] = new DieData(color, groupId, number);

        // Instanciamos el dado físico final en el tablero
        GameObject prefabAUsar = GetPrefabByColor(color);
        Vector3 position = new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2);

        GameObject nuevoDado = Instantiate(prefabAUsar, position, Quaternion.identity, boardRoots[pIndex].transform);
        nuevoDado.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        SpriteRenderer renderer = nuevoDado.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = UIManager.Instance.GetSprite(color, number);
    }

    // 2. Escanea solo el dado recién puesto para ver si toca en diagonal a otro de su misma especie
    public int EscanearConexionesDiagonalesNuevas(int pIndex, int r, int c, DieColor color, int groupId)
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
    public Vector3 ObtenerPosicionMundo(int pIndex, int r, int c)
    {
        return new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2);
    }

    public void ColocarDadoVisualTemporal(int pIndex, int r, int c, DieColor color, int number)
    {
        // Por seguridad, si ya había un fantasma, lo destruimos
        RemoverDadoVisualTemporal();

        GameObject prefabAUsar = GetPrefabByColor(color);
        Vector3 position = new Vector3(startX + (c * cellSize), startY + (r * cellSize), -2.1f); // Un poco más adelante

        dadoTemporal = Instantiate(prefabAUsar, position, Quaternion.identity, boardRoots[pIndex].transform);
        dadoTemporal.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        SpriteRenderer renderer = dadoTemporal.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = UIManager.Instance.GetSprite(color, number);

            // EFECTO VISUAL SENIOR: Lo hacemos 50% transparente para que el jugador sepa que "no es definitivo"
            Color colorFantasma = renderer.color;
            colorFantasma.a = 0.5f;
            renderer.color = colorFantasma;
        }
    }

    public void RemoverDadoVisualTemporal()
    {
        if (dadoTemporal != null)
        {
            Destroy(dadoTemporal);
            dadoTemporal = null;
        }
    }

    // --- NUEVO MOTOR DE COMBOS EN TIEMPO REAL ---
    public int EvaluarYCobrarCombosEnTiempoReal(int pIndex)
    {
        DieData[,] logic = allBoardsLogic[pIndex];
        PlayerData player = GameManager.Instance.players[pIndex];

        int completasRows = 0;
        int completasCols = 0;
        int maxConsecutiveRows = 0;
        int maxConsecutiveCols = 0;
        int intersecciones = 0;

        bool[] rowsFull = new bool[rows];
        bool[] colsFull = new bool[cols];

        // 1. Escaneo de líneas completas (Filas)
        int currentConsecutive = 0;
        for (int r = 0; r < rows; r++)
        {
            rowsFull[r] = true;
            for (int c = 0; c < cols; c++) if (logic[r, c] == null) { rowsFull[r] = false; break; }
            if (rowsFull[r]) { completasRows++; currentConsecutive++; maxConsecutiveRows = Mathf.Max(maxConsecutiveRows, currentConsecutive); }
            else currentConsecutive = 0;
        }

        // 1.b Escaneo de líneas completas (Columnas)
        currentConsecutive = 0;
        for (int c = 0; c < cols; c++)
        {
            colsFull[c] = true;
            for (int r = 0; r < rows; r++) if (logic[r, c] == null) { colsFull[c] = false; break; }
            if (colsFull[c]) { completasCols++; currentConsecutive++; maxConsecutiveCols = Mathf.Max(maxConsecutiveCols, currentConsecutive); }
            else currentConsecutive = 0;
        }

        // 2. Intersecciones
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (rowsFull[r] && colsFull[c]) intersecciones++;
            }
        }

        // Si no hay líneas, no hay nada que calcular
        if (completasRows == 0 && completasCols == 0) return 0;

        // 3. Matemática del Cliente: Base * Suma de Multiplicadores
        int basePuntos = (completasRows * ScoreManager.ROW_COMPLETE_BONUS) +
                         (completasCols * ScoreManager.COL_COMPLETE_BONUS) +
                         (intersecciones * ScoreManager.INTERSECTION_BONUS);

        float multRow = completasRows > 0 ? ScoreManager.Instance.GetConsecutiveRowMultiplier(maxConsecutiveRows) : 0f;
        float multCol = completasCols > 0 ? ScoreManager.Instance.GetConsecutiveColMultiplier(maxConsecutiveCols) : 0f;

        float multTotal = multRow + multCol;
        if (multTotal == 0f) multTotal = 1f; // Prevención de errores por si los multiplicadores fallan

        int puntajeTotalEstructuraActual = Mathf.FloorToInt(basePuntos * multTotal);

        // 4. Lógica Diferencial (Restamos lo que ya se le pagó en turnos anteriores para no cobrar doble)
        int puntosNuevosAGanar = puntajeTotalEstructuraActual - player.puntosEstructuraAcumulados;

        // Actualizamos la memoria del jugador
        player.puntosEstructuraAcumulados = puntajeTotalEstructuraActual;

        return puntosNuevosAGanar;
    }

    // Cuenta cuántos dados tocan en 3x3 y cuántos de esos toques fueron en diagonal
    public int ContarContactosEn3x3(int pIndex, int r, int c, int valorDado, out int contactosDiagonales)
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

    /// <summary>
    /// Highlights all cells that are part of a successfully completed pattern.
    /// </summary>
    public void HighlightCompletedPattern(int pIndex, IEnumerable<Vector2Int> occupiedCells)
    {
        foreach (Vector2Int pos in occupiedCells)
        {
            // Boundary safety check
            if (pos.x >= 0 && pos.x < rows && pos.y >= 0 && pos.y < cols)
            {
                CellComponent cell = allCellsVisual[pIndex][pos.x, pos.y];
                if (cell != null)
                {
                    cell.TriggerPatternSuccessVisuals();
                }
            }
        }
    }





}