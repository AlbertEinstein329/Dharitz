using UnityEngine;
using MyGame.Core;

public class UndoCommand : IGridCommand
{
    private UndoDieCommand dtoCommand;
    private GameManager gm;

    public UndoCommand(GameManager gm, int playerIndex, int r, int c, DieColor color, int groupId, int targetSize)
    {
        this.gm = gm;
        
        // El comando ahora es un simple transporte de la intención, sin clonar arrays nativos
        this.dtoCommand = new UndoDieCommand
        {
            PlayerId = playerIndex,
            TargetCell = new GridPos(r, c), // Mismo convenio que la colocacion y el nucleo: X=fila, Y=columna
            Color = (MyGame.Core.DieColor)(int)color,
            GroupId = groupId,
            Number = targetSize
        };
    }

    public void Execute()
    {
        // El orquestador ya aplicó la jugada visualmente. El comando solo se encola.
    }

    public bool Undo()
    {
        // 1. ENVIAR INTENCIÓN A LA AUTORIDAD (Usando el estado real del servidor persistente)
        float[] rowMults = ScoreManager.RowMultipliers;
        float[] colMults = ScoreManager.ColMultipliers;

        // FIREWALL: El cliente no crea estados, usa la verdad absoluta (gm.ServerState)
        bool isLegalUndo = CoreUndoProcessor.ProcessUndoIntent(
            gm.ServerState,
            dtoCommand,
            ScoreManager.ROW_COMPLETE_BONUS,
            ScoreManager.COL_COMPLETE_BONUS,
            ScoreManager.INTERSECTION_BONUS,
            rowMults,
            colMults
        );

        // Si el servidor lo rechaza, el cliente aborta toda ejecución visual
        if (!isLegalUndo)
        {
            Debug.LogError("[Security] CoreUndoProcessor rechazó la reversión. Desincronización detectada.");
            return false;
        }

        // 2. SINCRONIZACIÓN VISUAL ESTRICTA (El servidor aceptó y ya mutó el gm.ServerState)
        PlayerData p = gm.players[dtoCommand.PlayerId];
        PlayerDataDTO updatedProfile = gm.ServerState.PlayerProfiles[dtoCommand.PlayerId];

        p.score = updatedProfile.Score;
        p.placedDice = updatedProfile.PlacedDice;
        p.accumulatedStructurePoints = updatedProfile.AccumulatedStructurePoints;
        p.accumulatedOnesPenalty = updatedProfile.AccumulatedOnesPenalty;
        System.Array.Copy(updatedProfile.PatternCounts, p.patternCounts, p.patternCounts.Length);
        p.currentUndoUses = updatedProfile.CurrentUndoUses;

        DieColor visualColor = (DieColor)(int)dtoCommand.Color;

        // Verificamos nulos explícitamente para evitar tu NullReferenceException
        if (p.activeGroups.ContainsKey(visualColor) && p.activeGroups[visualColor] != null)
        {
            p.activeGroups[visualColor].occupiedCells.Remove(new Vector2Int(dtoCommand.TargetCell.X, dtoCommand.TargetCell.Y));
        }

        // Mapeo inverso a la matriz visual (Y = row, X = col)
        gm.gridManager.RemoveDie(dtoCommand.PlayerId, dtoCommand.TargetCell.X, dtoCommand.TargetCell.Y);

        // Si el dado retirado completaba un patron, su resaltado debe desaparecer
        gm.gridManager.RecomputeCompletionOverlays(dtoCommand.PlayerId);

        // 3. RESTAURACIÓN DE LA INTERFAZ
        UIManager.Instance.UpdateScore(p.score);

        if (p.activeGroups.ContainsKey(visualColor) && p.activeGroups[visualColor] != null)
        {
            UIManager.Instance.UpdateProgressText(visualColor, dtoCommand.Number, p.activeGroups[visualColor].occupiedCells.Count, dtoCommand.Number);
        }

        UIManager.Instance.RestoreDieToHand(visualColor, dtoCommand.Number);
        gm.turnManager.CurrentDrawnColor = visualColor;
        gm.turnManager.CurrentDrawnValue = dtoCommand.Number;
        // El nucleo tambien debe saber que el dado esta en la mano (Re-draw y colocar lo leen de aqui)
        gm.ServerState.CurrentDrawnColor = dtoCommand.Color;
        gm.ServerState.CurrentDrawnValue = dtoCommand.Number;
        gm.turnManager.HasDrawn = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetDrawInputLock(false);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Undo");
        }

        // La mano vuelve a tener dado: iluminar casillas, apagar Extraer y reactivar Re-draw
        gm.placementOrchestrator.RefreshPlacementHighlights();
        if (gm.drawButton != null) gm.drawButton.interactable = false;
        if (gm.reDrawButton != null) gm.reDrawButton.interactable = p.reDraws > 0;
        if (gm.reDrawText != null) gm.reDrawText.text = $"{p.reDraws}";
        return true;
    }
}