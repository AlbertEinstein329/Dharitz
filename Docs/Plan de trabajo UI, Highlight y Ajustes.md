# Dharitz: plan de trabajo de UI, highlight de patrones y ajustes

Fecha: 16-10-2026. Origen: la prueba de una partida online completa funciona; quedan tres bloques de diseño y ajustes.
Este documento es **solo el plan**: nada de lo que se describe aquí está implementado todavía.

## 0. Resumen

| # | Bloque | Estado hoy | Esfuerzo | Prioridad |
|---|---|---|---|---|
| A | Botones online con **Mystic Realms Wooden** + **Juice UI** | Todo el HUD y el menú online están hechos por código con rectángulos planos y fuente por defecto | 2-3 días | Media |
| B | **Highlight** solo cuando se completa el patrón de la Variante | Se dispara siempre que el grupo se llena, aunque la forma no sea la del patrón (bug confirmado, ver B.1) | 1 día | **Alta** |
| C | **Sonido, música e idioma** | Los sliders de volumen no hacen nada y el selector de idioma no cambia ningún texto | 3-4 días | Alta |

**Orden recomendado:** B → C (audio) → A → C (idioma). El highlight es un bug acotado y de valor inmediato; el audio es independiente de la UI; el idioma va al final para traducir los prefabs nuevos de una vez y no dos veces.

## 1. Supuestos y preguntas abiertas

1. **«Highlight» = el efecto de patrón completado** (resaltado de los dados + overlay de celdas + sonido «Nice»), no el resaltado verde de casillas válidas. Lo deduzco de «solo si se completa el patrón definido en Variantes». *Si te referías al de casillas válidas, el bloque B cambia.*
2. **Idiomas a soportar:** supongo español (por defecto) e inglés. ¿Alguno más?
3. El patrón del número **1** hoy siempre cuenta como válido (`CorePatternValidator.CheckPattern` devuelve `true` si `TargetNumber == 1`). Lo mantengo salvo que digas lo contrario.
4. `NetworkDebugLauncher` (botones Host/Client por `OnGUI`) es solo de desarrollo: **no** se restiliza.

---

## 2. Bloque B: highlight solo al completar el patrón (prioridad alta)

### B.1 Diagnóstico (con evidencia)

El resaltado se decide con `group.isClosed` (`GroupData.isClosed => occupiedCells.Count >= targetSize`), es decir, **solo por cantidad de dados**, no por forma. El motor sí valida la forma (`CorePatternValidator.CheckPattern`), pero únicamente para dar el bonus de puntos; ese resultado nunca llega a la parte visual.

Prueba hecha en Unity con las tres variantes: colocar 3 dados en **línea** (el patrón 3 de cada variante no es una línea recta salvo la Variante 3, que es vertical y aquí se probó horizontal):

| Variante | Dado 3 en línea | Grupo cerrado | Forma coincide con el patrón | Puntos |
|---|---|---|---|---|
| Variante_1_Base (Easy) | aceptado | **sí** | **no** | +50 (sin bonus) |
| Variante_2 (Normal) | aceptado | **sí** | **no** | +50 (sin bonus) |
| Variante_3 (Hard) | aceptado | **sí** | **no** | +50 (sin bonus) |

Es decir: el motor no da bonus (correcto), pero la UI sí resalta y suena «Nice» (incorrecto).

Sitios donde se dispara el efecto sin comprobar la forma:

| Sitio | Archivo | Qué usa |
|---|---|---|
| Colocación local | `PlacementOrchestrator.cs` (~l.145) | `if (group.isClosed)` → «Nice» + `MarkGroupAsCompleted` |
| Colocación online | `NetworkGameManager.RequestPlaceDieServerRpc` → snapshot → `OnlineMatchPresenter` | `ClosedGroup = ...IsClosed` |
| Move (local y online) | `CoreMoveProcessor.ProcessMove` | marca `PatternCompleted` y da +100 si `Count == TargetSize`, **sin** `CheckPattern` |

### B.2 Trabajo

