using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Lean.Gui; // Librería de Lean GUI activa

public class UIPlayerRowController : MonoBehaviour
{
    [Header("Componentes de la Ranura")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private LeanButton botToggleButton;
    [SerializeField] private Image botStatusIcon;
    [SerializeField] private Button removeRowButton; // Botón para eliminar la ranura ([-] o [X])

    [Header("Sprites de Configuración")]
    [SerializeField] private Sprite humanSprite;
    [SerializeField] private Sprite botSprite;
    [SerializeField] private Sprite[] availableAvatars; // Tus 10 avatares del inspector

    private int playerIndex;
    private bool isBotState = false;
    private int currentAvatarId = 0;

    public void SetupRow(int index, string defaultName)
    {
        playerIndex = index;
        nameInputField.text = defaultName;
        isBotState = false;

        // ==========================================
        // 1. ASIGNACIÓN DE AVATAR ALEATORIO
        // ==========================================
        // Leemos directamente el avatar que el manager ya calculó y guardó para este jugador
        if (MainMenuManager.Instance.sessionConfig != null)
        {
            currentAvatarId = MainMenuManager.Instance.sessionConfig.players[index].avatarId;
        }

        // Sincronizamos la interfaz gráfica local
        UpdateVisuals();

        // Reportamos el ID del avatar asignado al maletín del MainMenuManager
        MainMenuManager.Instance.UpdatePlayerAvatar(playerIndex, currentAvatarId);

        // ==========================================
        // 2. CONFIGURACIÓN DEL BOTÓN REMOVE (REGLA DE UX)
        // ==========================================
        if (removeRowButton != null)
        {
            // Si soy el Jugador 1 (índice 0), ocultamos el botón para evitar accidentes
            removeRowButton.gameObject.SetActive(index > 0);

            // Limpieza y asignación del evento de borrado quirúrgico
            removeRowButton.onClick.RemoveAllListeners();
            removeRowButton.onClick.AddListener(OnRemoveClicked);
        }

        // ==========================================
        // 3. ANIMACIÓN DE ENTRADA Y LISTENERS
        // ==========================================
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);

        nameInputField.onValueChanged.RemoveAllListeners();
        botToggleButton.OnClick.RemoveAllListeners();

        nameInputField.onValueChanged.AddListener(OnNameChanged);
        botToggleButton.OnClick.AddListener(ToggleBotStatus);
    }

    private void OnRemoveClicked()
    {
        // Desactivamos el botón al primer toque para evitar doble clic bugeado
        if (removeRowButton != null) removeRowButton.interactable = false;

        // Animamos el encogimiento de la tarjeta individual hacia cero
        transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
        {
            // Al terminar de encogerse, le avisa al manager para que limpie datos e índices
            MainMenuManager.Instance.RemovePlayerDynamic(playerIndex);
        });
    }

    public void RefreshIndex(int newIndex)
    {
        // Actualiza el ID interno de la ranura de forma silenciosa cuando otra se elimina
        playerIndex = newIndex;
    }

    private void OnNameChanged(string newName)
    {
        MainMenuManager.Instance.UpdatePlayerName(playerIndex, newName);
    }

    private void ToggleBotStatus()
    {
        isBotState = !isBotState;

        // Feedback físico elástico al cambiar a Bot
        botToggleButton.transform.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), 0.2f, 10, 1f);

        UpdateVisuals();
        MainMenuManager.Instance.UpdatePlayerBotState(playerIndex, isBotState);
    }

    private void UpdateVisuals()
    {
        botStatusIcon.sprite = isBotState ? botSprite : humanSprite;

        if (availableAvatars != null && availableAvatars.Length > currentAvatarId)
        {
            avatarImage.sprite = availableAvatars[currentAvatarId];
        }
    }
}