using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class MoveActionButton : MonoBehaviour
{
    private Toggle moveToggle;

    private void Awake()
    {
        moveToggle = GetComponent<Toggle>();

        // Prevents memory leaks and duplicate listeners
        moveToggle.onValueChanged.RemoveAllListeners();
        moveToggle.onValueChanged.AddListener(OnToggleStateChanged);
    }

    private void OnToggleStateChanged(bool isOn)
    {
        if (GridInteractionManager.Instance == null)
        {
            Debug.LogError("[Architecture Error] GridInteractionManager is missing in the scene.");
            return;
        }

        // Send the dynamic boolean to the interaction manager
        GridInteractionManager.Instance.ToggleMoveMode(isOn);
    }

    // Fix for CS1061: This allows the Manager to reset the UI safely
    public void ResetButtonState()
    {
        if (moveToggle != null)
        {
            // CRITICAL: We use WithoutNotify to avoid firing OnToggleStateChanged again and causing infinite loops.
            moveToggle.SetIsOnWithoutNotify(false);
        }
    }

    private void OnDestroy()
    {
        if (moveToggle != null)
        {
            moveToggle.onValueChanged.RemoveListener(OnToggleStateChanged);
        }
    }
    public void LockToggle()
    {
        if (moveToggle != null)
        {
            moveToggle.SetIsOnWithoutNotify(false);

            // LA SOLUCIÓN AL ABUSO: Desactiva físicamente el botón
            moveToggle.interactable = false;

            Debug.Log("[Wildcard] Comodín Move agotado y bloqueado.");
        }
    }

    /// <summary>
    /// Permite al GridInteractionManager bloquear o desbloquear físicamente el botón
    /// dependiendo de si el jugador del turno actual tiene usos disponibles.
    /// </summary>
    public void SetInteractable(bool isInteractable)
    {
        if (moveToggle != null)
        {
            moveToggle.interactable = isInteractable;

            // Si el botón se vuelve no interactuable, nos aseguramos de apagar el visual
            if (!isInteractable)
            {
                moveToggle.SetIsOnWithoutNotify(false);
            }
        }
    }

}