1. **Core (`MyGame.Core`, lógica pura):** nuevo `PlacementResult { Success, GroupClosed, PatternMatched, PatternBonus }` y una sobrecarga `ProcessPlacementIntent(..., out PlacementResult)`; la firma actual (devuelve `bool`) se conserva como envoltorio. `PatternMatched` sale de `CheckPattern` contra el patrón de la variante de la partida (`VariantConfig.GetPattern(TargetSize)`).
2. **Move:** `ProcessMove` usa `CheckPattern` antes de marcar `PatternCompleted` y de dar los +100. Hoy mover un dado dentro de un grupo ya completo vuelve a dar +100 cada vez (limitado por los usos de Move).
3. **Local:** `PlacementOrchestrator` y `MoveCommand` usan `PatternMatched`, no `isClosed`, para resaltar, sonar y mostrar el popup «PERFECT!».
4. **Online:** el snapshot añade `PatternCompleted` a `NetworkActionSnapshot` (se mantiene `ClosedGroup` para el contador de grupos); `OnlineMatchPresenter` resalta solo con `PatternCompleted`. El servidor es quien lo decide, el cliente no recalcula.
5. **Reconexión / Undo:** al aplicar un snapshot completo (reconexión) hoy no se repinta ningún highlight de patrones ya completados; hay que recalcularlo desde el estado (grupo cerrado + `CheckPattern`). Y al deshacer una colocación que completaba un patrón, el highlight debe retirarse.
6. **Grupo cerrado con forma incorrecta:** sin highlight, sin «Nice», sin bonus; solo el feedback normal de colocar. (Las reglas del juego no cambian.)
7. **Auditoría de datos de las Variantes** (`Assets/Resources/Variantes`, 3 variantes × 6 patrones): script que, para cada patrón, genera todas las orientaciones permitidas (`allowRotation`/`allowMirror`) y comprueba que `CheckPattern` las acepta y rechaza una forma distinta. Detecta patrones mal definidos (p. ej. `baseShape` vacío o con celdas repetidas).
8. **Opcional:** `VariantData.highlightColor` hoy solo tiñe el desplegable del menú (siempre blanco); usarlo para teñir el highlight del tablero.

### B.3 Criterios de aceptación

- 3 dados en línea con patrón en «L» → sin highlight ni «Nice» ni bonus, en local y online.
- Los 3 dados en la forma exacta (cualquier rotación/espejo permitida) → highlight, «Nice» y bonus.
- Mover un dado que rompe o completa la forma actualiza el highlight y los puntos correctamente.
- Tras reconectar, los patrones completados siguen resaltados.
- La auditoría de las 18 plantillas pasa sin avisos.

**Pruebas:** primeras pruebas automáticas del proyecto (EditMode) para `CorePatternValidator`, `CoreMatchProcessor` y `CoreMoveProcessor` con asmdef de tests que referencia `MyGame.Core`.

---

## 3. Bloque C: sonido, música e idioma

### C.1 Diagnóstico

**Sonido y música**
- Los sliders del menú (`bgmSlider`, `sfxSlider`) solo escriben `PlayerPrefs` (`Volume_BGM`, `Volume_SFX`). **`AudioManager` nunca lee esos volúmenes**, así que no cambian nada. No hay `AudioMixer`.
- Existen dos sistemas paralelos con claves distintas: volúmenes (menú) y mute (`Setting_MuteMusic/SFX`, panel de pausa). Ninguno aplica el otro y el menú no tiene mute.
- Hay un `AudioManager` en `MenuPrincipal` y otro en `Tablero`, cada uno con su propia biblioteca de sonidos duplicada (riesgo de que se desincronicen). `Login` no tiene ninguno.
- Un solo clip de música, sin transición entre escenas.
- Sonidos definidos pero sin usar: `Error`, `Reward`, `TimeOut`; `Tick` solo se usa en un sitio. El modo online no tiene sonidos propios (inicio de tu turno, cuenta atrás, timeout, confirmar, deshacer, emoji, desconexión) y los botones nuevos no suenan al pulsarse.

