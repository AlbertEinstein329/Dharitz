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

    [Tooltip("Arrastra aquí tu sprite ROJO para las celdas penalizadas.")]
    [SerializeField] private Sprite gapPenaltySprite;

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

    public void SetHighlight(bool isHighlighted)
    {
        if (isHighlighted)
        {
            // 1. Matamos la animación previa
            childSpriteRenderer.DOKill();

            // 2. Calculamos el color al 20% de iluminación. 
            // Lerp mezcla el originalColor con el Blanco. El 0.2f representa el 20% hacia el blanco.
            Color colorIluminacionMinima = Color.Lerp(originalColor, Color.white, 0.8f);

            // 3. Establecemos este color semi-iluminado como el punto de partida (el suelo de la respiración)
            childSpriteRenderer.color = colorIluminacionMinima;

            // 4. Animamos hacia el Blanco Puro (100%).
            // Como usamos Yoyo, DOTween rebotará eternamente entre el 100% y el 20% que establecimos arriba.
            childSpriteRenderer.DOColor(Color.greenYellow, 0.6f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        else
        {
            ClearHighlight();
        }
    }

    public void ClearHighlight()
    {
        // 1. Detenemos la animación de pulsación inmediatamente
        childSpriteRenderer.DOKill();

        // 2. Restauramos la celda a su color y opacidad 100% original
        childSpriteRenderer.color = originalColor;
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

        // F4.1: Si estamos en partida online, enviamos el ServerRpc al servidor autoritativo
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsClient && MyGame.Networking.NetworkGameManager.Instance != null)
        {
            MyGame.Networking.NetworkGameManager.Instance.RequestPlaceDieServerRpc(realRow, realCol);
            return;
        }

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
        if (childSpriteRenderer != null && gapPenaltySprite != null)
        {
            // 1. Detenemos cualquier efecto de respiración o brillo que pudiera estar activo
            childSpriteRenderer.DOKill();

            // 2. Cambiamos el sprite físico al rojo que asignaste en el Inspector
            childSpriteRenderer.sprite = gapPenaltySprite;

            // 3. Restauramos el color a Blanco (White) con 100% de opacidad. 
            // NOTA: En Unity, poner el SpriteRenderer en Color.white asegura que 
            // el sprite se vea con sus colores originales (Rojo) sin tintes extraños.
            childSpriteRenderer.color = Color.white;

            // 4. (Opcional - UX Premium) Un pequeño efecto de "latido de error" al aparecer
            childSpriteRenderer.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.5f, 5, 1f);
        }
        else
        {
            Debug.LogWarning("CellComponent: No has asignado el gapPenaltySprite en el Inspector.");
        }
    }
}