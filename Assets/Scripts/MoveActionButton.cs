using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class MoveActionButton : MonoBehaviour
{
    [Header("UI Sprites")]
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Image targetButtonImage;

    private Toggle toggleComponent;

    private void Awake()
    {
        toggleComponent = GetComponent<Toggle>();

        if (targetButtonImage == null)
            targetButtonImage = GetComponent<Image>();

        // Registrar el evento de Unity de forma segura
        toggleComponent.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnDestroy()
    {
        toggleComponent.onValueChanged.RemoveListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isActive)
    {
        // Cambiamos el Sprite de forma dinámica respetando las buenas prácticas de UI
        if (targetButtonImage != null && activeSprite != null && inactiveSprite != null)
        {
            targetButtonImage.sprite = isActive ? activeSprite : inactiveSprite;
        }

        // Informamos al mánager de interacción que el modo de movimiento cambió
        if (GridInteractionManager.Instance != null)
        {
            GridInteractionManager.Instance.SetMoveModeActive(isActive);
        }
    }

    // Método de seguridad para desactivar el botón desde fuera si el movimiento concluye
    public void ResetButtonState()
    {
        toggleComponent.isOn = false;
    }
}