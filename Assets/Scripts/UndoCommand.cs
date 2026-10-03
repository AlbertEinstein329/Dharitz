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
            TargetCell = new GridPos(c, r), // Manteniendo tu mapeo visual X=c, Y=r
            Color = (MyGame.Core.DieColor)(int)color,
            GroupId = groupId,
            Number = targetSize
        };
    }

    public void Execute()
    {
        // El orquestador ya aplicó la jugada visualmente. El comando solo se encola.
    }

    public void Undo()
    {
        // 1. ENVIAR INTENCIÓN A LA AUTORIDAD (Usando el estado real del servidor persistente)
        float[] rowMults = { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f };
        float[] colMults = { 1.0f, 2.0f, 3.0f, 3.5f, 5.0f, 6.0f };

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
            return;
        }

        // 2. SINCRONIZACIÓN VISUAL ESTRICTA (El servidor aceptó y ya mutó el gm.ServerState)
        PlayerData p = gm.players[dtoCommand.PlayerId];
        PlayerDataDTO updatedProfile = gm.ServerState.PlayerProfiles[dtoCommand.PlayerId];

        p.score = updatedProfile.Score;
        p.placedDice = updatedProfile.PlacedDice;
        p.accumulatedStructurePoints = updatedProfile.AccumulatedStructurePoints;
        p.currentUndoUses = updatedProfile.CurrentUndoUses;

        DieColor visualColor = (DieColor)(int)dtoCommand.Color;

        // Verificamos nulos explícitamente para evitar tu NullReferenceException
        if (p.activeGroups.ContainsKey(visualColor) && p.activeGroups[visualColor] != null)
        {
            p.activeGroups[visualColor].occupiedCells.Remove(new Vector2Int(dtoCommand.TargetCell.X, dtoCommand.TargetCell.Y));
        }

        // Mapeo inverso a la matriz visual (Y = row, X = col)
        gm.gridManager.RemoveDie(dtoCommand.PlayerId, dtoCommand.TargetCell.Y, dtoCommand.TargetCell.X);

        // 3. RESTAURACIÓN DE LA INTERFAZ
        UIManager.Instance.UpdateScore(p.score);

        if (p.activeGroups.ContainsKey(visualColor) && p.activeGroups[visualColor] != null)
        {
            UIManager.Instance.UpdateProgressText(visualColor, dtoCommand.Number, p.activeGroups[visualColor].occupiedCells.Count, dtoCommand.Number);
        }

        UIManager.Instance.RestoreDieToHand(visualColor, dtoCommand.Number);
        gm.turnManager.CurrentDrawnColor = visualColor;
        gm.turnManager.HasDrawn = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetDrawInputLock(false);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Undo");
        }
    }
}