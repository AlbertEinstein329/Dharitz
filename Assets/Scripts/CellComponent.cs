using UnityEngine;
using static PlayerData;

public class CellComponent : MonoBehaviour
{
    private int row;
    private int col;
    private int playerOwnerIndex;
    private GridManager gridManager;

    // --- NUEVAS VARIABLES PARA EL RESALTADO ---
    private SpriteRenderer spriteRenderer;
    private Color colorOriginal;

    void Awake()
    {
        // Capturamos el renderer y su color original al iniciar
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            colorOriginal = spriteRenderer.color;
        }
    }

    public void Setup(int r, int c, int pIndex, GridManager gm)
    {
        row = r;
        col = c;
        playerOwnerIndex = pIndex;
        gridManager = gm;
    }

    // --- NUEVA FUNCIÓN ---
    public void SetHighlight(bool highlight)
    {
        if (spriteRenderer == null) return;

        if (highlight)
        {
            // Cambia este color por el que prefieras (ej. un verde semi-transparente)
            spriteRenderer.color = new Color(0.5f, 1f, 0.5f, 1f);
        }
        else
        {
            spriteRenderer.color = colorOriginal;
        }
    }

    void OnMouseDown()
    {
        if (GameManager.Instance.currentPlayerIndex != playerOwnerIndex)
        {
            Debug.Log("¡Este no es tu tablero o no es tu turno!");
            return;
        }

        if (!GameManager.Instance.hasDrawn) return;

        DieColor colorActual = GameManager.Instance.currentDrawnColor;
        PlayerData jugadorActual = GameManager.Instance.players[GameManager.Instance.currentPlayerIndex];

        if (jugadorActual.activeGroups.ContainsKey(colorActual))
        {
            GroupData grupo = jugadorActual.activeGroups[colorActual];

            if (grupo != null && grupo.targetSize > 0)
            {
                if (gridManager.CanBotPlaceHere(playerOwnerIndex, row, col, colorActual, grupo.id, grupo.targetSize))
                {
                    GameManager.Instance.IniciarColocacion(row, col);
                }
            }
        }
    }
}