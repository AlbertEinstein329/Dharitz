using UnityEngine;
using System.Collections.Generic;

public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance { get; private set; }

    // EL NÚCLEO MULTIJUGADOR: Un diccionario que separa el historial por jugador
    private Dictionary<int, Stack<IGridCommand>> playerCommandStacks = new Dictionary<int, Stack<IGridCommand>>();

    [HideInInspector] public bool isTransitioning = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Método auxiliar privado para crear o devolver la pila correcta sin generar errores
    private Stack<IGridCommand> GetPlayerStack(int playerIndex)
    {
        if (!playerCommandStacks.ContainsKey(playerIndex))
        {
            playerCommandStacks[playerIndex] = new Stack<IGridCommand>();
        }
        return playerCommandStacks[playerIndex];
    }

    public void RegisterCommand(IGridCommand command)
    {
        // Enrutamiento automático al jugador activo
        int currentPlayer = GameManager.Instance.turnManager.CurrentPlayerIndex;
        GetPlayerStack(currentPlayer).Push(command);
    }

    public void ExecuteCommand(IGridCommand command)
    {
        // Ejecuta y guarda en la pila del jugador activo
        int currentPlayer = GameManager.Instance.turnManager.CurrentPlayerIndex;
        command.Execute();
        GetPlayerStack(currentPlayer).Push(command);
    }

    public void UndoLastCommand()
    {
        // ESCUDO ANTI-EXPLOIT
        if (isTransitioning)
        {
            Debug.LogWarning("[Anti-Exploit] Undo bloqueado: El turno está en transición.");
            return;
        }

        GameManager gm = GameManager.Instance;
        PlayerData p = gm.turnManager.GetCurrentPlayer();
        int currentPlayer = gm.turnManager.CurrentPlayerIndex;

        Stack<IGridCommand> currentStack = GetPlayerStack(currentPlayer);

        if (currentStack.Count == 0)
        {
            Debug.LogWarning($"No hay comandos para deshacer para el Jugador {currentPlayer}.");
            return;
        }

        // 1. Verificación de reglas de Undo (Límites de Uso) - TU LÓGICA INTACTA
        if (p.currentUndoUses <= 0)
        {
            Debug.LogWarning("Undo limit reached.");
            return;
        }

        // 2. Verificación de Fase de Turno - TU LÓGICA INTACTA
        if (gm.turnManager.HasDrawn && !gm.currentSession.isCampaignMode)
        {
            Debug.LogWarning("Undo locked: Ya has robado un nuevo dado este turno.");
            return;
        }

        // 3. Ejecución del Undo Aislado
        IGridCommand command = currentStack.Pop();
        command.Undo();
        p.currentUndoUses--;

        Debug.Log($"Undo exitoso. Usos restantes del Jugador {currentPlayer}: {p.currentUndoUses}");

        if (GameManager.Instance != null && GameManager.Instance.placementOrchestrator != null)
        {
            GameManager.Instance.placementOrchestrator.RefreshPlacementHighlights();
        }
    }

    /// <summary>
    /// Limpia el historial del jugador indicado (para llamar al final de su turno)
    /// </summary>
    public void ClearHistory(int playerIndex)
    {
        if (playerCommandStacks.ContainsKey(playerIndex))
        {
            playerCommandStacks[playerIndex].Clear();
        }
    }

    // --- PREMIUM CURRENCY / ABILITY UPGRADES (TUS FUNCIONES INTACTAS) ---

    public bool TryBuyUndoUpgrade(int cost)
    {
        GameManager gm = GameManager.Instance;
        if (!gm.currentSession.isCampaignMode) return false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        if (p.currentUndoUses >= gm.currentSession.baseUndoUses + 1)
        {
            Debug.LogWarning("Undo Upgrade limit reached (max 3).");
            return false; // Máximo alcanzado
        }

        if (SaveManager.Instance.CurrentProfile.totalCoins >= cost)
        {
            SaveManager.Instance.CurrentProfile.totalCoins -= cost;
            p.currentUndoUses++;
            Debug.Log($"Undo Upgrade comprado. Usos ahora: {p.currentUndoUses}");
            return true;
        }

        return false;
    }

    public bool TryBuyMoveUpgrade(int cost)
    {
        GameManager gm = GameManager.Instance;
        if (!gm.currentSession.isCampaignMode) return false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        if (p.currentMoveUses >= gm.currentSession.baseMoveUses + 1)
        {
            Debug.LogWarning("Move Upgrade limit reached (max 2).");
            return false;
        }

        if (SaveManager.Instance.CurrentProfile.totalCoins >= cost)
        {
            SaveManager.Instance.CurrentProfile.totalCoins -= cost;
            p.currentMoveUses++;
            Debug.Log($"Move Upgrade comprado. Usos ahora: {p.currentMoveUses}");
            return true;
        }

        return false;
    }
}