using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private GameManager gm;


    public int CurrentPlayerIndex { get; private set; } = 0;
    
    // =========================================================================
    // PROPIEDAD INTELIGENTE PUNTO DE NO RETORNO
    // El historial solo se borra cuando el jugador decide robar un nuevo dado.
    // =========================================================================
    private bool hasDrawn = false;
    public bool HasDrawn 
    { 
        get => hasDrawn; 
        set 
        {
            hasDrawn = value;
            // F1.5: Sincronizar con el estado maestro
            if (gm.ServerState != null)
                gm.ServerState.HasDrawn = value;

            if (hasDrawn && CommandManager.Instance != null)
            {
                // Al robar un nuevo dado, confirmas tu tablero pasado y se borra la pila vieja
                CommandManager.Instance.ClearHistory(CurrentPlayerIndex);
                Debug.Log($"[Fase de Turno] Dado extraído. Historial previo del Jugador {CurrentPlayerIndex + 1} limpiado.");
            }
        }
    }
    public DieColor CurrentDrawnColor { get; set; }
    public int CurrentDrawnValue { get; set; }

    public TurnManager(GameManager gm)
    {
        this.gm = gm;
    }

    public PlayerData GetCurrentPlayer()
    {
        if (gm.players == null || gm.players.Count == 0) return null;
        return gm.players[CurrentPlayerIndex];
    }


    public void StartTurn()
    {
        // Si la partida ya terminó, no iniciamos nuevos turnos
        if (gm.isGameOver) return;

        PlayerData currentPlayer = GetCurrentPlayer();
        if (currentPlayer == null) return;

        UIManager.Instance.SetDrawInputLock(false);

        if (currentPlayer.isBot)
        {
            UIManager.Instance.SetDrawInputLock(true);
            Debug.Log($"Turno de ML-Agent ({currentPlayer.name}). Esperando red neuronal...");
        }
        else
        {
            UIManager.Instance.SetDrawInputLock(false);
        }

        if (gm.reDrawButton != null) gm.reDrawButton.interactable = false;

        // Desbloqueamos dinámicamente el botón Move según los usos de este jugador
        if (GridInteractionManager.Instance != null)
        {
            GridInteractionManager.Instance.OnTurnChanged(CurrentPlayerIndex);
        }

        // =========================================================
        // NUEVO: ACTUALIZACIÓN DEL BOTÓN DE UNDO Y SU CONTADOR
        // =========================================================
        if (CommandManager.Instance != null)
        {
            CommandManager.Instance.RefreshCommandUI();
        }
        // =========================================================

        // BLINDAJE VISUAL: Si la bolsa se vació 
        if (gm.diceManager.diceBag.Count == 0 && !HasDrawn)
        {
            if (gm.drawButton != null)
            {
                gm.drawButton.interactable = false;
                TMPro.TextMeshProUGUI drawButtonText = gm.drawButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (drawButtonText != null) drawButtonText.text = "Match End";
            }
        }
        else
        {
            // Restauración normal
            if (gm.drawButton != null && !HasDrawn)
            {
                gm.drawButton.interactable = true;
                TMPro.TextMeshProUGUI drawButtonText = gm.drawButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (drawButtonText != null) drawButtonText.text = "EXTRACT DICE";
            }
        }
    }

    // Online: el turno lo decide el servidor; solo copiamos su estado (sin efectos de UI ni guardado)
    public void SyncFromServer(int currentPlayerIndex, bool serverHasDrawn, DieColor drawnColor, int drawnValue)
    {
        CurrentPlayerIndex = currentPlayerIndex;
        hasDrawn = serverHasDrawn;
        CurrentDrawnColor = drawnColor;
        CurrentDrawnValue = drawnValue;
    }

    public void EndTurn()
    {
        
        // Al finalizar el turno solo apagamos la bandera, no destruimos datos.
        hasDrawn = false;

        // =========================================================
        // PROTOCOLO DE SALTO DE TURNO (Ignorar Tableros Muertos)
        // =========================================================
        int startIndex = CurrentPlayerIndex;

        do
        {
            // Avanzamos al siguiente jugador
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % gm.numPlayers;

            // F1.6: Sincronizar con el estado maestro
            if (gm.ServerState != null)
                gm.ServerState.CurrentPlayerIndex = CurrentPlayerIndex;

            // Si dimos toda la vuelta y llegamos al mismo jugador, y también está muerto:
            if (CurrentPlayerIndex == startIndex && gm.players[CurrentPlayerIndex].isEliminated)
            {
                Debug.LogWarning("[TurnManager] Todos los jugadores han sido eliminados. Forzando fin de partida.");
                gm.EndMatch();
                return;
            }

        } while (gm.players[CurrentPlayerIndex].isEliminated);
        // =========================================================


        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveLocal();
        }

        Debug.Log($"Turno finalizado. Ahora es el turno del Jugador {CurrentPlayerIndex + 1}");

        // FORZAMOS a que el GridManager vuelva a mostrar el tablero del jugador que ahora tiene el turno
        if (gm.gridManager != null)
        {
            gm.gridManager.SwitchViewTo(CurrentPlayerIndex);
        }

        UIManager.Instance.SetDrawInputLock(false);

        // LEVANTAMOS EL ESCUDO ANTI-EXPLOIT DE TRANSICIÓN
        if (CommandManager.Instance != null)
        {
            CommandManager.Instance.isTransitioning = false;
        }

        StartTurn();
    }

    public IEnumerator TurnTransitionPause()
    {
        float tiempoDeEspera = (gm.numPlayers == 1) ? 1.0f : 1.5f;
        yield return new WaitForSeconds(tiempoDeEspera);
        EndTurn();
    }
}