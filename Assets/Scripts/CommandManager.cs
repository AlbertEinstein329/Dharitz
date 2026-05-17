using UnityEngine;
using System.Collections.Generic;

public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance { get; private set; }

    private Stack<IGridCommand> commandHistory = new Stack<IGridCommand>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterCommand(IGridCommand command)
    {
        commandHistory.Push(command);
    }

    public void UndoLastCommand()
    {
        if (commandHistory.Count == 0)
        {
            Debug.LogWarning("No hay comandos para deshacer.");
            return;
        }

        GameManager gm = GameManager.Instance;
        PlayerData p = gm.turnManager.GetCurrentPlayer();

        // 1. Verificación de reglas de Undo (Límites de Uso)
        if (p.currentUndoUses <= 0)
        {
            Debug.LogWarning("Undo limit reached.");
            return;
        }

        // 2. Verificación de Fase de Turno (en Free Play no se puede hacer Undo si ya robó el siguiente dado, a menos que no haya robado, pero wait,
        // The rule says "if the player draws a die in the current turn before executing Undo, the command is locked until the transition to the next formal round."
        // Wait, "Undo" is for the LAST placed die. If they draw a new die, the turn phase is "HasDrawn = true". We can block it.
        if (gm.turnManager.HasDrawn && !gm.currentSession.isCampaignMode)
        {
            Debug.LogWarning("Undo locked: Ya has robado un nuevo dado este turno.");
            return;
        }

        IGridCommand command = commandHistory.Pop();
        command.Undo();
        p.currentUndoUses--;

        Debug.Log($"Undo exitoso. Usos restantes: {p.currentUndoUses}");
    }

    public void ExecuteCommand(IGridCommand command)
    {
        // For MoveCommand, we execute and push.
        command.Execute();
        commandHistory.Push(command);
    }

    public void ClearHistory()
    {
        commandHistory.Clear();
    }

    // --- PREMIUM CURRENCY / ABILITY UPGRADES ---
    // Diferimos la persistencia en disco hasta el final del turno, operando en memoria local (SaveManager.CurrentProfile).
    
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
            // Operación síncrona en memoria, sin escribir a JSON inmediatamente (cero lag)
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