**Idioma**
- El desplegable solo guarda un índice (`Setting_Language`); **nada lo aplica**.
- `LeanLocalization` está importado en `Plugins/CW` pero **no hay ninguna instancia en ninguna escena** ni código que lo use. El paquete oficial de Localization no está instalado.
- Todos los textos están escritos a mano y mezclan español e inglés (`EXTRACT DICE`, `MATCH END`, `Match End`, «Jugador N», etc.).

### C.2 Trabajo: ajustes (sonido y música)

1. **`SettingsService`** (una sola fuente de verdad, sobre `PlayerPrefs`): volumen y mute de música, volumen y mute de SFX, idioma y modo zurdo; evento `OnChanged`; migración de las claves antiguas.
2. **`AudioMixer` `MainMixer`** con grupos Music y SFX y parámetros expuestos (conversión lineal→dB con `20·log10(v)`, mínimo −80 dB). `AudioManager` enruta sus `AudioSource` y aplica los ajustes al arrancar y al cambiar de escena.
3. **Un único `AudioManager`** como prefab con la biblioteca completa, colocado en `Login` (primera escena, `DontDestroyOnLoad`); se eliminan los de `MenuPrincipal` y `Tablero`.
4. **Panel de ajustes único** (prefab) usado desde el menú y desde la pausa: slider + mute de música, slider + mute de SFX, idioma, zurdo. Reutiliza `CheckBox`/`OptionsToggle` y `ButtonControl` (sprites on/off) de Mystic Realms. Se sincroniza al abrirse.
5. **Sonidos:** componente `UiButtonSfx` (reproduce «Click» vía `AudioManager`) en todos los botones; sonidos para los eventos online (tu turno, últimos 5 s con `Tick`, `TimeOut`, confirmar, deshacer, error de jugada, emoji recibido, desconexión/reconexión). Música: transición suave entre escenas.

### C.3 Trabajo: idioma

**Decisión:** usar **LeanLocalization** (ya importado, ligero, con soporte para TextMeshPro) en vez de instalar el paquete oficial de Localization.

1. Prefab `LeanLocalization` en `Login` (persistente) con los idiomas definidos en la pregunta 2.
2. Tabla de frases en CSV (`Assets/Localization/`) con claves estables (`menu.play`, `online.your_turn`, `hud.confirm_turn`…). Textos fijos de la escena → `LeanLocalizedTextMeshProUGUI`. Textos creados por código → helper `Loc.T("clave", args)` (para el HUD online, avisos, nombres de jugador).
3. El desplegable muestra los idiomas disponibles, aplica `LeanLocalization.SetCurrentLanguageAll(...)`, guarda la elección y, si no hay ninguna, usa el idioma del dispositivo.
4. **Fuente:** comprobar que el atlas TMP (`Luckiest Guy SDF`) incluye tildes, `ñ`, `¿`, `¡`; si no, añadir fuente de reserva.
5. Unificar los textos mezclados (todo pasa por claves; no queda cadena de UI escrita a mano).

### C.4 Criterios de aceptación

- Mover el slider de música/SFX cambia el volumen al instante y se conserva al reiniciar.
- El mute del menú y el de la pausa son el mismo estado.
- Hay un solo `AudioManager` y los sonidos suenan igual en las tres escenas.
- Cambiar el idioma actualiza **todos** los textos visibles (incluido el HUD online) sin reiniciar.
- Ningún texto de interfaz queda escrito a mano.

---

## 4. Bloque A: botones online con Mystic Realms Wooden + Juice UI

### A.1 Diagnóstico

