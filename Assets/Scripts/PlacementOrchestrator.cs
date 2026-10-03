using System.Collections;
using UnityEngine;

public class PlacementOrchestrator
{
    private GameManager gm;
    private Coroutine confirmationRoutine;
    public bool isMoveModeActive = false;

    public PlacementOrchestrator(GameManager gm)
    {
        this.gm = gm;
    }

    public void BeginPlacement(int row, int col)
    {
        ConfirmAndProcessScore(row, col);
        UIManager.Instance.ClearDieUI();
    }

    public void ConfirmAndProcessScore(int r, int c)
    {
        UIManager.Instance.SetDrawInputLock(true);

        if (CommandManager.Instance != null) CommandManager.Instance.isTransitioning = true;
        if (GridInteractionManager.Instance != null) GridInteractionManager.Instance.LockUIForTransition();
        if (gm.reDrawButton != null) gm.reDrawButton.interactable = false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        DieColor color = gm.turnManager.CurrentDrawnColor;
        GroupData group = p.activeGroups[color];
        int number = group.targetSize;

        // 1. ENSAMBLAJE DEL COMANDO (Dumb Terminal)
        var command = new MyGame.Core.PlaceDieCommand
        {
            PlayerId = gm.turnManager.CurrentPlayerIndex,
            TargetCell = new MyGame.Core.GridPos(r, c),
            Color = (MyGame.Core.DieColor)(int)color,
            GroupId = group.id,
            Number = number
        };

        // =============================================================
        // F1.3: Usamos GameManager.Instance.ServerState directamente.
        // Ya NO creamos un MatchStateDTO efímero local.
        // =============================================================

        // Sincronizamos el estado del tablero y el perfil del jugador actual
        gm.ServerState.CurrentPhase = MyGame.Core.MatchPhase.PlayerTurn;
        gm.ServerState.CurrentPlayerIndex = gm.turnManager.CurrentPlayerIndex;
        gm.ServerState.PlayerBoards[gm.turnManager.CurrentPlayerIndex] = gm.gridManager.GetBoardStateDTO(gm.turnManager.CurrentPlayerIndex);
        gm.ServerState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex] = p.ToDTO();

        // F1.7: Extraemos el patrón de variantConfig del ServerState (o del ScriptableObject como fallback)
        MyGame.Core.PatternDefDTO variantPattern = null;
        if (gm.ServerState.VariantConfig != null)
        {
            variantPattern = gm.ServerState.VariantConfig.GetPattern(number);
        }
        else if (gm.currentSession != null && gm.currentSession.selectedVariant != null)
        {
            PatternData pData = gm.currentSession.selectedVariant.GetPattern(number);
            if (pData != null) variantPattern = pData.ToDTO();
        }

        // F1.7: Constantes de scoring desde ScoringConfig (fuente de verdad única)
        MyGame.Core.ScoringConfigDTO sc = gm.ServerState.ScoringConfig;

        // 3. ENVIAR INTENCIÓN A LA CAPA 0 (LA AUTORIDAD)
        bool isLegalMove = MyGame.Core.CoreMatchProcessor.ProcessPlacementIntent(
            gm.ServerState, command, variantPattern, variantPattern,
            sc.RowCompleteBonus, sc.ColCompleteBonus, sc.IntersectionBonus,
            sc.RowMultipliers, sc.ColMultipliers
        );

        if (!isLegalMove)
        {
            Debug.LogWarning("[Security] CoreMatchProcessor rechazó la jugada.");
            UIManager.Instance.SetDrawInputLock(false);
            if (CommandManager.Instance != null) CommandManager.Instance.isTransitioning = false;
            return;
        }

        // 4. SINCRONIZACIÓN VISUAL (Capa 0 -> Capa 1)
        MyGame.Core.PlayerDataDTO updatedProfile = gm.ServerState.PlayerProfiles[gm.turnManager.CurrentPlayerIndex];

        int puntosGanados = updatedProfile.Score - p.score;
        p.score = updatedProfile.Score;
        p.placedDice = updatedProfile.PlacedDice;

        group.occupiedCells.Add(new UnityEngine.Vector2Int(r, c));

        UndoCommand undoCmd = new UndoCommand(gm, gm.turnManager.CurrentPlayerIndex, r, c, color, group.id, number);
        if (CommandManager.Instance != null) CommandManager.Instance.RegisterCommand(undoCmd);

        gm.gridManager.CommitDieToLogic(gm.turnManager.CurrentPlayerIndex, r, c, color, group.id, number);
        gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Placed");

        UnityEngine.Vector3 posMundo = gm.gridManager.GetWorldPosition(gm.turnManager.CurrentPlayerIndex, r, c);
        PopUpManager.Instance.ShowPopUp(posMundo, $"+{puntosGanados}", UnityEngine.Color.white);

