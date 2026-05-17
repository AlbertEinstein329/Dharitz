using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private GameManager gm;

    public int CurrentPlayerIndex { get; private set; } = 0;
    public bool HasDrawn { get; set; } = false;
    public DieColor CurrentDrawnColor { get; set; }

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
    }

    public void EndTurn()
    {
        HasDrawn = false;
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % gm.numPlayers;

        if (SaveManager.Instance != null)
        {
            // Diferimos la escritura a disco JSON hasta este punto del bucle de juego
            // para evitar tirones (stuttering) en móviles durante la colocación de piezas.
            SaveManager.Instance.SaveLocal();
        }

        Debug.Log($"Turno finalizado. Ahora es el turno del Jugador {CurrentPlayerIndex + 1}");

        if (gm.gridManager != null)
        {
            gm.gridManager.SwitchViewTo(CurrentPlayerIndex);
        }

        UIManager.Instance.SetDrawInputLock(false);
        StartTurn();
    }

    public IEnumerator TurnTransitionPause()
    {
        float tiempoDeEspera = (gm.numPlayers == 1) ? 0f : 1.5f;

        if (tiempoDeEspera > 0f)
        {
            yield return new WaitForSeconds(tiempoDeEspera);
        }

        EndTurn();
    }
}