- `OnlineMenuController` y `OnlineHudController` construyen la interfaz **por código** con `Image` de color plano, `Text` legacy con la fuente integrada e `InputField` básico. Ningún elemento usa los assets del juego.
- **Mystic Realms Wooden** (`Assets/Mystic Realms Wooden/Prefabs/01. Buttons`): botones `Image` (sprite *Sliced*) + `Button` + hijo TMP con `Luckiest Guy SDF`. Variantes de color (`ButtonRect Green/Blue/Brown/Cream/Cyan/Gray/Purple/Yellow`), cuadrados (`ButtonSquare`, `ButtonSquareSmall`) y hexagonales (`ButtonMission*`). Ojo: `ButtonRect` y `ButtonWood` llevan un script `SceneBase` (navegación de escenas) que **no** debe heredarse.
- **Juice UI Free** (`Assets/Juice_UI_Free/Scripts`): `ButtonAnimator_Free` (5 efectos de hover + 5 de click, sonido opcional), `PanelAnimator_Free`, `Animate_Pulse_Free`, `Animate_Shake_Free`. Requiere DOTween, que ya está instalado (`Resources/DOTweenSettings.asset`).

### A.2 Trabajo

1. **Inventario de elementos a restilizar**

| Pantalla | Elementos |
|---|---|
| Menú | botón ONLINE, panel, título, PARTIDA RÁPIDA 2J/4J, CREAR SALA 2J/4J, campo IP, UNIRSE POR IP, estado, VOLVER |
| HUD online | SALIR, emojis ×4, CONFIRMAR TURNO, RECONECTAR, sala de espera, turno, temporizador, avisos |

2. **Kit de prefabs** en `Assets/Prefabs/OnlineUI/` (variantes de los de Mystic, sin `SceneBase`):

| Prefab | Base | Uso |
|---|---|---|
| `OnlineButton_Primary` | `ButtonRect Green` | partida rápida, confirmar turno |
| `OnlineButton_Secondary` | `ButtonRect Blue` / `Brown` | crear sala, unirse, reconectar |
| `OnlineButton_Danger` | variante roja de Mystic | salir de la partida |
| `OnlineButton_Icon` | `ButtonSquareSmall` | emojis, cerrar |
| `OnlinePanel` | tablero de madera de `04. UI Elements` / `Board*` | panel online |
| `OnlineInputField` | `TMP_InputField` con sprite de ranura de madera | IP |
| Etiquetas | TMP `Luckiest Guy SDF` | todos los textos |

3. **Vida con Juice UI:** `ButtonAnimator_Free` en cada botón (hover *BounceScale*, click *Squeeze*); `PanelAnimator_Free` para abrir y cerrar paneles; `Animate_Pulse_Free` en CONFIRMAR TURNO (llama la atención) y en el temporizador cuando quedan ≤ 10 s; `Animate_Shake_Free` en el aviso de jugada inválida. El «Click» sale por `UiButtonSfx` (bloque C), no por el `AudioSource` propio de Juice, para respetar el volumen de ajustes.
4. **Refactor de los controladores:** `ScriptableObject` `OnlineUiKit` con las referencias a prefabs, sprites y fuente; `OnlineMenuController` y `OnlineHudController` pasan de `new GameObject` a `Instantiate(prefab)`. `DharitzOnlineSetup` asigna el kit. Se conserva la lógica de red tal cual.
5. **Android / móvil:** zonas táctiles ≥ 48 dp, respeto del *safe area*, mismo `CanvasScaler` que el resto del juego, sin romper la resolución de referencia 1080×1920.

### A.3 Criterios de aceptación

- Ningún `Image` plano ni `Text` legacy en la UI online; todo con sprites y TMP de Mystic Realms.
- Todos los botones reaccionan al hover y al click con Juice y suenan al pulsarse.
- Ningún botón arrastra el script `SceneBase`.
- Capturas del Editor en 1080×1920 y 2340×1080 revisadas sin recortes.

---

## 5. Fases, dependencias y riesgos

| Fase | Contenido | Depende de | Estimación |
|---|---|---|---|
| 1 | **B** completo (Core, local, online, reconexión, auditoría, tests) | — | 1 día |
| 2 | **C.2** servicio de ajustes, mixer, `AudioManager` único, panel de ajustes, sonidos | — | 1,5-2 días |
| 3 | **A** kit de prefabs, Juice, refactor de controladores | 2 (`UiButtonSfx`) | 2-3 días |
| 4 | **C.3** localización y revisión de textos | 3 | 1,5-2 días |

Total aproximado: **6-8 días**.

