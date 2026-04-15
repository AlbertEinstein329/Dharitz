using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    // --- NUEVO: Para mostrar los puntos en tiempo real ---
    public TextMeshProUGUI textoScoreHUD;

    [Header("Base de Datos de Sprites")]
    public Sprite[] spritesRojos;
    public Sprite[] spritesAzules;
    public Sprite[] spritesBlancos;
    public Sprite[] spritesNegros;

    [Header("Contador de Bolsa")]
    public TextMeshProUGUI textoDadosRestantes;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ActualizarManoUI(DieColor color, int numeroLanzado, int dadosColocados, int totalDados)
    {
        if (panelDados != null) panelDados.SetActive(true);

        Sprite spriteAUsar = null;
        int indiceSprite = numeroLanzado - 1;

        switch (color)
        {
            case DieColor.Rojo: spriteAUsar = spritesRojos[indiceSprite]; break;
            case DieColor.Azul: spriteAUsar = spritesAzules[indiceSprite]; break;
            case DieColor.Blanco: spriteAUsar = spritesBlancos[indiceSprite]; break;
            case DieColor.Negro: spriteAUsar = spritesNegros[indiceSprite]; break;
        }

        if (spriteAUsar != null) imagenDadoActual.sprite = spriteAUsar;

        int dadosFaltantes = totalDados - dadosColocados;
        textoProgreso.text = $"{color.ToString().ToUpper()} {numeroLanzado} / Faltan: {dadosFaltantes}";
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