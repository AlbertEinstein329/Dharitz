using System.Collections.Generic;
using UnityEngine;
using MyGame.Core;

namespace MyGame.Networking
{
    /// <summary>
    /// Capa 1 del modo online: traduce el snapshot autoritativo a la escena (tableros, HUD, mano).
    /// No decide reglas; solo refleja lo que el servidor ya resolvió.
    /// </summary>
    public static class OnlineMatchPresenter
    {
        private static bool hasAppliedOnce;

        public static void ResetPresentation() => hasAppliedOnce = false;

        // La escena debe tener tableros y jugadores creados (GridManager.Start) antes de aplicar
        public static bool IsSceneReady(int playerCount)
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.gridManager != null
                && gm.gridManager.allBoardsLogic != null
                && gm.gridManager.allBoardsLogic.Count == playerCount
                && gm.players.Count == playerCount
                && UIManager.Instance != null;
        }

        public static void Apply(NetworkMatchSnapshot snap, int localPlayerIndex, bool isServer)
        {
            GameManager gm = GameManager.Instance;

            // En el host, ServerState ES la autoridad: nunca se sobrescribe con su propio espejo
            if (!isServer) gm.AdoptServerState(snap.ToMatchState());

            int previousPlayer = gm.turnManager.CurrentPlayerIndex;
            bool hadHandInMyTurn = gm.turnManager.HasDrawn && previousPlayer == localPlayerIndex;

            for (int i = 0; i < snap.Players.Length; i++)
            {
                ApplyPlayer(gm.players[i], snap.Players[i]);
                ApplyBoard(gm.gridManager, i, snap.Players[i].Board);
            }

            DieColor drawnColor = snap.DrawnColor >= 0 ? (DieColor)snap.DrawnColor : DieColor.Red;
            gm.turnManager.SyncFromServer(snap.CurrentPlayerIndex, snap.HasDrawn, drawnColor, snap.DrawnValue);

            if (!hasAppliedOnce || previousPlayer != snap.CurrentPlayerIndex)
            {
                gm.gridManager.SwitchViewTo(snap.CurrentPlayerIndex);
            }

            PlayActionFeedback(gm, snap);
            ApplyHud(gm, snap, localPlayerIndex, hadHandInMyTurn);

            hasAppliedOnce = true;
        }

        private static void ApplyPlayer(PlayerData player, NetworkPlayerSnapshot ps)
        {
            player.name = ps.Name;
            player.score = ps.Score;
            player.placedDice = ps.PlacedDice;
            player.reDraws = ps.ReDraws;
            player.currentUndoUses = ps.UndoUses;
            player.currentMoveUses = ps.MoveUses;
            player.isEliminated = ps.IsEliminated;
            player.isBot = false;
            player.patternCounts = (int[])ps.PatternCounts.Clone();

            // PlacementValidator del cliente usa estos grupos para iluminar casillas válidas
            player.activeGroups.Clear();
            int cols = ps.Board.Cols;
            foreach (NetworkGroupSnapshot g in ps.Groups)
            {
                var group = new GroupData
                {
                    id = g.Id,
                    color = (DieColor)g.Color,
                    targetSize = g.TargetSize,
                    occupiedCells = new List<Vector2Int>(g.Cells.Length)
                };
                foreach (short cell in g.Cells) group.occupiedCells.Add(new Vector2Int(cell / cols, cell % cols));
                player.activeGroups[group.color] = group;
            }
        }

