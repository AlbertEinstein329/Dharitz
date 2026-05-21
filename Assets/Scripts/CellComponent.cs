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
    [SerializeField] private SpriteRenderer childSpriteRenderer;
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
        playerOwnerIndex = pIndex;

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

    public void OnPointerClick(PointerEventData eventData)
    {
        // El EventSystem llama a esto automáticamente. Solo filtramos el clic izquierdo.
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Llamamos al motor lógico central
        ProcessInteraction();
    }

    public void ProcessInteraction()
    {
        // ==========================================
        // ESCUDO 1: Blindaje contra NullReference y Turnos
        // ==========================================
        if (turnProvider == null || gridValidator == null || placementExecutor == null) return;

        if (turnProvider.CurrentPlayerIndex != playerOwnerIndex)
        {
            Debug.LogWarning("[Tráfico] Clic ignorado. Tablero equivocado o turno inválido.");
            return;
        }

        // ==========================================
        // INTERCEPCIÓN MÁXIMA PRIORIDAD: Modo Mover
        // ==========================================
        if (GridInteractionManager.Instance != null && GridInteractionManager.Instance.IsMoveModeActive)
        {
            // FALLO CORREGIDO: Usamos gridCoordinate directamente.
            // Esta es la única Fuente de la Verdad que los dados tienen actualizada.
            GridInteractionManager.Instance.OnGridCellClicked(this.gridCoordinate);
            return;
        }

        // ==========================================
        // COMPORTAMIENTO NORMAL: Colocar dado nuevo
        // ==========================================
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

        // FALLO CORREGIDO A LARGO PLAZO: 
        // Extraemos row y col directamente de gridCoordinate para que la colocación 
        // normal tampoco sufra del síndrome de las "coordenadas fantasma".
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