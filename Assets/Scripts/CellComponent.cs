using UnityEngine;
using DG.Tweening;

public class CellComponent : MonoBehaviour
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
    [SerializeField] private SpriteRenderer childSpriteRenderer; // Asignar el hijo aquí
    private Color originalColor;

    void Awake()
    {
        // Fallback: If not assigned in Inspector, search in children automatically
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
            Debug.LogError($"CellComponent on {gameObject.name} is missing a SpriteRenderer in its children!");
        }
    }

    // Dependency Injection via Setup. 
    // The cell receives its dependencies without knowing their concrete implementations.
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

        // El resaltado normal de movimientos ocurre en celdas vacías, no necesita superponerse a un dado
        childSpriteRenderer.color = highlight ? new Color(0.5f, 1f, 0.5f, 1f) : originalColor;
        childSpriteRenderer.sortingOrder = 0; // Orden base
    }

    void OnMouseDown()
    {
        HandleClick();
    }

    public void HandleClick()
    {
        // 1. Ask the Turn Provider if it's our turn
        if (turnProvider.CurrentPlayerIndex != playerOwnerIndex)
        {
            Debug.LogWarning("Invalid Turn or Board.");
            return;
        }

        if (!turnProvider.HasDrawn) return;

        DieColor currentColor = turnProvider.CurrentDrawnColor;
        PlayerData currentPlayer = turnProvider.GetCurrentPlayer();

        if (currentPlayer.activeGroups.TryGetValue(currentColor, out GroupData group))
        {
            if (group != null && group.targetSize > 0)
            {
                // 2. Ask the Grid Validator if the move is legal
                if (gridValidator.CanBotPlaceHere(playerOwnerIndex, row, col, currentColor, group.id, group.targetSize))
                {
                    // 3. Ask the Executor to process the play
                    placementExecutor.BeginPlacement(row, col);
                }
            }
        }
    }

    /// <summary>
    /// Colors the empty cell red to indicate a gap penalty.
    /// </summary>
    public void HighlightGapColor()
    {
        // Si tu tablero usa SpriteRenderer (2D World)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Usamos un rojo semitransparente/claro para que no sea muy agresivo
            sr.color = new Color(1f, 0.3f, 0.3f, 1f);
        }
        else
        {
            // Si por algún motivo tu tablero está hecho de UI Images (Canvas)
            UnityEngine.UI.Image img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = new Color(1f, 0.3f, 0.3f, 1f);
        }
    }



}