        // Diff contra la matriz lógica local: solo se tocan las casillas que cambiaron
        private static void ApplyBoard(GridManager grid, int playerIndex, NetworkBoardState board)
        {
            GridManager.DieData[,] logic = grid.GetBoardLogic(playerIndex);
            int rows = logic.GetLength(0);
            int cols = logic.GetLength(1);

            if (rows != board.Rows || cols != board.Cols)
            {
                Debug.LogError($"[OnlineMatchPresenter] Tablero {playerIndex}: el servidor envía {board.Rows}x{board.Cols} y la escena tiene {rows}x{cols}.");
                return;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int index = r * cols + c;
                    byte raw = board.CompressedCells[index];
                    GridManager.DieData existing = logic[r, c];

                    if (raw == 0)
                    {
                        if (existing != null) grid.RemoveDie(playerIndex, r, c);
                        continue;
                    }

                    DieColor color = (DieColor)((raw >> 4) - 1);
                    int value = raw & 0x0F;
                    int groupId = board.GroupIds[index];

                    if (existing != null && existing.color == color && existing.value == value && existing.groupId == groupId) continue;

                    if (existing != null) grid.RemoveDie(playerIndex, r, c);
                    grid.CommitDieToLogic(playerIndex, r, c, color, groupId, value);
                }
            }
        }

        private static void PlayActionFeedback(GameManager gm, NetworkMatchSnapshot snap)
        {
            NetworkActionSnapshot action = snap.LastAction;
            if (action.Type != NetworkActionType.Placed) return;
            if (action.PlayerIndex < 0 || action.PlayerIndex >= gm.players.Count) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Placed");

            if (PopUpManager.Instance != null)
            {
                Vector3 worldPos = gm.gridManager.GetWorldPosition(action.PlayerIndex, action.Row, action.Col);
                PopUpManager.Instance.ShowPopUp(worldPos, $"+{action.ScoreDelta}", Color.white);
            }

            if (action.ClosedGroup)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Nice");

                PlayerData player = gm.players[action.PlayerIndex];
                foreach (GroupData group in player.activeGroups.Values)
                {
                    if (group.isClosed && group.occupiedCells.Contains(new Vector2Int(action.Row, action.Col)))
                    {
                        gm.gridManager.MarkGroupAsCompleted(action.PlayerIndex, group.occupiedCells);
                        break;
                    }
                }
            }
        }

        private static void ApplyHud(GameManager gm, NetworkMatchSnapshot snap, int localPlayerIndex, bool hadHandInMyTurn)
        {
            bool isGameOver = snap.Phase == (byte)MatchPhase.GameOver;
            bool isMyTurn = !isGameOver && localPlayerIndex >= 0 && snap.CurrentPlayerIndex == localPlayerIndex;
            bool hasHandNow = isMyTurn && snap.HasDrawn;
            PlayerData me = localPlayerIndex >= 0 && localPlayerIndex < gm.players.Count ? gm.players[localPlayerIndex] : null;

            int[] bag = snap.BagCountsByColor;
            UIManager.Instance.UpdateDiceCounters(bag[0], bag[1], bag[2], bag[3]);
            if (me != null) UIManager.Instance.UpdateScore(me.score);

            // Solo se puede tocar la bolsa en el turno propio y con la mano vacía
            UIManager.Instance.SetDrawInputLock(!(isMyTurn && !snap.HasDrawn));
            if (gm.drawButton != null) gm.drawButton.interactable = isMyTurn && !snap.HasDrawn && snap.BagTotal > 0;
            if (gm.reDrawButton != null) gm.reDrawButton.interactable = hasHandNow && me != null && me.reDraws > 0;
            if (gm.reDrawText != null && me != null) gm.reDrawText.text = $"{me.reDraws}";

            // Undo y Move todavía mutan estado local: deshabilitados online hasta pasar por el servidor
            if (UIManager.Instance.undoButton != null) UIManager.Instance.undoButton.interactable = false;

            // Un Re-Draw mantiene la mano llena pero cambia el dado: el servidor lo marca como Drew
            bool handReplaced = snap.LastAction.Type == NetworkActionType.Drew;
            if (hasHandNow && (!hadHandInMyTurn || handReplaced))
            {
                gm.gridManager.ClearHighlights(localPlayerIndex);
                DieColor color = gm.turnManager.CurrentDrawnColor;
                if (me != null && me.activeGroups.TryGetValue(color, out GroupData group))
                {
                    int pIndex = localPlayerIndex;
                    UIManager.Instance.UpdateHandUI(color, group.targetSize, group.occupiedCells.Count, group.targetSize, () =>
                    {
                        gm.gridManager.ShowValidMoves(pIndex, color, group.id, group.targetSize);
                    });
                }
            }
            else if (hadHandInMyTurn && !hasHandNow)
            {
                UIManager.Instance.ClearDieUI();
                gm.gridManager.ClearHighlights(localPlayerIndex);
            }

            if (isGameOver && !gm.isGameOver)
            {
                gm.isGameOver = true;
                if (gm.drawButton != null) gm.drawButton.interactable = false;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Victory");
                UIManager.Instance.ShowFinalResults(Mathf.Max(0, localPlayerIndex));
            }
        }
    }
}
