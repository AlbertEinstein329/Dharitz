using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoardPlayerDisplay : MonoBehaviour
{
    // Punto de acceso global para evitar acoplamiento fuerte en el Inspector
    public static BoardPlayerDisplay Instance { get; private set; }

    [Header("Referencias UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image avatarImage;

    [Header("Base de Datos Visual")]
    [SerializeField] private Sprite[] allAvatars;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Renderiza la información del dueño del tablero visible.
    /// </summary>
    public void Setup(string playerName, int avatarId)
    {
        if (nameText != null)
        {
            nameText.text = $"{playerName.ToUpper()}";
        }

        if (avatarImage != null && allAvatars != null)
        {
            if (avatarId >= 0 && avatarId < allAvatars.Length)
            {
                avatarImage.sprite = allAvatars[avatarId];
                avatarImage.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"BoardPlayerDisplay: El AvatarId ({avatarId}) de {playerName} está fuera de rango.");
                // Ocultamos la imagen para no dejar el avatar del jugador anterior renderizado por error
                avatarImage.gameObject.SetActive(false);
            }
        }
    }
}