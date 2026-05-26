using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

// NUEVO: Añadimos IPointerClickHandler a la clase
[RequireComponent(typeof(BoxCollider2D))]
public class CellComponent : MonoBehaviour, IPointerClickHandler
{
    private int row;
    private int col;
    private int playerOwnerIndex;

    // Dependencies via Interfaces (ISP applied)
    private IGridValidator gridValidator;
    private ITurnProvider turnProvider;
    private IPlacementExecutor placementExecutor;

    [Header("Visual Configuration")]
    [Tooltip("Drag the child object containing the SpriteRenderer here.")]
    public SpriteRenderer childSpriteRenderer;

    [Tooltip("Drag the overlay sprite for completed patterns here.")]
    [SerializeField] private SpriteRenderer completionSpriteOverlay;
    private Color originalColor;

    [Header("Datos de la Entidad")]
    // Esta coordenada DEBE ser asignada por tu BoardLogic cuando genera el tablero
    public Vector2Int gridCoordinate;

    void Awake()
    {
        if (childSpriteRenderer == null)
        {
            childSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (childSpriteRenderer != null)
        {
            originalColor = childSpriteRenderer.color;
        }
        else
        {
            Debug.LogError($"CellComponent on {gameObject.name} is missing a SpriteRenderer!");
        }
    }

    private void Start()

    {

        // Si no se asignaron desde el Inspector o al instanciar, búscalas automáticamente

        if (turnProvider == null) turnProvider = GameManager.Instance;

        if (gridValidator == null) gridValidator = GameManager.Instance.gridManager; // O GameManager.Instance si lo implementa directo

        if (placementExecutor == null) placementExecutor = GameManager.Instance;

    }



    public void Setup(int r, int c, int pIndex, IGridValidator validator, ITurnProvider turnInfo, IPlacementExecutor executor)
    {
        row = r;
        col = c;
        this.playerOwnerIndex = pIndex;

        gridValidator = validator;
        turnProvider = turnInfo;
        placementExecutor = executor;
    }

    public void SetHighlight(bool highlight)
    {

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Cambia el color a amarillo suave si está iluminado, y vuelve al blanco (normal) si no.
            sr.color = highlight ? new Color(1f, 1f, 0f, 0.5f) : Color.white;
        }

        if (childSpriteRenderer == null) return;
        childSpriteRenderer.color = highlight ? new Color(0.5f, 1f, 0.5f, 1f) : originalColor;
        childSpriteRenderer.sortingOrder = 0;


    }



    public void ToggleCompletionSprite(bool isActive)
    {
        if (completionSpriteOverlay != null)
        {
            completionSpriteOverlay.gameObject.SetActive(isActive);
        }
    }

    [System.Obsolete]
    public void OnPointerClick(PointerEventData eventData)
    {
        if (turnProvider == null) return;

        // 🛠️ THE FIX: We define what isOwnerTurn actually means mathematically!
        bool isOwnerTurn = (turnProvider.CurrentPlayerIndex == playerOwnerIndex);

        if (!GridInteractionManager.Instance.IsMoveModeActive && !isOwnerTurn)
        {
            Debug.LogWarning("Invalid turn.");
            return;
        }

        if (!turnProvider.HasDrawn) return;

        DieColor currentColor = turnProvider.CurrentDrawnColor;
        PlayerData currentPlayer = turnProvider.GetCurrentPlayer();

        if (currentPlayer == null) return;

        int targetSize = 0;
        int currentGroupId = 0;

        if (currentPlayer.activeGroups.TryGetValue(currentColor, out GroupData group))
        {
            if (group != null && group.targetSize > 0)
            {
                targetSize = group.targetSize;
                currentGroupId = group.id;
            }
        }

        if (targetSize == 0) return;

        int realRow = this.gridCoordinate.y;
        int realCol = this.gridCoordinate.x;

        if (gridValidator.IsValidPlacement(playerOwnerIndex, realRow, realCol, currentColor, currentGroupId, targetSize))
        {
            // Trigger the flying animation!
            GridManager gridManager = FindObjectOfType<GridManager>();
            if (gridManager != null)
            {
                gridManager.AnimateAndPlaceDie(realRow, realCol, currentColor, targetSize, transform.position, placementExecutor);
            }
        }
    }
    public void ProcessInteraction()
    {

        // Consultemos el ID real desde el GridManager.
        int currentPlayerTurn = GameManager.Instance.turnManager.CurrentPlayerIndex;

        // MODIFICACIÓN: En lugar de comparar contra una variable interna,
        // validemos si la celda clickeada pertenece al jugador activo.
        if (currentPlayerTurn != this.playerOwnerIndex)
        {
            Debug.LogWarning($"[Error] Clic en celda de Jugador {playerOwnerIndex}, pero turno es de {currentPlayerTurn}");
            return;
        }

        // =========================================================================
        // 1. INTERCEPCIÓN DEL COMODÍN MOVE (Al principio de todo)
        // Si el modo mover está activo, procesamos el clic aquí y detenemos el flujo normal.
        // =========================================================================
        if (GridInteractionManager.Instance != null && GridInteractionManager.Instance.IsMoveModeActive)
        {
            // Verificamos de forma segura que el jugador esté interactuando con SU propio tablero
            if (GameManager.Instance.turnManager.CurrentPlayerIndex == playerOwnerIndex)
            {
                GridInteractionManager.Instance.OnGridCellClicked(gridCoordinate);
            }
            else
            {
                Debug.LogWarning("[Tráfico] Clic ignorado: Intentaste usar el comodín en el tablero de un rival.");
            }
            return; // Bloquea y evita que se ejecute la colocación normal de dados inferiores
        }

        // =========================================================================
        // 2. COLOCACIÓN NORMAL DE DADOS (Tu lógica intacta)
        // =========================================================================
        if (turnProvider == null || gridValidator == null || placementExecutor == null) return;

        bool isOwnerTurn = (GameManager.Instance.turnManager.CurrentPlayerIndex == playerOwnerIndex);

        // Usamos el turnManager real para la colocación estándar
        if (!GridInteractionManager.Instance.IsMoveModeActive && !isOwnerTurn)
        {
            Debug.LogWarning("Turno inválido para colocar dado.");
            return;
        }

        if (!turnProvider.HasDrawn) return;

        DieColor currentColor = turnProvider.CurrentDrawnColor;
        PlayerData currentPlayer = turnProvider.GetCurrentPlayer();

        if (currentPlayer == null) return;

        int targetSize = 0;
        int currentGroupId = 0;

        if (currentPlayer.activeGroups.TryGetValue(currentColor, out GroupData group))
        {
            if (group != null && group.targetSize > 0)
            {
                targetSize = group.targetSize;
                currentGroupId = group.id;
            }
        }

        if (targetSize == 0) return;

        int realRow = this.gridCoordinate.y;
        int realCol = this.gridCoordinate.x;

        if (gridValidator.IsValidPlacement(playerOwnerIndex, realRow, realCol, currentColor, currentGroupId, targetSize))
        {
            placementExecutor.BeginPlacement(realRow, realCol);
        }
        else
        {
            Debug.LogWarning($"[Tráfico] Movimiento inválido en ({realCol}, {realRow}). La celda ignora la interacción.");
        }
    }

    public void HighlightGapColor()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0.3f, 0.3f, 1f);
    }
}