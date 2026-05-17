using UnityEngine;

public class GridInteractionManager : MonoBehaviour
{
    public static GridInteractionManager Instance { get; private set; }

    [SerializeField] private BoardLogic boardLogic;
    [SerializeField] private MoveActionButton moveButton;

    private bool isMoveModeActive = false;
    private Vector2Int? selectedDiceCoordinate = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetMoveModeActive(bool active)
    {
        isMoveModeActive = active;
        if (!active)
        {
            ClearHighlights();
            selectedDiceCoordinate = null;
        }
    }

    // Lógica de interacción (ejemplo simplificado de clic en el tablero)
    public void HandleCellClick(Vector2Int clickedCoordinate)
    {
        if (!isMoveModeActive) return;

        // Paso 1: Seleccionar el dado a mover
        if (selectedDiceCoordinate == null)
        {
            if (boardLogic.occupiedCells.Contains(clickedCoordinate))
            {
                // ¡REGLA CRÍTICA! Validar si el dado rompería la isla ANTES de dejarlo seleccionar
                if (boardLogic.WouldLeaveSplitIslands(clickedCoordinate))
                {
                    Debug.LogWarning("Acción inválida: Este movimiento dividiría el tablero en 2 islas.");
                    return;
                }

                selectedDiceCoordinate = clickedCoordinate;
                HighlightValidDestinationsFor(clickedCoordinate);
            }
        }
        else
        {
            // Paso 2: Procesar el movimiento a la celda destino vacía
            Vector2Int destination = clickedCoordinate;
            if (!boardLogic.occupiedCells.Contains(destination))
            {
                ExecuteMovement(selectedDiceCoordinate.Value, destination);
            }
        }
    }

    private void HighlightValidDestinationsFor(Vector2Int diceCoordinate)
    {
        // Aquí mandarías a llamar a tus celdas visuales del tablero
        // que cumplan con la regla de supervivencia y mismo color de grupos vecinos.
        Debug.Log($"Creando Highlights en el tablero para el dado en: {diceCoordinate}");
    }

    private void ExecuteMovement(Vector2Int from, Vector2Int to)
    {
        // Trasladar la data en BoardLogic
        boardLogic.occupiedCells.Remove(from);
        boardLogic.occupiedCells.Add(to);

        // Limpiar estados e interfaz
        ClearHighlights();
        selectedDiceCoordinate = null;
        if (moveButton != null) moveButton.ResetButtonState();
    }

    private void ClearHighlights()
    {
        Debug.Log("Limpiando los Highlights del tablero.");
    }
}