**Riesgos**
- *Patrones mal definidos:* la auditoría puede destapar plantillas con formas no válidas; habría que corregirlas en los assets de `Variantes`.
- *Escenas con YAML frágil:* eliminar los `AudioManager` duplicados y enlazar prefabs toca `MenuPrincipal` y `Tablero`; se hará con cambios mínimos (como en las últimas entregas) para no generar ruido en el diff.
- *Fuente y tildes:* `Luckiest Guy` puede no tener todos los glifos del español.
- *Juice Free* limita el número de efectos; si falta alguno, se cubre con DOTween directo.
- *Ramas:* esto parte de `master` tras mergear `feat/online-phase5`.

---

## 6. Estado de ejecución

### Fase 1 (bloque B, highlight): hecha y verificada (16-10-2026)

**Qué se cambió**
- `Core/CoreMatchProcessor`: nuevo `PlacementResult { Success, GroupClosed, PatternMatched, PatternBonus }` y sobrecarga `ProcessPlacementIntent(..., out PlacementResult)`; la firma antigua se conserva. `PatternMatched` solo es `true` si el grupo está lleno **y** `CorePatternValidator.CheckPattern` acepta su forma contra el patrón de la variante.
- `Core/CoreMoveProcessor`: `PatternCompleted` y los +100 solo si la forma coincide (antes bastaba con tener los dados; mover dentro de un grupo lleno repetía el bonus).
- `Core/CoreCompletion` (nuevo): `FindMatchedCells(board, variant)` deduce desde el **tablero** qué celdas forman patrones completados (agrupa por `GroupId`; el valor del dado es el tamaño objetivo). Se calcula del tablero y no de `ActiveGroups` porque ahí solo vive el grupo activo de cada color.
- Local: `PlacementOrchestrator` resalta y suena «Nice» con `PatternMatched`, no con `isClosed`. `UndoCommand` y `MoveCommand` recalculan el overlay (`GridManager.RecomputeCompletionOverlays`) tras deshacer/mover.
- Online: el snapshot lleva `PatternCompleted` en la acción y `MatchedCells` por jugador (decididas por el servidor). `OnlineMatchPresenter` pinta el overlay **desde el estado** (`GridManager.SetCompletionOverlays`): se repinta solo al reconectar y se retira al deshacer o mover. La animación de celebración solo ocurre en la acción que completa el patrón.
- `CellComponent.IsCompletionOverlayActive` (solo lectura) para consultar el overlay.

**Pruebas**
- 11 pruebas automáticas EditMode nuevas (`Assets/Tests/EditMode`, asmdef `Dharitz.Core.Tests`): forma con rotación/espejo/traslación, línea rechazada para un patrón en «L», rotación desactivada, colocación (cierra grupo sin patrón / patrón con bonus 200), `FindMatchedCells`, Move que forma o rompe la forma y su Undo. **11/11 pasan.**
- Herramienta `Tools ▸ Dharitz ▸ Auditar patrones de variantes` (`Assets/Editor/DharitzPatternAudit.cs`): las **18 plantillas** (3 variantes × 6) pasan: número y celdas coherentes, conectadas, aceptan todas las orientaciones permitidas y rechazan formas distintas y rotaciones no permitidas.
- En vivo (Host online): 3 dados en **línea** con patrón «L» → `PATRON=False`, +50, ningún overlay; forma en **«L»** → `PATRON=True`, +250 (50 + bonus 200), overlay en las 3 celdas; **Undo** retira el overlay y el puntaje vuelve; recolocar lo vuelve a encender.
- **Sin ejercitar en Play:** la ruta local (`PlacementOrchestrator`/`MoveCommand`). En esa escena el agente de ML (`DarithzAgent`) juega al jugador 0 y interfiere con una prueba automática. Es el mismo `PlacementResult` del Core que ya se verificó, pero conviene probarlo a mano una vez.

**Pendiente de este bloque (opcional):** usar `VariantData.highlightColor` para teñir el highlight del tablero.

### Siguientes fases
Fase 2 (ajustes de audio), Fase 3 (kit de UI con Mystic Realms + Juice) y Fase 4 (idioma) siguen sin empezar.

