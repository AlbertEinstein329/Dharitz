using UnityEngine;
using UnityEngine.EventSystems; // NUEVO: Necesario para el Input System
using DG.Tweening;

// NUEVO: Añadimos IPointerClickHandler a la clase
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
        HandleClick();
    }

    public void HandleClick()
    {
        // 🛡️ ESCUDO 1: Blindaje contra NullReferenceException
        if (turnProvider == null || gridValidator == null || placementExecutor == null) return;

        // 1. Ask the Turn Provider if it's our turn
        if (turnProvider.CurrentPlayerIndex != playerOwnerIndex)
        {
            Debug.LogWarning("Invalid Turn or Board.");
            return;
        }

        // Interceptamos si el Modo Mover está activo
        var gm = global::GameManager.Instance;
        if (gm != null && gm.placementOrchestrator != null && gm.placementOrchestrator.isMoveModeActive)
        {
            gm.placementOrchestrator.HandleMoveClick(row, col);
            return; // Bloqueamos la colocación normal
        }

        if (!turnProvider.HasDrawn) return;

        DieColor currentColor = turnProvider.CurrentDrawnColor;
        PlayerData currentPlayer = turnProvider.GetCurrentPlayer();

        if (currentPlayer == null) return;

        // 🛡️ ESCUDO 2: Deducimos el tamaño y el ID para permitir colocar grupos NUEVOS
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

        if (targetSize == 0) return; // Si no hay dado extraído, abortamos

        // 2. CORRECCIÓN: Usamos IsValidPlacement en lugar de CanBotPlaceHere
        if (gridValidator.IsValidPlacement(playerOwnerIndex, row, col, currentColor, currentGroupId, targetSize))
        {
            // 3. Ask the Executor to process the play
            placementExecutor.BeginPlacement(row, col);
        }
        else
        {
            Debug.Log("Movimiento inválido. La celda ignora el clic.");
        }
    }

    public void HighlightGapColor()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0.3f, 0.3f, 1f);
    }
}