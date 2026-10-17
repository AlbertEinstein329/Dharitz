using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Boton Atras de Android (el Input System lo entrega como la tecla Escape). Comportamiento por pantalla:
/// cierra el panel abierto; en el menu vuelve al panel principal; en el tablero abre o cierra la pausa; y en el panel
/// principal del menu o en el Login pide pulsar otra vez para salir. Se crea solo al arrancar.
/// </summary>
public class BackNavigation : MonoBehaviour
{
    private const float ExitWindowSeconds = 2f;

    private float exitDeadline = -1f;
    private GameObject exitToast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        DontDestroyOnLoad(new GameObject("[BackNavigation]", typeof(BackNavigation)));
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        Handle();
    }

    public void Handle()
    {
        // 1. Ajustes abierto: se cierra
        if (SettingsPanel.IsOpen)
        {
            SettingsPanel.CloseCurrent();
            return;
        }

        // 2. Menu principal: un panel secundario vuelve al principal; desde el principal, doble pulsacion para salir
        MainMenuManager menu = MainMenuManager.Instance;
        if (menu != null)
        {
            if (menu.mainPanel != null && !menu.mainPanel.activeSelf) menu.ShowMainPanel();
            else ExitOnSecondPress();
            return;
        }

        // 3. Tablero: abre o cierra la pausa
        LapKan.PanelPauseController pause = LapKan.PanelPauseController.Instance;
        if (pause != null)
        {
            pause.ToggleFromBackButton();
            return;
        }

        // 4. Login (o cualquier otra pantalla sin menu): doble pulsacion para salir
        ExitOnSecondPress();
    }

    private void ExitOnSecondPress()
    {
        if (Time.unscaledTime <= exitDeadline)
        {
            Application.Quit();
            return;
        }

        exitDeadline = Time.unscaledTime + ExitWindowSeconds;
        StartCoroutine(ShowExitHint());
    }

    private IEnumerator ShowExitHint()
    {
        OnlineUiKit kit = OnlineUiKit.Instance;
        RectTransform parent = UiRoot.Instance != null ? UiRoot.Instance.Toasts : null;
        if (kit == null || parent == null) yield break;

        if (exitToast != null) Destroy(exitToast);
        TMP_Text label = kit.CreateLabel(parent, string.Empty, new Vector2(0.5f, 0.12f), new Vector2(860f, 100f), 40f, TextAlignmentOptions.Center);
        LocalizedText.Bind(label, "back_again_to_exit");
        label.raycastTarget = false;
        exitToast = label.gameObject;

        yield return new WaitForSecondsRealtime(ExitWindowSeconds);
        if (exitToast != null) Destroy(exitToast);
    }
}
