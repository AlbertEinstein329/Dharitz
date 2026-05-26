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
    }

    public void EndTurn()
    {
        // Al finalizar el turno solo apagamos la bandera, no destruimos datos.
        hasDrawn = false; 
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % gm.numPlayers;

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