### Fase 2 (bloque C.2, sonido y música): hecha y verificada (16-10-2026)

**Qué se cambió**
- `SettingsService` (nuevo): única fuente de verdad de volumen y mute de música/SFX, idioma y modo zurdo sobre `PlayerPrefs`, con evento `Changed`. **Reutiliza las claves que ya existían** (`Volume_BGM`, `Volume_SFX`, `Setting_MuteMusic`, `Setting_MuteSFX`, `Setting_Language`, `Setting_LeftHanded`), así que los ajustes guardados siguen valiendo y no hizo falta migración.
- `AudioMixer` `Assets/Audio/MainMixer.mixer` con grupos **Music** y **SFX** y parámetros expuestos `MusicVolume` / `SFXVolume`. `AudioManager` convierte el volumen lineal a dB (`20·log10(v)`; 0 o mute = −80 dB) y lo aplica al mixer (sin mixer, directo a los `AudioSource`). Se guardan los ajustes al pausar/salir.
- **Un único `AudioManager`:** ahora es el prefab `Resources/AudioManager.prefab` (con la biblioteca de 10 sonidos y la música «Troll forest») y se instancia solo antes de la primera escena. Se retiraron las copias de `MenuPrincipal` y `Tablero` (el `Tablero` ni tenía música cuando se arrancaba directamente). Funciona igual arrancando desde Login, el menú o el tablero.
- `UiButtonSfx`: el sonido «Click» se añade solo a **todos** los botones y toggles de cada escena que se carga (respeta volumen, mute e interactividad). Se quitaron los 6 `PlaySFX("Click")` cableados a mano en `PanelPauseController`.
- `OnlineAudioFeedback` (lo añade `OnlineHudController`): sonidos de tu turno, cuenta atrás de los últimos 5 s («Tick»), tiempo agotado, confirmar turno, jugada rechazada (nuevo evento `NetworkGameManager.OnPlacementRejected`), desconexión/reconexión y emojis.
- Menú y pausa pasan por `SettingsService`: los sliders del menú ya **cambian el volumen de verdad** y los toggles de la pausa comparten el mismo estado.
- Herramienta `Tools ▸ Dharitz ▸ Audio` (`DharitzAudioSetup`) que crea el mixer, el prefab y limpia las escenas (idempotente).

**Pruebas (en el Editor)**
- Arranque desde `MenuPrincipal` y directo desde `Tablero` online: exactamente **1** `AudioManager`; la música suena en ambos; **15/15** botones del menú y **21/21** del tablero tienen `UiButtonSfx`.
- Curva del mixer: 0,5 → −6,0 dB; 0,25 → −12,0 dB; 1,0 → 0,0 dB; 0 y mute → −80 dB.
- Mover el slider del menú (rango 0..1) cambia `SettingsService` al instante; `AudioManager.SetMusicMute` y los toggles de la pausa ven el mismo estado.
- Sin errores nuevos en la consola.

**Decisiones y límites**
- El **panel de ajustes unificado en un prefab** (menú + pausa con slider y mute de música y SFX con el aspecto de Mystic Realms) pasa a la **fase 3**, junto con el resto del kit de UI: hacerlo ahora obligaba a estilizar dos veces. Hoy el menú tiene los sliders y la pausa los toggles de mute, ambos funcionando sobre el mismo estado.
- «Tick» usa el clip `Diceroll` de la biblioteca (el nombre del sonido y su clip no coinciden); si quieres un tic de cuenta atrás, basta cambiar el clip en el prefab.
- Tus ajustes guardados en el Editor tenían **música y SFX silenciados** (mute activado); se respetaron y se restauraron tras las pruebas. Por eso al pulsar Play no se oye nada hasta quitar el mute (toggle de la pausa).

### Fase 3 — Kit de UI con Mystic Realms Wooden + Juice UI (hecha)

