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

        // Refrescamos la UI porque ahora hay historial disponible
        RefreshCommandUI();
    }

    public void ExecuteCommand(IGridCommand command)
    {
        command.Execute();
        int currentPlayer = GameManager.Instance.turnManager.CurrentPlayerIndex;
        GetPlayerStack(currentPlayer).Push(command);
    }

    public void UndoLastCommand()
    {
        // ESCUDO ANTI-EXPLOIT: Bloqueado durante la transición
        if (isTransitioning)
        {
            Debug.LogWarning("[Anti-Exploit] Undo bloqueado: El turno está en transición.");
            return;
        }

        GameManager gm = GameManager.Instance;
        int currentPlayerIndex = gm.turnManager.CurrentPlayerIndex;
        PlayerData p = gm.turnManager.GetCurrentPlayer();

        Stack<IGridCommand> currentStack = GetPlayerStack(currentPlayerIndex);

        if (currentStack.Count == 0) return;

        // 1. Verificación predictiva en cliente (para UX rápida), pero la autoridad final es del servidor
        if (p.currentUndoUses <= 0)
        {
            Debug.LogWarning("Undo limit reached.");
            return;
        }

        // 2. Verificación de Fase de Turno
        if (gm.turnManager.HasDrawn)
        {
            Debug.LogWarning("Undo locked: Ya tienes un dado en la mano. Colócalo primero.");
            return;
        }

        // 3. Ejecución del Comando (Se comunicará con la Capa 0, actualizará 'p', y mutará 'currentUndoUses' según el DTO)
        IGridCommand command = currentStack.Pop();
        command.Undo();

        // ELIMINADA LA MUTACIÓN LOCAL: p.currentUndoUses--;

        // 4. Sincronización de UI post-ejecución
        RefreshCommandUI();
    }

    public void RefreshCommandUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.turnManager == null) return;

        int currentPlayer = GameManager.Instance.turnManager.CurrentPlayerIndex;
        var stack = GetPlayerStack(currentPlayer);
        PlayerData p = GameManager.Instance.turnManager.GetCurrentPlayer();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateUndoUI(p.currentUndoUses, stack.Count > 0);
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
    // --- LOGICA DE MEJORAS Y TIENDA (DEGRADADA A TERMINAL) ---
    public bool TryBuyUndoUpgrade(int cost)
    {
        GameManager gm = GameManager.Instance;
        if (!gm.currentSession.isCampaignMode) return false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        int maxAllowed = gm.currentSession.baseUndoUses + 1;

        // 1. ENSAMBLAJE DEL COMANDO DE COMPRA
        var command = new MyGame.Core.BuyUpgradeCommand
        {
            PlayerId = gm.turnManager.CurrentPlayerIndex,
            UpgradeType = "Undo",
            Cost = cost,
            MaxUses = maxAllowed
        };

        // 2. CONSTRUCCIÓN DEL ESTADO AUTORITATIVO
        MyGame.Core.MatchStateDTO serverState = new MyGame.Core.MatchStateDTO();
        serverState.CurrentPhase = MyGame.Core.MatchPhase.PlayerTurn;
        serverState.CurrentPlayerIndex = gm.turnManager.CurrentPlayerIndex;

        MyGame.Core.PlayerDataDTO pDto = p.ToDTO();
        pDto.TotalCoins = SaveManager.Instance.CurrentProfile.totalCoins; // Inyectamos la billetera actual
        serverState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex] = pDto;

        // 3. ENVÍO AL SERVIDOR
        bool isLegalPurchase = MyGame.Core.CoreEconomyProcessor.ProcessBuyIntent(serverState, command);

        if (!isLegalPurchase)
        {
            Debug.LogWarning("[Security] CoreEconomyProcessor rechazó la compra. Fondos insuficientes o límite excedido.");
            return false;
        }

        // 4. SINCRONIZACIÓN LOCAL TRAS APROBACIÓN
        p.currentUndoUses = serverState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex].CurrentUndoUses;
        SaveManager.Instance.CurrentProfile.totalCoins = serverState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex].TotalCoins;

        Debug.Log($"Undo Upgrade validado por el servidor. Usos ahora: {p.currentUndoUses}");
        RefreshCommandUI();
        return true;
    }

    public bool TryBuyMoveUpgrade(int cost)
    {
        GameManager gm = GameManager.Instance;
        if (!gm.currentSession.isCampaignMode) return false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        int maxAllowed = gm.currentSession.baseMoveUses + 1;

        var command = new MyGame.Core.BuyUpgradeCommand
        {
            PlayerId = gm.turnManager.CurrentPlayerIndex,
            UpgradeType = "Move",
            Cost = cost,
            MaxUses = maxAllowed
        };

        MyGame.Core.MatchStateDTO serverState = new MyGame.Core.MatchStateDTO();
        serverState.CurrentPhase = MyGame.Core.MatchPhase.PlayerTurn;
        serverState.CurrentPlayerIndex = gm.turnManager.CurrentPlayerIndex;

        MyGame.Core.PlayerDataDTO pDto = p.ToDTO();
        pDto.TotalCoins = SaveManager.Instance.CurrentProfile.totalCoins;
        serverState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex] = pDto;

        bool isLegalPurchase = MyGame.Core.CoreEconomyProcessor.ProcessBuyIntent(serverState, command);

        if (!isLegalPurchase)
        {
            Debug.LogWarning("[Security] CoreEconomyProcessor rechazó la compra de Move.");
            return false;
        }

        p.currentMoveUses = serverState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex].CurrentMoveUses;
        SaveManager.Instance.CurrentProfile.totalCoins = serverState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex].TotalCoins;

        Debug.Log($"Move Upgrade validado por el servidor. Usos ahora: {p.currentMoveUses}");
        return true;
    }


}