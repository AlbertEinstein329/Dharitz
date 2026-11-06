using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tutorial guiado. Se crea desde GameManager cuando SessionConfig.isTutorial es true: la partida usa los dados fijos de
/// <see cref="MyGame.Core.TutorialScript"/> y esta clase muestra un panel con un texto por paso. Cada paso avanza solo cuando
/// el estado real del juego cumple su condicion (robar, colocar, deshacer, re-draw), asi no se puede romper el guion.
/// Marca <c>tutorialDone</c> en el perfil al terminar o al saltarlo. No suma monedas ni progreso de niveles.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private const int MenuSceneIndex = 1;

    private class Step
    {
        public string TextKey;
        public Func<bool> Done;              // null = el paso avanza con el boton Siguiente
        public Func<RectTransform> Target;   // elemento que late para llamar la atencion (opcional)
        public Action OnEnter;
        public Action OnExit;
        public bool Centered;                // true = el panel se pone en medio (para no tapar lo que se explica)
        public Func<object[]> Args;
        public bool IsLast;
    }

    private GameManager gm;
    private PlayerData player;
    private readonly List<Step> steps = new List<Step>();
    private int index = -1;
    private float advanceAt = -1f;

    private RectTransform panel;
    private TMP_Text body;
    private Button nextButton;
    private Button skipButton;
    private TMP_Text nextCaption;

    private Tween pulse;
    private RectTransform pulsed;
    private Vector3 pulsedScale;

    private int undoStart;
    private int redrawStart;

    public static TutorialManager Instance { get; private set; }

    /// <summary>Paso actual (0 = bienvenida) y su texto; los usan las pruebas automaticas.</summary>
    public int StepIndex => index;
    public string CurrentText => body != null ? body.text : string.Empty;
    public bool NextVisible => nextButton != null && nextButton.gameObject.activeSelf;
    public void PressNext() { OnNextPressed(); }

    public static void Begin(GameManager gameManager)
    {
        var go = new GameObject("TutorialManager");
        go.AddComponent<TutorialManager>().Init(gameManager);
    }

    /// <summary>Guarda en el perfil que el tutorial ya se vio (local; no toca monedas ni niveles).</summary>
    public static void MarkDone()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.CurrentProfile == null) return;
        SaveManager.Instance.CurrentProfile.tutorialDone = true;
        SaveManager.Instance.SaveLocal();
    }

    public static bool IsDone => SaveManager.Instance != null && SaveManager.Instance.CurrentProfile != null && SaveManager.Instance.CurrentProfile.tutorialDone;

    private void Init(GameManager gameManager)
    {
        Instance = this;
        gm = gameManager;
        player = gm.players[0];
        BuildSteps();
        BuildPanel();
        Advance();
    }

    private Vector2Int FindFirstDie(MyGame.Core.DieColor color)
    {
        var logic = gm.gridManager.GetBoardLogic(0);
        for (int r = 0; r < logic.GetLength(0); r++)
            for (int c = 0; c < logic.GetLength(1); c++)
                if (logic[r, c] != null && (int)logic[r, c].color == (int)color) return new Vector2Int(r, c);
        return new Vector2Int(-10, -10);
    }

    private void OnDestroy()
    {
        PlacementOrchestrator.TutorialGuard = null;
        ClearPulse();
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ Guion

    private void BuildSteps()
    {
        var scoring = gm.ServerState.ScoringConfig;

        Func<bool> hasDrawn = () => gm.turnManager.HasDrawn;
        Func<RectTransform> dieSlot = () =>
        {
            var interactor = FindFirstObjectByType<UIDieInteractor>();
            return interactor != null ? (RectTransform)interactor.transform : null;
        };

        steps.Add(new Step { TextKey = "tut_intro" });
        steps.Add(new Step { TextKey = "tut_draw", Done = hasDrawn, Target = dieSlot });
        steps.Add(new Step { TextKey = "tut_place1", Done = () => player.placedDice >= 1 });
        steps.Add(new Step
        {
            TextKey = "tut_patterns",
            Centered = true,
            OnEnter = () => { var ui = FindFirstObjectByType<PatternUIManager>(); if (ui != null) ui.SetExpanded(true); },
            OnExit = () => { var ui = FindFirstObjectByType<PatternUIManager>(); if (ui != null) ui.SetExpanded(false); }
        });
        steps.Add(new Step { TextKey = "tut_draw2", Done = hasDrawn, Target = dieSlot });
        steps.Add(new Step
        {
            TextKey = "tut_place2",
            // El patron del 2 es en diagonal: solo se aceptan las casillas diagonales al primer dado rojo (si no, el grupo se cerraria sin patron)
            OnEnter = () =>
            {
                Vector2Int first = FindFirstDie(MyGame.Core.DieColor.Red);
                PlacementOrchestrator.TutorialGuard = (r, c) =>
                {
                    bool ok = Mathf.Abs(r - first.x) == 1 && Mathf.Abs(c - first.y) == 1;
                    if (!ok && PopUpManager.Instance != null) PopUpManager.Instance.ShowPopUp(Vector3.up * 2f, Loc.T("tut_wrong_cell"), Color.yellow);
                    return ok;
                };
            },
            OnExit = () => PlacementOrchestrator.TutorialGuard = null,
            Done = () => player.placedDice >= 2
        });
        steps.Add(new Step
        {
            TextKey = "tut_scoring",
            Args = () => new object[]
            {
                scoring.PointsPerDie, scoring.RowCompleteBonus, scoring.ColCompleteBonus,
                scoring.PatternBonuses[1], scoring.PatternBonuses[6], scoring.CollectionBonus
            }
        });
        steps.Add(new Step { TextKey = "tut_rules", Args = () => new object[] { scoring.GapPenaltyPerCell, Mathf.Abs(scoring.GapPenaltyCap) } });
        steps.Add(new Step { TextKey = "tut_draw3", Done = hasDrawn, Target = dieSlot });
        steps.Add(new Step { TextKey = "tut_place3", Done = () => player.placedDice >= 3 });
        steps.Add(new Step
        {
            TextKey = "tut_undo",
            OnEnter = () => undoStart = player.currentUndoUses,
            Done = () => player.currentUndoUses < undoStart,
            Target = () => UIManager.Instance != null && UIManager.Instance.undoButton != null ? (RectTransform)UIManager.Instance.undoButton.transform : null
        });
        steps.Add(new Step { TextKey = "tut_replace", Done = () => player.placedDice >= 3 && !gm.turnManager.HasDrawn });
        steps.Add(new Step { TextKey = "tut_draw4", Done = hasDrawn, Target = dieSlot });
        steps.Add(new Step
        {
            TextKey = "tut_redraw",
            OnEnter = () => redrawStart = player.reDraws,
            Done = () => player.reDraws < redrawStart,
            Target = () => gm.reDrawButton != null ? (RectTransform)gm.reDrawButton.transform : null
        });
        steps.Add(new Step { TextKey = "tut_place5", Done = () => player.placedDice >= 4 && !gm.turnManager.HasDrawn });
        steps.Add(new Step { TextKey = "tut_end", IsLast = true });
    }

    // ------------------------------------------------------------------ Interfaz

    private void BuildPanel()
    {
        OnlineUiKit kit = OnlineUiKit.Instance;
        RectTransform parent = UiRoot.GetOverlayParent("[TutorialCanvas]", 450);
        panel = kit.CreatePanel(parent, new Vector2(960f, 360f), Loc.T("tut_title"));
        panel.name = "TutorialPanel";
        PlacePanel(false);

        body = kit.CreateLabel(panel, string.Empty, new Vector2(0.5f, 0.5f), new Vector2(840f, 170f), 34f, TextAlignmentOptions.Center);
        body.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.enableAutoSizing = true;
        body.fontSizeMin = 22f;
        body.fontSizeMax = 34f;

        nextButton = kit.CreateButton(panel, OnlineUiKit.ButtonStyle.Primary, Loc.T("tut_next"), new Vector2(0.72f, 0f), new Vector2(340f, 90f), OnNextPressed);
        nextButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 70f);
        nextCaption = nextButton.GetComponentInChildren<TMP_Text>(true);

        skipButton = kit.CreateButton(panel, OnlineUiKit.ButtonStyle.Neutral, Loc.T("tut_skip"), new Vector2(0.22f, 0f), new Vector2(260f, 90f), Skip);
        skipButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 70f);

        UiButtonSfx.AttachToAll();
    }

    // Arriba: entre el tablero y el borde (el panel de patrones, cuando se abre, ocupa esa zona: ahi se pasa al centro)
    private void PlacePanel(bool centered)
    {
        float y = centered ? 0.5f : 0.79f;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, y);
        panel.pivot = new Vector2(0.5f, centered ? 0.5f : 1f);
        panel.anchoredPosition = Vector2.zero;
    }

    private void Update()
    {
        if (index < 0 || index >= steps.Count) return;

        Step step = steps[index];
        if (step.Done == null) return;

        if (advanceAt < 0f && step.Done())
        {
            advanceAt = Time.unscaledTime + 0.7f; // deja ver el resultado (puntos, sonido) antes de pasar al siguiente texto
        }
        else if (advanceAt >= 0f && Time.unscaledTime >= advanceAt)
        {
            Advance();
        }
    }

    private void OnNextPressed()
    {
        if (index >= 0 && index < steps.Count && steps[index].IsLast) Finish();
        else Advance();
    }

    private void Advance()
    {
        advanceAt = -1f;
        if (index >= 0 && index < steps.Count) steps[index].OnExit?.Invoke();
        index++;
        if (index >= steps.Count) { Finish(); return; }

        Step step = steps[index];
        step.OnEnter?.Invoke();
        PlacePanel(step.Centered);

        body.text = Loc.T(step.TextKey, step.Args != null ? step.Args() : null);

        bool needsButton = step.Done == null;
        nextButton.gameObject.SetActive(needsButton);
        if (nextCaption != null) nextCaption.text = Loc.T(step.IsLast ? "tut_finish" : "tut_next");
        skipButton.gameObject.SetActive(!step.IsLast);

        ClearPulse();
        if (step.Target != null)
        {
            RectTransform target = step.Target();
            if (target != null) StartPulse(target);
        }
    }

    private void StartPulse(RectTransform target)
    {
        pulsed = target;
        pulsedScale = target.localScale;
        pulse = target.DOScale(pulsedScale * 1.12f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
    }

    private void ClearPulse()
    {
        if (pulse != null) pulse.Kill();
        pulse = null;
        if (pulsed != null) pulsed.localScale = pulsedScale;
        pulsed = null;
    }

    private void Skip() { Finish(); }

    private void Finish()
    {
        ClearPulse();
        MarkDone();
        if (panel != null) Destroy(panel.gameObject);
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuSceneIndex);
    }
}
