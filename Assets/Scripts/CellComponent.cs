using UnityEngine;

public class CellComponent : MonoBehaviour
{
    private int row;
    private int col;
    private int playerOwnerIndex;

    // Dependencies via Interfaces (ISP applied)
    private IGridValidator gridValidator;
    private ITurnProvider turnProvider;
    private IPlacementExecutor placementExecutor;

    private SpriteRenderer spriteRenderer;
    private Color colorOriginal;

    void Awake()
    {
        // Cache components to avoid GetComponent calls later
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            colorOriginal = spriteRenderer.color;
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
        if (spriteRenderer == null) return;
        // Optimization: Avoid new Color allocation in every call if possible, but fine for simple UI
        spriteRenderer.color = highlight ? new Color(0.5f, 1f, 0.5f, 1f) : colorOriginal;
    }

    void OnMouseDown()
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
                    placementExecutor.IniciarColocacion(row, col);
                }
            }
        }
    }
}