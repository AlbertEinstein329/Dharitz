using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
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

    [Header("Contador de Bolsa")]
    public TextMeshProUGUI textoDadosRestantes;

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
    }

    public void UpdateHandUI(DieColor color, int numeroLanzado, int dadosColocados, int totalDados)
    {
        if (panelDados != null) panelDados.SetActive(true);

        Sprite finalSprite = GetSprite(color, numeroLanzado);

        // 1. Prevent animation overlap (Spam protection)
        rollSequence?.Kill();

        // 2. Ensure the image is visible (Alpha = 1) in case it was hidden
        imagenDadoActual.color = Color.white;

        // 3. Create the DOTween Sequence for the rolling effect
        rollSequence = DOTween.Sequence();
        float intervalDuration = rollDuration / rollAnimationSteps;

        // Loop to create rapid random sprite changes
        for (int i = 0; i < rollAnimationSteps; i++)
        {
            rollSequence.AppendCallback(() =>
            {
                // Select a random number between 1 and 6 to simulate the roll
                int randomFace = UnityEngine.Random.Range(1, 7);
                imagenDadoActual.sprite = GetSprite(color, randomFace);

                // [OPEN PLACEHOLDER FOR AUDIO]
                // AudioManager.Instance.PlayTickSound();
            });
            rollSequence.AppendInterval(intervalDuration);
        }

        // 4. Final step: Show the actual rolled die
        rollSequence.AppendCallback(() =>
        {
            imagenDadoActual.sprite = finalSprite;
            // [OPEN PLACEHOLDER FOR AUDIO]
            // AudioManager.Instance.PlayDingSound();
        });

        int dadosFaltantes = totalDados - dadosColocados;
        textoProgreso.text = $"{color.ToString().ToUpper()} {numeroLanzado} / Faltan: {dadosFaltantes}";
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
    }

    public Sprite GetSprite(DieColor color, int number)
    {
        if (number < 1 || number > 6) return null;

        int index = number - 1;
        Sprite[] listaSeleccionada = null;

        switch (color)
        {
            case DieColor.Rojo: listaSeleccionada = spritesRojos; break;
            case DieColor.Azul: listaSeleccionada = spritesAzules; break;
            case DieColor.Blanco: listaSeleccionada = spritesBlancos; break;
            case DieColor.Negro: listaSeleccionada = spritesNegros; break;
        }

        if (listaSeleccionada == null || listaSeleccionada.Length < 6) return null;

        return listaSeleccionada[index];
    }

    // --- NUEVA FUNCIÓN: Actualiza el texto en la pantalla durante el juego ---
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
        int puntosBase = player.dadosColocados * ScoreManager.POINTS_PER_DIE;

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

        int puntosEstructura = player.puntosEstructuraAcumulados;

        // Puntos Exóticos: Lo que "sobra" del score total tras restar lo básico, son los combos de variantes.
        int puntosVariante = puntosTiempoReal - (puntosBase + totalBonosPatrones + puntosEstructura);

        // 2. PENALIZACIONES DE FIN DE PARTIDA (AHORA SOLO HUECOS)
        // Recordatorio: Debes usar la función que creamos para contar huecos agrupados
        int penalizacionHuecos = GameManager.Instance.gridManager.CalcularPenalizacionHuecos(playerIndex, out int cantidadHuecos);

        // Los unos ya fueron restados del 'puntosTiempoReal' durante la partida. 
        // Solo los obtenemos para mostrarlos como información al jugador.
        int cantidadUnos = GameManager.Instance.gridManager.ObtenerPenalizacionesPorUnos(playerIndex);
        int puntosRestadosPorUnos = cantidadUnos * 100;

        // 3. CÁLCULO FINAL (Score Real - Multa de Huecos)
        int totalFinal = puntosTiempoReal - penalizacionHuecos;

        // 4. CONSTRUCCIÓN DE LA INTERFAZ
        string textoCombos = puntosEstructura > 0 ? $"Combos Estructura: +{puntosEstructura} pts\n" : "";
        string textoVariantes = puntosVariante > 0 ? $"Bonos de Variante: +{puntosVariante} pts\n" : "";

        string textoPenalizaciones = "";
        if (penalizacionHuecos > 0) textoPenalizaciones += $"<color=red>Huecos encerrados: -{penalizacionHuecos} pts</color>\n";
        if (puntosRestadosPorUnos > 0) textoPenalizaciones += $"<color=orange>1s mal colocados (Cobrado en juego): -{puntosRestadosPorUnos} pts</color>\n";

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

    public void ActualizarContadorBolsa(int cantidad)
    {
        if (textoDadosRestantes != null)
        {
            textoDadosRestantes.text = $"DADOS: {cantidad}";
            textoDadosRestantes.color = cantidad <= 10 ? Color.red : Color.black;
        }
    }

    public void OcultarPanelResultados()
    {
        if (panelGameOver != null) panelGameOver.SetActive(false);

        // --- Volvemos a encender el HUD para poder ver el tablero ---
        if (textoScoreHUD != null) textoScoreHUD.gameObject.SetActive(true);
    }



}