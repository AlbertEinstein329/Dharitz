using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    [System.Serializable]
    public struct DiceCounterUI
    {
        public Image dieIcon;
        public TextMeshProUGUI countText;
    }

    public static UIManager Instance;

    [Header("Paneles Principales")]
    public GameObject panelGameOver;
    public TextMeshProUGUI textoResultados;

    [Header("Referencias de UI del Panel Superior")]
    public GameObject panelDados;
    public Image imagenDadoActual;
    public TextMeshProUGUI textoProgreso;
    public TextMeshProUGUI textoScoreHUD;

    [Header("Base de Datos de Sprites")]
    public Sprite[] spritesRojos;
    public Sprite[] spritesAzules;
    public Sprite[] spritesBlancos;
    public Sprite[] spritesNegros;

    [Header("Dice Counters")]
    public DiceCounterUI redCounter;
    public DiceCounterUI blueCounter;
    public DiceCounterUI whiteCounter;
    public DiceCounterUI blackCounter;

    [Header("Animation Settings")]
    [SerializeField] private float rollDuration = 0.5f; // Duración total de la animación en segundos
    [SerializeField] private int rollAnimationSteps = 6; // Cuántas veces cambiará de sprite
    //[SerializeField] private Sprite emptySlotSprite; // OPCIONAL: Asigna aquí la imagen de fondo vacía si la tienes, si no, déjalo en null
    
    private Sprite originalSlotSprite;
    private Sequence rollSequence; // Guarda la referencia de DOTween

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // CACHÉ: Guardamos la "Source Image" que configuraste en el editor de Unity
        if (imagenDadoActual != null)
        {
            originalSlotSprite = imagenDadoActual.sprite;
        }

        InitializeCounters();

    }

    /// <summary>
    /// Locks or unlocks the draw area to prevent inputs during turn transitions.
    /// </summary>
    public void SetDrawInputLock(bool isLocked)
    {
        if (imagenDadoActual != null)
        {
            UIDieInteractor interactor = imagenDadoActual.GetComponent<UIDieInteractor>();
            if (interactor != null) interactor.isInputLocked = isLocked;
        }
    }


    /// <summary>
    /// Updates the UI with a rolling animation, randomizing both color and number.
    /// Executes the onComplete callback only when the animation finishes.
    /// </summary>
    public void UpdateHandUI(DieColor finalColor, int finalNumber, int placedDice, int totalDice, System.Action onCompleteCallback)
    {
        if (panelDados != null) panelDados.SetActive(true);

        Sprite finalSprite = GetSprite(finalColor, finalNumber);
        rollSequence?.Kill();
        imagenDadoActual.color = Color.white;

        // Bloqueamos el espacio de interacción
        UIDieInteractor interactor = imagenDadoActual.GetComponent<UIDieInteractor>();
        if (interactor != null) interactor.IsSlotEmpty = false;

        rollSequence = DOTween.Sequence();
        float intervalDuration = rollDuration / rollAnimationSteps;

        // Bucle de animación
        for (int i = 0; i < rollAnimationSteps; i++)
        {
            rollSequence.AppendCallback(() =>
            {
                DieColor randomColor = (DieColor)UnityEngine.Random.Range(0, 4);
                int randomFace = UnityEngine.Random.Range(1, 7);

                imagenDadoActual.sprite = GetSprite(randomColor, randomFace);
                AudioManager.Instance.PlayTickSound();
            });
            rollSequence.AppendInterval(intervalDuration);
        }

        // Finalización de la animación
        rollSequence.AppendCallback(() =>
        {
            imagenDadoActual.sprite = finalSprite;

            // Ahora las variables sí existen en la cabecera y la resta funcionará
            int missingDice = totalDice - placedDice;
            if (textoProgreso != null)
            {
                textoProgreso.text = $"{finalColor.ToString().ToUpper()} {finalNumber} / Faltan: {missingDice}";
            }

            // Disparamos el callback hacia el GameManager
            onCompleteCallback?.Invoke();
        });
    }

    /// <summary>
    /// Clears the current die from the UI, restoring the original default source image.
    /// </summary>
    public void ClearDieUI()
    {
        // 1. Kill animation to prevent overlaps
        rollSequence?.Kill();

        // 2. Restore the cached default sprite
        if (imagenDadoActual != null)
        {
            imagenDadoActual.sprite = originalSlotSprite;
            imagenDadoActual.color = Color.white; // Ensures visibility
        }

        UIDieInteractor interactor = imagenDadoActual.GetComponent<UIDieInteractor>();
        if (interactor != null)
        {
            // Desbloqueamos el espacio: Vuelve a estar vacío, permitiendo el "Tap" para extraer el siguiente.
            interactor.IsSlotEmpty = true;
        }

    }

    public Sprite GetSprite(DieColor color, int number)
    {
        if (number < 1 || number > 6) return null;

        int index = number - 1;
        Sprite[] listaSeleccionada = null;

        switch (color)
        {
            case DieColor.Red: listaSeleccionada = spritesRojos; break;
            case DieColor.Blue: listaSeleccionada = spritesAzules; break;
            case DieColor.White: listaSeleccionada = spritesBlancos; break;
            case DieColor.Black: listaSeleccionada = spritesNegros; break;
        }

        if (listaSeleccionada == null || listaSeleccionada.Length < 6) return null;

        return listaSeleccionada[index];
    }

    //Actualiza el texto en la pantalla durante el juego
    public void ActualizarScore(int nuevoScore)
    {
        if (textoScoreHUD != null)
        {
            textoScoreHUD.text = $"PUNTOS: {nuevoScore}";
        }
    }

    public void MostrarResultadosFinales(int playerIndex)
    {
        panelGameOver.SetActive(true);

        //  Ocultamos el HUD de puntos en pantalla
        if (textoScoreHUD != null) textoScoreHUD.gameObject.SetActive(false);

        PlayerData player = GameManager.Instance.players[playerIndex];

        // 1. Puntos que el jugador ya ganó en tiempo real
        int puntosTiempoReal = player.score;

        // --- DESGLOSE VISUAL (No se suman al total, solo se calculan para mostrar en texto) ---
        int puntosBase = player.placedDice * ScoreManager.POINTS_PER_DIE;

        int totalBonosPatrones = 0;
        string desglosePatrones = "";
        for (int i = 1; i <= 6; i++) // Ahora evaluamos desde el 1
        {
            if (player.conteoPatrones[i] > 0)
            {
                int bonoUnico = ScoreManager.Instance.GetPatternBonus(i);
                int subtotal = player.conteoPatrones[i] * bonoUnico;
                totalBonosPatrones += subtotal;
                desglosePatrones += $"Patrones de {i} (x{player.conteoPatrones[i]}): +{subtotal} pts\n";
            }
        }

        int puntosEstructura = player.accumulatedStructurePoints;

        // Puntos Exóticos: Lo que "sobra" del score total tras restar lo básico, son los combos de variantes.
        int puntosVariante = puntosTiempoReal - (puntosBase + totalBonosPatrones + puntosEstructura);

        // 2. PENALIZACIONES DE FIN DE PARTIDA (AHORA SOLO HUECOS)
        // Recordatorio: Debes usar la función que creamos para contar huecos agrupados
        int penalizacionHuecos = GameManager.Instance.gridManager.CalculateGapPenalty(playerIndex, false);


        // Los unos ya fueron restados del 'puntosTiempoReal' durante la partida. 
        // Solo los obtenemos para mostrarlos como información al jugador.
        int cantidadUnos = GameManager.Instance.gridManager.ObtenerPenalizacionesPorUnos(playerIndex);
        int puntosRestadosPorUnos = cantidadUnos * 200;

        // 2. CORRECCIÓN: El Score real YA TIENE la multa aplicada. ¡Están sincronizados!
        int totalFinal = player.score;

        // 4. CONSTRUCCIÓN DE LA INTERFAZ
        string textoCombos = puntosEstructura > 0 ? $"Combos Estructura: +{puntosEstructura} pts\n" : "";
        string textoVariantes = puntosVariante > 0 ? $"Bonos de Variante: +{puntosVariante} pts\n" : "";

        string textoPenalizaciones = "";
        if (penalizacionHuecos < 0)
        {
            textoPenalizaciones += $"<color=red>Huecos encerrados: {penalizacionHuecos} pts</color>\n";
        }

        textoResultados.text =
            $"<size=120%>{player.name.ToUpper()}</size>\n\n" +
            $"Dados (+{ScoreManager.POINTS_PER_DIE} c/u): +{puntosBase} pts\n" +
            $"{desglosePatrones}" +
            $"{textoCombos}" +
            $"{textoVariantes}" +
            $"{textoPenalizaciones}" +
            $"------------------------------\n" +
            $"<size=140%>TOTAL: {Mathf.Max(0, totalFinal)} PTS</size>";
    }

    /// <summary>
    /// Updates the 4 individual counters for remaining dice.
    /// </summary>
    public void UpdateDiceCounters(int redLeft, int blueLeft, int whiteLeft, int blackLeft)
    {
        if (redCounter.countText != null) redCounter.countText.text = redLeft.ToString();
        if (blueCounter.countText != null) blueCounter.countText.text = blueLeft.ToString();
        if (whiteCounter.countText != null) whiteCounter.countText.text = whiteLeft.ToString();
        if (blackCounter.countText != null) blackCounter.countText.text = blackLeft.ToString();
    }

    /// <summary>
    /// Initializes the counter icons with the base sprite (face 1) of each color.
    /// Ensures UI matches the current active skin automatically.
    /// </summary>
    public void InitializeCounters()
    {
        if (redCounter.dieIcon != null) redCounter.dieIcon.sprite = GetSprite(DieColor.Red, 1);
        if (blueCounter.dieIcon != null) blueCounter.dieIcon.sprite = GetSprite(DieColor.Blue, 1);
        if (whiteCounter.dieIcon != null) whiteCounter.dieIcon.sprite = GetSprite(DieColor.White, 1);
        if (blackCounter.dieIcon != null) blackCounter.dieIcon.sprite = GetSprite(DieColor.Black, 1);
    }


    public void OcultarPanelResultados()
    {
        if (panelGameOver != null) panelGameOver.SetActive(false);

        // --- Volvemos a encender el HUD para poder ver el tablero ---
        if (textoScoreHUD != null) textoScoreHUD.gameObject.SetActive(true);
    }

    /// <summary>
    /// Updates only the progress text without triggering the roll animation.
    /// Used after placing a die on the board.
    /// </summary>
    public void UpdateProgressText(DieColor finalColor, int finalNumber, int placedDice, int totalDice)
    {
        int missingDice = totalDice - placedDice;
        if (textoProgreso != null)
        {
            textoProgreso.text = $"{finalColor.ToString().ToUpper()} {finalNumber} / Faltan: {missingDice}";
        }
    }

}