**Qué se hizo**
- `OnlineUiKit` (`Resources/OnlineUiKit.asset`, `Scripts/UI/OnlineUiKit.cs`): fábrica de botones (Primary verde, Secondary azul, Neutral crema, Danger rojo, Close), paneles con banner, etiquetas TMP, campo de texto TMP, slider y toggle con los sprites de Mystic Realms. Los botones llevan `ButtonAnimator_Free` (hover con rebote, clic con squeeze) y `UiButtonSfx`; el panel usa `PanelAnimator_Free` (pop).
- `Tools ▸ Dharitz ▸ UI ▸ Construir kit de UI online` (`DharitzUiKitBuilder`) genera todo en `Assets/Prefabs/OnlineUI/` y es idempotente. Los botones son variantes de los `ButtonRect {Green,Blue,Cream,Yellow}` (los prefabs con `SceneBase` se evitan).
- **Fuentes**: las SDF de Mystic Realms son estáticas y no traen á é í ó ú ñ ¿ ¡. Se crearon fuentes dinámicas desde los TTF y se añadieron como respaldo global en `TMP Settings` (PassionOne primero, porque Luckiest Guy solo tiene mayúsculas).
- `SettingsPanel` (prefab): música y efectos (slider + mute), idioma ES/EN (`SettingsService.SetLanguageIndex`), modo zurdo. Se abre con `SettingsPanel.Open()` desde el menú (`MainMenuManager.ShowSettingsPanel`) y desde un botón AJUSTES creado en la pausa. El panel antiguo de la escena queda como respaldo.
- `OnlineMenuController` y `OnlineHudController` instancian del kit. Pulso en CONFIRMAR TURNO y sacudida del banner (`Animate_Shake_Free`). SALIR y los emojis se recolocaron para no solaparse con el HUD del tablero.

**Pruebas (Editor, 1080×2340)**: panel de ajustes, menú online y HUD online renderizan con las fuentes y sprites correctos, tildes y Ñ incluidas; 24/24 botones del tablero con `UiButtonSfx`; ajustes abiertos desde pausa y menú.

**Pendiente / límites**
- El idioma elegido se guarda pero aún no traduce textos: es la fase 4 (localización).
- Los textos del kit están en español en código; la fase 4 los pasa a claves.
- El efecto de atenuación del fondo del panel de ajustes no se apreció en la captura; revisar en dispositivo.

### Fase 4 — Localización español / inglés (hecha)

**Decisión**: no se usó LeanLocalization (exigía un prefab persistente y editar cada escena). Se hizo un servicio mínimo, sin tocar las escenas:
- `Loc` (`Scripts/Localization/Loc.cs`): tabla `Resources/Localization/Strings.csv` con columnas `key, es, en, aliases`. `Loc.T(clave, args)` devuelve el texto del idioma actual (si falta, el otro o la clave; admite `string.Format`). El idioma sale de `SettingsService.LanguageIndex` (0 = es, 1 = en; si el jugador no ha elegido, el del dispositivo: español si el sistema está en español, si no inglés).
- `LocalizedText`: mantiene un TMP traducido y se refresca al cambiar el idioma. Al cargar cada escena, `LocalizedText.AttachToAll` reconoce los textos originales por los *aliases* del CSV (sin distinguir mayúsculas) y les asocia su clave; así no se editó ninguna escena.
- Textos dinámicos por código (HUD online, menú online, estado del matchmaking —ahora clave + argumentos—, botón de extraer dado, fin de partida) usan `Loc.T`.
- El desplegable antiguo de idioma de la escena (English/Español/Português) se reemplazó en tiempo de ejecución por Español/English para coincidir con los índices.

**Pruebas (Editor)**: cambio de idioma en vivo con el menú online abierto (todo el panel pasa a inglés), 25 textos de escena enlazados al arrancar el menú, formato con argumentos correcto en ES y EN, sin errores nuevos en consola.

**Pendiente / límites**: nombres de marca (Google Play, Facebook, PLAYSTORE) no se traducen; los textos con número (puntos, dados) son dinámicos y no llevan clave; añadir un idioma = una columna nueva en el CSV y su código en `Loc.Codes`. Probar en dispositivo con idioma del sistema distinto.