        if (group.isClosed)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Nice");
            gm.gridManager.MarkGroupAsCompleted(gm.turnManager.CurrentPlayerIndex, group.occupiedCells);
        }

        UIManager.Instance.UpdateProgressText(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize);

        // ==========================================
        // EVALUACIÓN DE SESIÓN EN LA CAPA 0
        // ==========================================
        MyGame.Core.CoreSessionProcessor.EvaluateSessionState(gm.ServerState, gm.maxDicePerPlayer);

        if (gm.ServerState.CurrentPhase == MyGame.Core.MatchPhase.GameOver)
        {
            // EL SERVIDOR DICTAMINÓ EL FIN DE LA PARTIDA
            // Sincronizamos las multas finales calculadas en Capa 0 al cliente visual
            for (int i = 0; i < gm.players.Count; i++)
            {
                if (gm.ServerState.PlayerProfiles.ContainsKey(i))
                {
                    gm.players[i].score = gm.ServerState.PlayerProfiles[i].Score;
                }
            }
            gm.StartCoroutine(gm.EndGameSequence());
        }
        else
        {
            // LA PARTIDA CONTINÚA
            UIManager.Instance.UpdateScore(p.score);

            // F1.5: HasDrawn se gestiona a través de ServerState
            gm.ServerState.HasDrawn = false;
            gm.turnManager.HasDrawn = false;

            if (gm.diceManager.GetTotalDiceLeft() == 0 && !gm.turnManager.HasDrawn)
            {
                // Failsafe local
                gm.EndMatch();
            }
            else
            {
                // Avance de turno visual
                gm.StartCoroutine(gm.turnManager.TurnTransitionPause());
            }
        }
    }

    /// <summary>
    /// Recalcula e ilumina las celdas válidas para el dado que el jugador tiene actualmente en la mano.
    /// Invocado después de usar un comodín (Move/Undo) para restaurar el estado visual.
    /// </summary>
    public void RefreshPlacementHighlights()
    {
        // 1. Verificación de estado: Si no hay dado en la mano, no hay nada que iluminar.
        if (!gm.turnManager.HasDrawn)
        {
            gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);
            return;
        }

        // 2. Limpieza de seguridad antes de recalcular
        int pIndex = gm.turnManager.CurrentPlayerIndex;
        gm.gridManager.ClearHighlights(pIndex);

        // 3. Recopilación de datos inyectables
        PlayerData p = gm.turnManager.GetCurrentPlayer();
        DieColor drawnColor = gm.turnManager.CurrentDrawnColor;

        // Prevención de NullReference si el grupo aún no existe
        if (!p.activeGroups.ContainsKey(drawnColor)) return;

        GroupData group = p.activeGroups[drawnColor];
        VariantData variant = gm.currentSession != null ? gm.currentSession.selectedVariant : null;

        // Obtenemos la matriz lógica actual
        var logic = gm.gridManager.GetBoardLogic(pIndex);

        bool foundValidSpot = false;

        // 4. Escaneo del tablero (8x10)
        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                bool isValid = PlacementValidator.IsValidPlacement(
                    logic,
                    10, 8,
                    r, c,
                    drawnColor,
                    group.id,
                    group.targetSize,
                    p,
                    variant
                );

                if (isValid)
                {
                    CellComponent cell = gm.gridManager.allCellsVisual[pIndex][r, c];
                    if (cell != null)
                    {
                        cell.SetHighlight(true);
                        foundValidSpot = true;
                    }
                }
            }
        }

        if (!foundValidSpot)
        {
            Debug.LogWarning("[UX] Softlock topológico detectado. Evaluando supervivencia...");

            UIManager.Instance.ClearDieUI();
            UIManager.Instance.SetDrawInputLock(false);
            gm.turnManager.HasDrawn = false;
            gm.ServerState.HasDrawn = false;

            if (p.reDraws > 0)
            {
                // SE SALVA (AÚN TIENE REDRAWS)
                if (gm.reDrawButton != null) gm.reDrawButton.interactable = true;
                if (PopUpManager.Instance != null)
                {
                    PopUpManager.Instance.ShowPopUp(UnityEngine.Vector3.up * 2f, "DADO INJUGABLE\n¡Usa un Re-Draw!", UnityEngine.Color.yellow);
                }
            }
            else
            {
                // MUERTE SÚBITA (0 REDRAWS)
                p.isEliminated = true;

                int maxDadosPorJugador = 52;
                int rondasFaltantes = maxDadosPorJugador - p.placedDice;

                int jugadoresVivos = 0;
                foreach (var player in gm.players)
                {
                    if (!player.isEliminated) jugadoresVivos++;
                }

                if (jugadoresVivos == 0)
                {
                    if (PopUpManager.Instance != null)
                        PopUpManager.Instance.ShowPopUp(UnityEngine.Vector3.up * 2f, "TABLERO MUERTO", UnityEngine.Color.red);

                    gm.EndMatch();
                }
                else
                {
                    if (rondasFaltantes > 0)
                    {
                        gm.diceManager.BurnRandomDice(rondasFaltantes);
                    }

                    if (PopUpManager.Instance != null)
                        PopUpManager.Instance.ShowPopUp(UnityEngine.Vector3.up * 2f, "¡JUGADOR ELIMINADO!", UnityEngine.Color.red);

                    gm.turnManager.EndTurn();
                }
            }
            return;
        }
    }
}
