using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Asegúrate de tener instalado TextMeshPro

public class UIManager : MonoBehaviour
{
    public GameObject panelGameOver;
    public TextMeshProUGUI textoResultados;
    public static UIManager Instance;

    [Header("Referencias de UI del Panel")]
    public GameObject panelDados;       // El PanelDadoExtraido base
    public Image imagenDadoActual;     // La imagen central (ImagenDadoActual)
    public TextMeshProUGUI textoProgreso; // El TextoProgreso TMP

    [Header("Base de Datos de Sprites")]
    // Arreglo de 24 sprites (6 por color). Se configuran en el Inspector.
    public Sprite[] spritesRojos;   // Indices 0-5 corresponden a caras 1-6
    public Sprite[] spritesAzules;
    public Sprite[] spritesBlancos;
    public Sprite[] spritesNegros;

    [Header("Contador de Bolsa")]
    public TextMeshProUGUI textoDadosRestantes;

    void Awake()
    {
        // Singleton simple
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Ocultar el panel al inicio hasta que se extraiga un dado
        //if (panelDados != null) panelDados.SetActive(false);
    }

    // Esta función actualiza la UI basándose en el color y número lanzado (1-6) 
    public void ActualizarManoUI(DieColor color, int numeroLanzado, int dadosColocados, int totalDados)
    {
        if (panelDados != null) panelDados.SetActive(true);

        // 1. Elegir el sprite correcto basándose en Color y Número
        Sprite spriteAUsar = null;
        int indiceSprite = numeroLanzado - 1; // Convertimos cara 1-6 a índice 0-5

        switch (color)
        {
            case DieColor.Rojo:
                spriteAUsar = spritesRojos[indiceSprite];
                break;
            case DieColor.Azul:
                spriteAUsar = spritesAzules[indiceSprite];
                break;
            case DieColor.Blanco:
                spriteAUsar = spritesBlancos[indiceSprite];
                break;
            case DieColor.Negro:
                spriteAUsar = spritesNegros[indiceSprite];
                break;
        }

        // 2. Asignar el sprite a la imagen de la UI
        if (spriteAUsar != null)
        {
            imagenDadoActual.sprite = spriteAUsar;
            // Opcional: imagenDadoActual.SetNativeSize(); // Ajusta tamaño al sprite original
        }

        // 3. Actualizar el texto [cite: 32, 41-46]
        int dadosFaltantes = totalDados - dadosColocados;
        textoProgreso.text = $"{color.ToString().ToUpper()} {numeroLanzado} / Faltan: {dadosFaltantes}";
    }


    // Función para que el GridManager pueda pedir el sprite correcto
    public Sprite GetSprite(DieColor color, int number)
    {
        // 1. Validar que el número esté en el rango de dados (1-6) [cite: 21, 30]
        if (number < 1 || number > 6)
        {
            Debug.LogError($"Error: Se intentó pedir el número {number}, pero solo existen del 1 al 6.");
            return null;
        }

        int index = number - 1; // Convertimos cara 1-6 a índice de lista 0-5

        // 2. Elegir la lista según el color y verificar si tiene los 6 sprites
        Sprite[] listaSeleccionada = null;

        switch (color)
        {
            case DieColor.Rojo: listaSeleccionada = spritesRojos; break;
            case DieColor.Azul: listaSeleccionada = spritesAzules; break;
            case DieColor.Blanco: listaSeleccionada = spritesBlancos; break;
            case DieColor.Negro: listaSeleccionada = spritesNegros; break;
        }

        // 3. Verificación CRÍTICA: ¿La lista existe y tiene los sprites suficientes?
        if (listaSeleccionada == null || listaSeleccionada.Length < 6)
        {
            Debug.LogError($"¡Atención! La lista de sprites {color} en el UIManager está vacía o tiene menos de 6 imágenes.");
            return null;
        }

        return listaSeleccionada[index];
    }


    public void MostrarResultadosFinales(int playerIndex)
    {
        panelGameOver.SetActive(true);

        PlayerData player = GameManager.Instance.players[playerIndex];

        // 1. Obtener todas las penalizaciones
        int penalizacionHuecos = GameManager.Instance.gridManager.ObtenerPenalizacionesPorHuecos(playerIndex);
        int penalizacionUnos = GameManager.Instance.gridManager.ObtenerPenalizacionesPorUnos(playerIndex);
        int totalPenalizaciones = penalizacionHuecos + penalizacionUnos;

        // 2. Obtener bonos por líneas completas
        GameManager.Instance.gridManager.ContarLineasCompletas(playerIndex, out int filas, out int columnas);
        int bonoFilas = filas * 4;
        int bonoColumnas = columnas * 5;

        // --- NUEVO: Bonos de Variante ---
        int bonoVariante = GameManager.Instance.gridManager.CalcularBonosDeVariante(playerIndex, out string desgloseVariante);

        // 3. Cálculos de puntaje base y patrones
        int puntosBase = player.dadosColocados;
        string desglose = "";
        int totalBonosPatrones = 0;

        for (int i = 2; i <= 6; i++)
        {
            if (player.conteoPatrones[i] > 0)
            {
                int bonoUnico = ScoreManager.Instance.GetPatternBonus(i);
                int subtotal = player.conteoPatrones[i] * bonoUnico;
                totalBonosPatrones += subtotal;
                desglose += $"Patrón de {i}: +{subtotal} pts ({player.conteoPatrones[i]} compt.)\n";
            }
        }

        // Añadir textos de líneas
        if (filas > 0) desglose += $"Filas (x{filas}): +{bonoFilas} pts\n";
        if (columnas > 0) desglose += $"Columnas (x{columnas}): +{bonoColumnas} pts\n";

        // 4. Construir bloque de texto para las penalizaciones solo si existen
        string textoPenalizaciones = "";
        if (penalizacionHuecos > 0) textoPenalizaciones += $"<color=red>Huecos encerrados: -{penalizacionHuecos} pts</color>\n";
        if (penalizacionUnos > 0) textoPenalizaciones += $"<color=orange>Unos en contacto: -{penalizacionUnos} pts</color>\n";

        // Sumatoria final total (actualizada)
        int totalFinal = (puntosBase + totalBonosPatrones + bonoFilas + bonoColumnas + bonoVariante) - totalPenalizaciones;

        // 6. Mostrar en la UI
        textoResultados.text =
            $"<size=120%>{player.name.ToUpper()}</size>\n\n" +
            $"Dados colocados: +{puntosBase} pts\n" +
            $"{desglose}" +
            $"{desgloseVariante}" +
            $"{textoPenalizaciones}" +
            $"------------------------------\n" +
            $"<size=140%>TOTAL: {Mathf.Max(0, totalFinal)} PTS</size>";
    }

    public void ActualizarContadorBolsa(int cantidad)
    {
        if (textoDadosRestantes != null)
        {
            textoDadosRestantes.text = $"DADOS: {cantidad}";

            // OPCIONAL: Cambiar el color a rojo cuando queden pocos dados
            if (cantidad <= 10)
                textoDadosRestantes.color = Color.red;
            else
                textoDadosRestantes.color = Color.black;
        }
    }


    public void OcultarPanelResultados()
    {
        if (panelGameOver != null)
        {
            panelGameOver.SetActive(false);
            Debug.Log("Panel de resultados ocultado. ¡Disfruta la vista del tablero!");
        }
    }

}