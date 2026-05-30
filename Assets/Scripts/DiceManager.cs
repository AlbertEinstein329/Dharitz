using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DiceManager
{
    private GameManager gm;
    public List<DieColor> diceBag = new List<DieColor>();

    public DiceManager(GameManager gm)
    {
        this.gm = gm;
    }

    public void InitializeBag()
    {
        diceBag.Clear();
        int dicePerColor = 13 * gm.numPlayers;

        for (int i = 0; i < dicePerColor; i++)
        {
            diceBag.Add(DieColor.Red);
            diceBag.Add(DieColor.Blue);
            diceBag.Add(DieColor.White);
            diceBag.Add(DieColor.Black);
        }

        ShuffleBag();
        Debug.Log($"Bolsa creada con {diceBag.Count} dados para {gm.numPlayers} players.");

        UpdateDiceCountersUI();
    }

    private void ShuffleBag()
    {
        for (int i = 0; i < diceBag.Count; i++)
        {
            DieColor temp = diceBag[i];
            int randomIndex = Random.Range(i, diceBag.Count);
            diceBag[i] = diceBag[randomIndex];
            diceBag[randomIndex] = temp;
        }
    }

    public void UpdateDiceCountersUI()
    {
        int redLeft = diceBag.Count(d => d == DieColor.Red);
        int blueLeft = diceBag.Count(d => d == DieColor.Blue);
        int whiteLeft = diceBag.Count(d => d == DieColor.White);
        int blackLeft = diceBag.Count(d => d == DieColor.Black);

        UIManager.Instance.UpdateDiceCounters(redLeft, blueLeft, whiteLeft, blackLeft);
    }

    public void DrawDie()
    {
        if (diceBag == null || diceBag.Count == 0)
        {
            Debug.Log("Bolsa vacía. ¡Es tu último movimiento!");
            if (gm.drawButton != null) gm.drawButton.interactable = false;
            return;
        }

        if (gm.turnManager.HasDrawn) return;
        if (gm.players == null || gm.players.Count == 0) return;

        gm.turnManager.CurrentDrawnColor = diceBag[0];
        diceBag.RemoveAt(0);
        gm.turnManager.HasDrawn = true;

        if (gm.drawButton != null) gm.drawButton.interactable = false;

        PlayerData currentPlayer = gm.turnManager.GetCurrentPlayer();

        if (!currentPlayer.activeGroups.ContainsKey(gm.turnManager.CurrentDrawnColor))
        {
            currentPlayer.activeGroups[gm.turnManager.CurrentDrawnColor] = null;
        }

        GroupData group = currentPlayer.activeGroups[gm.turnManager.CurrentDrawnColor];

        if (group == null || group.isClosed)
        {


            // Roll the number and save it globally in the TurnManager
            gm.turnManager.CurrentDrawnValue = Random.Range(1, 7);

            group = new GroupData
            {
                id = Random.Range(10000, 99999),
                color = gm.turnManager.CurrentDrawnColor,
                // Use the global value here
                targetSize = gm.turnManager.CurrentDrawnValue
            };
            currentPlayer.activeGroups[gm.turnManager.CurrentDrawnColor] = group;
        }
        else
        {
            // The drawn value must reflect the target size of the ongoing group!
            gm.turnManager.CurrentDrawnValue = group.targetSize;
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
            else
            {
                // Bot ML agents logic goes here
            }
        });
    }

    public void UseReDraw()
    {
        PlayerData p = gm.turnManager.GetCurrentPlayer();

        if (!gm.turnManager.HasDrawn || p.reDraws <= 0 || p.isBot) return;

        if (p.activeGroups.ContainsKey(gm.turnManager.CurrentDrawnColor))
        {
            GroupData group = p.activeGroups[gm.turnManager.CurrentDrawnColor];
            if (group != null && group.occupiedCells.Count == 0)
            {
                p.activeGroups[gm.turnManager.CurrentDrawnColor] = null; 
            }
        }

        int randomIndex = Random.Range(0, diceBag.Count + 1);
        diceBag.Insert(randomIndex, gm.turnManager.CurrentDrawnColor);

        p.reDraws--;
        gm.turnManager.HasDrawn = false;

        gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);

        DrawDie();
    }

    public int GetTotalDiceLeft()
    {
        return diceBag != null ? diceBag.Count : 0;
    }

    public void BurnRandomDice(int count)
    {
        int burnedCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (diceBag.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, diceBag.Count);
                diceBag.RemoveAt(randomIndex);
                burnedCount++;
            }
        }
        Debug.Log($"[Muerte Súbita] Se han quemado {burnedCount} dados de la bolsa.");

        // (Opcional) Si tienes un método para actualizar la UI del contador global, llámalo aquí
        // UIManager.Instance.UpdateDiceCounters(); 
    }
}
