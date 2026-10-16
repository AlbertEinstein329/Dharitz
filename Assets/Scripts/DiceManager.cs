using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MyGame.Core;

public class DiceManager
{
    private GameManager gm;

    // La bolsa vive en gm.ServerState.DiceBag (fuente de verdad única).
    // En modo online el servidor es la autoridad; en local (hotseat / ML) lo es el GameManager.

    public DiceManager(GameManager gm)
    {
        this.gm = gm;
    }

    public void InitializeBag()
    {
        int dicePerColor = 13 * gm.numPlayers;

        // F1.1: Inicializamos directamente en ServerState.DiceBag
        gm.ServerState.DiceBag = new List<MyGame.Core.DieColor>();

        for (int i = 0; i < dicePerColor; i++)
        {
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.Red);
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.Blue);
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.White);
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.Black);
        }

        ShuffleBag();

        // Bloqueo estricto
        if (!gm.isTrainingMode)
        {
            Debug.Log($"Bolsa creada con {gm.ServerState.DiceBag.Count} dados para {gm.numPlayers} players.");
        }

        UpdateDiceCountersUI();
    }

    private void ShuffleBag()
    {
        // F1.4: Fisher-Yates con el RNG autoritativo del ServerState
        System.Random rng = gm.ServerState.ServerRNG;
        var bag = gm.ServerState.DiceBag;
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            MyGame.Core.DieColor temp = bag[i];
            bag[i] = bag[j];
            bag[j] = temp;
        }
    }

    public void UpdateDiceCountersUI()
    {
        if (gm.isTrainingMode) return; // BYPASS: No actualiza elementos UI de Canvas en modo IA

        var bag = gm.ServerState.DiceBag;
        int redLeft   = bag.Count(d => d == MyGame.Core.DieColor.Red);
        int blueLeft  = bag.Count(d => d == MyGame.Core.DieColor.Blue);
        int whiteLeft = bag.Count(d => d == MyGame.Core.DieColor.White);
        int blackLeft = bag.Count(d => d == MyGame.Core.DieColor.Black);

        UIManager.Instance.UpdateDiceCounters(redLeft, blueLeft, whiteLeft, blackLeft);
    }

    public void DrawDie()
    {
        if (GetTotalDiceLeft() == 0)
        {
            if (!gm.isTrainingMode) Debug.Log("Bolsa vacía. ¡Es tu último movimiento!");
            if (gm.drawButton != null) gm.drawButton.interactable = false;
            return;
        }

        if (gm.turnManager.HasDrawn) return;
        if (gm.players == null || gm.players.Count == 0) return;

        // F1.1: La intención se procesa en la Capa 0 sobre el ServerState
        var command = new DrawDieCommand { PlayerId = gm.turnManager.CurrentPlayerIndex };
        PlayerData currentPlayer = gm.turnManager.GetCurrentPlayer();

        // Sincronizamos el perfil del jugador actual antes del procesado
        gm.ServerState.CurrentPlayerIndex = command.PlayerId;
        gm.ServerState.PlayerProfiles[command.PlayerId] = currentPlayer.ToDTO();

        bool isLegalDraw = CoreDrawProcessor.ProcessDrawIntent(gm.ServerState, command, gm.ServerState.ServerRNG);
        if (!isLegalDraw)
        {
            Debug.LogError("[Security] CoreDrawProcessor rechazó el robo.");
            return;
        }

        // SINCRONIZACIÓN VISUAL (Capa 0 -> Capa 1)
        if (gm.drawButton != null) gm.drawButton.interactable = false;

        gm.turnManager.CurrentDrawnColor = (DieColor)(int)gm.ServerState.CurrentDrawnColor.Value;
        gm.turnManager.CurrentDrawnValue = gm.ServerState.CurrentDrawnValue.Value;
        gm.turnManager.HasDrawn = true;

        DieColor drawnColor = gm.turnManager.CurrentDrawnColor;
        GroupDataDTO serverGroup = gm.ServerState.PlayerProfiles[command.PlayerId].ActiveGroups[(MyGame.Core.DieColor)(int)drawnColor];

        currentPlayer.activeGroups.TryGetValue(drawnColor, out GroupData group);
        if (group == null || group.isClosed)
        {
            group = new GroupData
            {
                id = serverGroup.Id,
                color = drawnColor,
                targetSize = serverGroup.TargetSize
            };
            currentPlayer.activeGroups[drawnColor] = group;
        }

        if (gm.isTrainingMode)
        {
            // Retorno inmediato: el motor de reglas ya tiene los datos, no necesitamos callbacks visuales
            return;
        }

        UIManager.Instance.UpdateHandUI(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize, () =>
        {
            UpdateDiceCountersUI();

            if (!currentPlayer.isBot)
            {
                gm.gridManager.ShowValidMoves(gm.turnManager.CurrentPlayerIndex, gm.turnManager.CurrentDrawnColor, group.id, group.targetSize);

                if (gm.reDrawButton != null)
                {
                    gm.reDrawButton.interactable = (currentPlayer.reDraws > 0);
                }

                if (gm.reDrawText != null)
                {
                    gm.reDrawText.text = $"{currentPlayer.reDraws}";
                }
            }
        });
    }

    public void UseReDraw()
    {
        PlayerData p = gm.turnManager.GetCurrentPlayer();

        if (!gm.turnManager.HasDrawn || p.reDraws <= 0 || p.isBot) return;

        // F1.2: Sincronizamos el estado actual y procesamos en la Capa 0
        var command = new ReDrawCommand { PlayerId = gm.turnManager.CurrentPlayerIndex };
        gm.ServerState.HasDrawn = true;
        gm.ServerState.CurrentDrawnColor = (MyGame.Core.DieColor)(int)gm.turnManager.CurrentDrawnColor;
        gm.ServerState.PlayerProfiles[command.PlayerId] = p.ToDTO();

        bool isLegalReDraw = CoreDrawProcessor.ProcessReDrawIntent(gm.ServerState, command, gm.ServerState.ServerRNG);
        if (!isLegalReDraw)
        {
            Debug.LogError("[Security] CoreDrawProcessor rechazó el ReDraw.");
            return;
        }

        // SINCRONIZACIÓN VISUAL (Capa 0 -> Capa 1)
        PlayerDataDTO pDto = gm.ServerState.PlayerProfiles[command.PlayerId];
        p.reDraws = pDto.ReDraws;

        // Si el servidor eliminó el grupo huérfano, lo limpiamos también en la capa visual
        DieColor color = gm.turnManager.CurrentDrawnColor;
        if (!pDto.ActiveGroups.ContainsKey((MyGame.Core.DieColor)(int)color))
        {
            p.activeGroups[color] = null;
        }

        gm.turnManager.HasDrawn = false;
        gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);

        DrawDie();
    }

    public int GetTotalDiceLeft()
    {
        return gm.ServerState.DiceBag != null ? gm.ServerState.DiceBag.Count : 0;
    }

    public void BurnRandomDice(int count)
    {
        var bag = gm.ServerState.DiceBag;
        System.Random rng = gm.ServerState.ServerRNG;

        int burnedCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (bag.Count > 0)
            {
                bag.RemoveAt(rng.Next(0, bag.Count));
                burnedCount++;
            }
        }
        Debug.Log($"[Muerte Súbita] Se han quemado {burnedCount} dados de la bolsa.");

        UpdateDiceCountersUI();
    }
}
