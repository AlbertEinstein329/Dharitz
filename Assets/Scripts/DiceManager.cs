using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MyGame.Core;

public class DiceManager
{
    private GameManager gm;

    // La bolsa visual LOCAL es un mirror del ServerState.DiceBag.
    // En modo online, el servidor es la única fuente de verdad.
    // En modo local (hotseat), el GameManager.ServerState es la autoridad.
    public List<DieColor> diceBag => gm.ServerState.DiceBag
        .Select(c => (DieColor)(int)c)
        .ToList();

    public DiceManager(GameManager gm)
    {
        this.gm = gm;
    }

    public void InitializeBag()
    {
        int dicePerColor = 13 * gm.numPlayers;

        // F1.1: Inicializamos directamente en ServerState.DiceBag (fuente de verdad única)
        gm.ServerState.DiceBag = new List<MyGame.Core.DieColor>();

        for (int i = 0; i < dicePerColor; i++)
        {
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.Red);
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.Blue);
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.White);
            gm.ServerState.DiceBag.Add(MyGame.Core.DieColor.Black);
        }

        ShuffleBag();
        Debug.Log($"Bolsa creada con {gm.ServerState.DiceBag.Count} dados para {gm.numPlayers} players.");

        UpdateDiceCountersUI();
    }

    private void ShuffleBag()
    {
        // F1.4: Usar ServerState.ServerRNG en lugar de new System.Random() local
        System.Random rng = gm.ServerState.ServerRNG;

        // Fisher-Yates con el RNG autoritativo
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
        var bag = gm.ServerState.DiceBag;
        int redLeft   = bag.Count(d => d == MyGame.Core.DieColor.Red);
        int blueLeft  = bag.Count(d => d == MyGame.Core.DieColor.Blue);
        int whiteLeft = bag.Count(d => d == MyGame.Core.DieColor.White);
        int blackLeft = bag.Count(d => d == MyGame.Core.DieColor.Black);

        UIManager.Instance.UpdateDiceCounters(redLeft, blueLeft, whiteLeft, blackLeft);
    }

    public void DrawDie()
    {
        if (gm.ServerState.DiceBag == null || gm.ServerState.DiceBag.Count == 0)
        {
            Debug.Log("Bolsa vacía. ¡Es tu último movimiento!");
            if (gm.drawButton != null) gm.drawButton.interactable = false;
            return;
        }

        if (gm.turnManager.HasDrawn || gm.players == null || gm.players.Count == 0) return;

        // =============================================
        // F1.1: Usamos GameManager.Instance.ServerState directamente.
        // Ya NO creamos un MatchStateDTO efímero local.
        // =============================================
        var command = new DrawDieCommand { PlayerId = gm.turnManager.CurrentPlayerIndex };
        PlayerData currentPlayer = gm.turnManager.GetCurrentPlayer();

        // Sincronizamos el perfil del jugador actual en ServerState antes del procesado
        gm.ServerState.PlayerProfiles[command.PlayerId] = currentPlayer.ToDTO();

        // F1.4: Usar ServerState.ServerRNG (el único RNG autoritativo)
        bool isLegalDraw = CoreDrawProcessor.ProcessDrawIntent(gm.ServerState, command, gm.ServerState.ServerRNG);

        if (!isLegalDraw)
        {
            Debug.LogError("[Security] CoreDrawProcessor rechazó el robo.");
            return;
        }

        // =============================================
        // SINCRONIZACIÓN VISUAL (Capa 0 -> Capa 1)
        // =============================================
        if (gm.drawButton != null) gm.drawButton.interactable = false;

        // F1.5: HasDrawn se lee/escribe desde ServerState
        gm.turnManager.HasDrawn = gm.ServerState.HasDrawn;
        gm.turnManager.CurrentDrawnColor = (DieColor)(int)gm.ServerState.CurrentDrawnColor.Value;
        gm.turnManager.CurrentDrawnValue = gm.ServerState.CurrentDrawnValue.Value;

        PlayerDataDTO pDto = gm.ServerState.PlayerProfiles[command.PlayerId];

        // Sincronizamos los grupos activos en el MonoBehaviour visual
        DieColor drawnColor = gm.turnManager.CurrentDrawnColor;
        if (!currentPlayer.activeGroups.ContainsKey(drawnColor))
        {
            currentPlayer.activeGroups[drawnColor] = null;
        }

        var serverGroup = pDto.ActiveGroups[(MyGame.Core.DieColor)(int)drawnColor];
        if (currentPlayer.activeGroups[drawnColor] == null || currentPlayer.activeGroups[drawnColor].isClosed)
        {
            currentPlayer.activeGroups[drawnColor] = new GroupData
            {
                id = serverGroup.Id,
                color = drawnColor,
                targetSize = serverGroup.TargetSize
            };
        }

        GroupData visualGroup = currentPlayer.activeGroups[drawnColor];

        // 4. ACTUALIZACIÓN DE UI
        UIManager.Instance.UpdateHandUI(visualGroup.color, visualGroup.targetSize, visualGroup.occupiedCells.Count, visualGroup.targetSize, () =>
        {
            UpdateDiceCountersUI();

            if (!currentPlayer.isBot)
            {
                gm.gridManager.ShowValidMoves(gm.turnManager.CurrentPlayerIndex, gm.turnManager.CurrentDrawnColor, visualGroup.id, visualGroup.targetSize);
                if (gm.reDrawButton != null) gm.reDrawButton.interactable = (currentPlayer.reDraws > 0);
                if (gm.reDrawText != null) gm.reDrawText.text = $"{currentPlayer.reDraws}";
            }
        });
    }

    public void UseReDraw()
    {
        PlayerData currentPlayer = gm.turnManager.GetCurrentPlayer();
        if (!gm.turnManager.HasDrawn || currentPlayer.reDraws <= 0 || currentPlayer.isBot) return;

        // =============================================
        // F1.2: Usamos GameManager.Instance.ServerState directamente.
        // =============================================
        var command = new ReDrawCommand { PlayerId = gm.turnManager.CurrentPlayerIndex };

        // Sincronizamos estado actual antes de procesar
        gm.ServerState.HasDrawn = gm.turnManager.HasDrawn;
        gm.ServerState.CurrentDrawnColor = (MyGame.Core.DieColor)(int)gm.turnManager.CurrentDrawnColor;
        gm.ServerState.PlayerProfiles[command.PlayerId] = currentPlayer.ToDTO();

        // F1.4: Usar ServerState.ServerRNG
        bool isLegalReDraw = CoreDrawProcessor.ProcessReDrawIntent(gm.ServerState, command, gm.ServerState.ServerRNG);

        if (!isLegalReDraw)
        {
            Debug.LogError("[Security] CoreDrawProcessor rechazó el ReDraw.");
            return;
        }

        // SINCRONIZACIÓN VISUAL (Capa 0 -> Capa 1)
        PlayerDataDTO pDto = gm.ServerState.PlayerProfiles[command.PlayerId];
        currentPlayer.reDraws = pDto.ReDraws;

        // Limpiar el grupo visual localmente si el servidor lo eliminó
        DieColor color = gm.turnManager.CurrentDrawnColor;
        if (!pDto.ActiveGroups.ContainsKey((MyGame.Core.DieColor)(int)color))
        {
            currentPlayer.activeGroups[color] = null;
        }

        gm.turnManager.HasDrawn = false;
        gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);

        // Disparamos un nuevo Draw automáticamente
        DrawDie();
    }

    public int GetTotalDiceLeft()
    {
        return gm.ServerState.DiceBag != null ? gm.ServerState.DiceBag.Count : 0;
    }

    public void BurnRandomDice(int count)
    {
        int burnedCount = 0;
        var bag = gm.ServerState.DiceBag;
        // F1.4: Usar ServerState.ServerRNG
        System.Random rng = gm.ServerState.ServerRNG;

        for (int i = 0; i < count; i++)
        {
            if (bag.Count > 0)
            {
                int randomIndex = rng.Next(0, bag.Count);
                bag.RemoveAt(randomIndex);
                burnedCount++;
            }
        }
        Debug.Log($"[Muerte Súbita] Se han quemado {burnedCount} dados de la bolsa.");
    }
}
