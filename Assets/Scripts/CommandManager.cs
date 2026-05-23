using UnityEngine;
using System.Collections.Generic;

public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance { get; private set; }

    // SOLUCIÓN MULTIJUGADOR: Un historial independiente por cada jugador
    private Dictionary<int, Stack<IGridCommand>> playerCommandStacks = new Dictionary<int, Stack<IGridCommand>>();

    [HideInInspector] public bool isTransitioning = false; // Escudo Anti-Exploit

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Método auxiliar seguro para obtener la pila del jugador actual
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
        int currentPlayer = GameManager.Instance.turnManager.CurrentPlayerIndex;
        GetPlayerStack(currentPlayer).Push(command);
    }

    public void ExecuteCommand(IGridCommand command)
    {
        command.Execute();
        int currentPlayer = GameManager.Instance.turnManager.CurrentPlayerIndex;
        GetPlayerStack(currentPlayer).Push(command);
    }

    public void UndoLastCommand()
    {
        // ESCUDO ANTI-EXPLOIT: Bloqueado durante la animación de 1.5s
        if (isTransitioning)
        {
            Debug.LogWarning("[Anti-Exploit] Undo bloqueado: El turno está en transición.");
            return;
        }

        GameManager gm = GameManager.Instance;
        int currentPlayerIndex = gm.turnManager.CurrentPlayerIndex;
        PlayerData p = gm.players[currentPlayerIndex];

        Stack<IGridCommand> currentStack = GetPlayerStack(currentPlayerIndex);

        if (currentStack.Count == 0)
        {
            Debug.LogWarning($"No hay comandos para deshacer para el Jugador {currentPlayerIndex + 1}.");
            return;
        }

        // 1. Verificación de límites de uso tradicionales
        if (p.currentUndoUses <= 0)
        {
            Debug.LogWarning("Undo limit reached.");
            return;
        }

        // 2. Verificación de Fase de Turno (Bloqueo absoluto)
        if (gm.turnManager.HasDrawn)
        {
            Debug.LogWarning("Undo locked: Ya tienes un dado en la mano. Colócalo primero.");
            return;
        }

        // 3. Ejecución del Undo de la pila del jugador actual
        IGridCommand command = currentStack.Pop();
        command.Undo();
        p.currentUndoUses--;

        Debug.Log($"Undo exitoso para Jugador {currentPlayerIndex + 1}. Usos restantes: {p.currentUndoUses}");

        if (GameManager.Instance != null && GameManager.Instance.placementOrchestrator != null)
        {
            GameManager.Instance.placementOrchestrator.RefreshPlacementHighlights();
        }
    }

    public void ClearHistory(int playerIndex)
    {
        if (playerCommandStacks.ContainsKey(playerIndex))
        {
            playerCommandStacks[playerIndex].Clear();
        }
    }

    // --- LOGICA DE MEJORAS Y TIENDA INTACTA ---
    public bool TryBuyUndoUpgrade(int cost)
    {
        GameManager gm = GameManager.Instance;
        if (!gm.currentSession.isCampaignMode) return false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        if (p.currentUndoUses >= gm.currentSession.baseUndoUses + 1)
        {
            Debug.LogWarning("Undo Upgrade limit reached (max 3).");
            return false;
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