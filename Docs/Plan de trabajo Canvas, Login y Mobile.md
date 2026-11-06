# Plan de trabajo — Canvas únicos, Login, UI adaptable y puesta a punto móvil

Alcance: **solo móvil (Android)**, orientación vertical. Este plan cubre los 5 puntos pedidos, el análisis de lo que falta y las dos fases finales (purga y optimización). Es un plan: todavía no se ha tocado código.

---

## 1. Diagnóstico (estado real del proyecto)

### 1.1 Canvases hoy

| Escena | Canvas | Modo | Escalado | Observación |
|---|---|---|---|---|
| Login | `Canvas` | Overlay | **ConstantPixelSize** | El «800×600» es ignorado: no escala con la pantalla. |
| MenuPrincipal | `MainMenuCanvas` | Overlay | Scale With Screen Size 1080×1920, match 0,5 | Es el bueno. |
| MenuPrincipal | `OnlineMenuCanvas` (**en runtime**, order 100) | Overlay | 1080×1920 | Lo crea `OnlineMenuController`. Contiene el botón ONLINE, que **vive fuera de los paneles del menú** y por eso se ve siempre. |
| cualquier escena | `[SettingsCanvas]` (runtime, order 500) | Overlay | 1080×1920 | Lo crea `SettingsPanel.Open`. |
| Tablero | `Canvas` | Overlay | 1080×1920 | HUD principal (Score, dados, jugadores, pausa, game over). |
| Tablero | `Canvas` bajo el tablero | Screen Space Camera | ConstantPixelSize | 1 hijo; no escala. |
| Tablero | `Canvas_PopUps Flotantes` | World Space | ConstantPixelSize | Textos flotantes de puntos. |
| Tablero | `OnlineHudCanvas` (runtime, order 50) | Overlay | 1080×1920 | Lo crea `OnlineHudController`. |

Total: 3 canvases en el menú (1 de escena + 2 de runtime), 5 en el tablero. Cada canvas es un batch aparte, otro raycaster y otra escala distinta.

### 1.2 Modo zurdo
Aparece en: `SettingsService` (clave, getter y setter), `SettingsPanel` (toggle + handler), `MainMenuManager` (campo, listener, carga), `UIManager.ApplyHandednessLayout`, el prefab `SettingsPanel`, `DharitzUiKitBuilder`, `Strings.csv` (`left_handed`) y **dos toggles en escenas** (MenuPrincipal «LEFT-HANDED MODE», Tablero «Left Hand Mode»).

### 1.3 Login hoy
La escena tiene una imagen de fondo y un único botón «Google Play». `AuthManager` inicia sesión **anónima sola** al arrancar y en cuanto termina carga el menú (`SceneManager.LoadScene(1)` dentro del evento `SignedIn`). Resultado: el botón casi nunca se llega a usar, no hay pantalla de carga, ni error visible, ni reintento, ni modo sin conexión, ni versión. Si no hay internet, el jugador se queda en una pantalla fija para siempre.

### 1.4 Adaptación a pantallas
No hay ningún tratamiento de **área segura** (notch, cámara perforada, barra de gestos). El tablero y el HUD usan anclas/posiciones fijas pensadas para 1080×1920 (9:16). Orientación: Portrait por defecto pero con landscape permitido.

---

## 2. Plan por puntos

### Punto 2 — Eliminar el modo zurdo (primero: es pequeño y reduce lo que hay que adaptar luego)
1. Quitar `LeftHanded`/`SetLeftHanded`/`KeyLeftHanded` de `SettingsService` y borrar la clave de PlayerPrefs al arrancar (limpieza única).
2. Quitar el toggle de `SettingsPanel` (script, prefab y `DharitzUiKitBuilder`) y reajustar el alto del panel.
3. Quitar campo/listener/carga en `MainMenuManager` y el método `UIManager.ApplyHandednessLayout` (dejando el layout por defecto).
4. Borrar los dos toggles de escena (MenuPrincipal, Tablero) y la clave `left_handed` de `Strings.csv`.
5. **Verificación**: búsqueda de «hand» sin resultados; menú y tablero sin referencias rotas («Missing») en consola.

### Punto 1 — Unificar canvases del menú y arreglar el botón ONLINE
Idea: **un solo canvas por escena** con capas ordenadas, y todo lo que se crea por código cuelga de él.
1. Crear `UiRoot` (script + prefab) con tres capas hijas: `Screens` (paneles de la escena), `Overlays` (online, ajustes) y `Toasts` (avisos, popups). `UiRoot.Instance.Overlay` entrega el padre para cualquier UI creada en runtime. Un solo `CanvasScaler` (Scale With Screen Size 1080×1920) y un solo `GraphicRaycaster`.
2. **Botón ONLINE**: eliminar el botón flotante de `OnlineMenuController`. La escena ya tiene el botón «Online (WIP)» del panel principal y `MainMenuManager.ShowOnlinePanel()`; se cablea ese botón y el panel online pasa a ser **un panel más** (se oculta con `ShowMainPanel/ShowSettingsPanel/ShowFreePlayPanel`). Así desaparece al entrar en Ajustes o en Un jugador.
3. `OnlineMenuController` deja de crear canvas: instancia su panel dentro de `Screens`. `SettingsPanel.Open` instancia en `Overlays` en lugar de crear `[SettingsCanvas]`.
4. Back de Android: el panel abierto se cierra primero; sin paneles abiertos vuelve al menú anterior (ver 3.1).
5. **Verificación**: 1 solo `Canvas` activo en MenuPrincipal (script de comprobación en Play); ONLINE solo visible en el panel principal; abrir/cerrar Ajustes y Un jugador no deja botones huérfanos.

### Punto 5 — Unificar canvases del tablero
1. `Canvas` overlay existente = canvas único. Capas: `Hud`, `Overlays`, `Toasts`.
2. **Canvas bajo el tablero (Screen Space Camera, 1 hijo)**: si es un fondo, pasa a ser un `SpriteRenderer` en el mundo o una `Image` al fondo de la capa `Hud` (según lo que sea; se confirma al abrirlo).
3. **`Canvas_PopUps Flotantes` (World Space)**: `PopUpManager` pasa a un pool de TMP en la capa `Toasts`, posicionando con `Camera.WorldToScreenPoint` y animando con DOTween. Se elimina el canvas world-space.
4. `OnlineHudController` cuelga de la capa `Overlays` del canvas único (en vez de crear el suyo).
5. **Verificación**: 1 `Canvas` en Tablero; popups de puntos, game over, pausa y HUD online visibles y por encima del orden correcto; capturas comparadas antes/después.

### Punto 4 — UI dinámica para cualquier pantalla (móvil)
1. **`SafeAreaFitter`** en el contenedor raíz de cada canvas: ajusta anclas a `Screen.safeArea` y reacciona a cambios (rotación bloqueada, plegables).
2. **Escalado**: `CanvasScaler` con referencia 1080×1920 y un `CanvasFit` que ajusta `matchWidthOrHeight` según la relación de aspecto (0 en pantallas más estrechas que 9:16 para no recortar ancho; 1 en más anchas/tablets).
3. **Tablero**: calcular el tamaño de celda/cámara a partir del rectángulo libre entre el HUD superior y la zona inferior (dados, emojis) en vez de valores fijos; la rejilla de 10×8 debe caber siempre con margen.
4. Revisar todas las anclas del HUD (arriba, abajo, esquinas) y pasar a anclas relativas con `LayoutGroup` donde haya listas (jugadores, emojis, ajustes).
5. Orientación: **bloquear Portrait** (`allowedAutorotateToLandscape*` = false).
6. **Matriz de verificación** (Device Simulator + capturas): 1080×1920 (9:16), 1080×2340 (19,5:9), 1080×2400 (20:9), 1440×3200, 720×1280, iPad-like 3:4 (1536×2048) y notch simulado. Criterio: nada recortado, nada solapado, botones ≥ 48 dp.

### Punto 3 — Login funcional con fondo y botones
Rediseño de flujo:
1. `AuthManager` deja de cargar el menú solo. Expone `SignInAsGuestAsync`, `SignInWithGooglePlayAsync`, `HasCachedSession`, estado (`Idle/Working/Failed/Ready`) y errores tipados.
2. `LoginController` (nuevo) sobre el canvas único de Login, con: fondo del bosque (como el menú), título/logo, **«Jugar como invitado»**, **«Entrar con Google Play»** (solo Android), **«Jugar sin conexión»** (acceso a modos locales), indicador de carga, mensaje de error con **Reintentar**, y versión (`Application.version`) abajo.
3. Si hay sesión en caché, se muestra «Entrando…» ~1 s y se pasa al menú automáticamente (sin pulsar nada).
4. Google Play: si la cuenta ya está vinculada a otro jugador de UGS, iniciar con ella (`SignInWithGooglePlayGamesAsync`); si no, vincular la actual (`LinkWithGooglePlayGamesAsync`).
5. Ajustes: sección **Cuenta** (nombre/ID corto, «Vincular Google Play», «Cerrar sesión»), que reemplaza la sección antigua del panel viejo.
6. Textos por `Loc` (ES/EN); botones y panel del kit de UI.
7. **Verificación**: sin internet → error + Reintentar + Sin conexión; con internet → invitado entra; sesión en caché entra sola; Google Play (en dispositivo real, no se puede probar en el Editor).

---

## 3. Lo que falta (análisis, solo móvil)

### 3.1 Imprescindible antes de publicar
- **Botón Atrás de Android**: no hay gestión; hoy cierra la app desde cualquier pantalla. Definir comportamiento por pantalla (cerrar panel → atrás → confirmar salida).
- ~~Panel de pruebas de red visible~~: revisado, `NetworkDebugLauncher` ya está limitado a Editor y builds de desarrollo (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`); no hay nada que hacer.
- **Modo sin conexión**: sin internet el juego se queda en Login. El modo local contra IA no necesita red.
- **Servidor dedicado en producción**: sigue pendiente el proveedor de hosting (Agones/Edgegap/GameLift/Rocket Science) y la prueba con 2 dispositivos reales.
- **Requisitos de Google Play**: `targetSdk` está en «Auto» (fijar el nivel que exija la tienda), política de privacidad, formulario de seguridad de datos, ficha, iconos adaptativos y capturas. Se publica como AAB (ya activo).
- **Pantalla de inicio (splash)**: sigue el de Unity; definir la propia.
- **Pruebas en dispositivo real**: nada de lo online ni de Google Play se ha probado fuera del Editor.

### 3.2 Funcionalidad pendiente del roadmap (Informe de pendientes)
- Tutorial interactivo (reglas de aislamiento del 1 y contacto de color).
- Modo campaña (mapa de niveles, estrellas, récords persistentes).
- Tienda de skins y personalización de dados/tablero; IAP y anuncios (los paquetes están instalados pero **no hay una sola línea de código** que los use).
- IA: entrenar y fijar dificultades (Easy/Normal/Hard) con modelos exportados; hoy `DarithzAgent` en Editor.
- Vibración: existe la etiqueta «Vibrations» en el tablero; confirmar que hay lógica real o quitarla.

### 3.3 Calidad y robustez
- Reconexión y abandono ya tienen lógica; falta una pantalla clara de «Conexión perdida» y de resultado final online (ganador, puntos, revancha).
- Estados vacíos y errores de red en el menú online (hoy solo texto de estado).
- Accesibilidad básica: tamaño mínimo de texto (≥ 14 sp equivalente) y contraste en los botones de madera.
- Telemetría/errores (Analytics + crash reporting) para saber qué falla en dispositivos reales.
- Pruebas automáticas: hay tests de Core (11); faltan tests de la capa online y de `Loc`/`SettingsService`.

---

## 4. Fase de purga (tras los puntos 1–5)

Principio: **medir antes de borrar**; cada borrado en su commit y con build de prueba.

1. **Repositorio**: `Assets/ML-Config/results` pesa **1,2 GB** y hay **221 ficheros de ML-Config en git**. Sacarlo de git (`git rm --cached`), añadirlo a `.gitignore` y valorar reescribir el historial si el tamaño del repo molesta.
2. **Paquetes sin uso** (cero referencias en código): `com.unity.purchasing`, `com.unity.services.levelplay`, `com.unity.services.vivox`, `visualscripting`, `timeline`, `device-simulator.devices` (conservar si se usa), `ai.assistant`, `antigravity.ide`, `multiplayer.center(.quickstart)`/`playmode`/`tools` (solo desarrollo; fuera del build). Decisión de producto: purchasing y levelplay se quitan **solo si** la monetización se retrasa; si no, se mantienen y se implementan.
3. **Módulos de Unity**: desactivar los que no se usan (cloth, terrain, terrainphysics, vehicles, vr, xr, wind, video, umbra, vectorgraphics, unityanalytics si no se usa, accessibility, adaptiveperformance…).
4. **Assets**: `Background for mobile games, forest` (102 MB), `Materials` (48 MB), `JMO Assets` (40 MB), `Mystic Realms Wooden` (125 MB), `Procedural Text Animator - LITE`, `HintsStarsLite`: marcar con referencias reales (Project > Find References / script de auditoría) y eliminar lo no usado: demos, escenas de ejemplo (Juice UI `Demo`, `Scenes`, Mystic escenas), TMP `Examples & Extras`, texturas y prefabs huérfanos.
5. **Código**: `SceneExporter`, `AdvancedSceneExporter`, `.txt` internos antiguos (`Fases de progamacion`, `Resumen`, `PatternSystem`) a una carpeta `Docs/` fuera de `Assets`, scripts no referenciados desde escenas/prefabs, panel antiguo de ajustes de MenuPrincipal, `UiButtonSfx` duplicados.
6. `GooglePlayGamesPlugin-2.1.0.unitypackage` dentro de `Assets` (3 MB, ya importado): borrar.
7. **Criterio**: build Android antes y después; el tamaño del AAB y el tiempo de arranque deben bajar; cero «Missing» en las 3 escenas; tests verdes.

## 5. Fase de optimización

1. **Calidad**: el perfil activo es **Ultra** con VSync 1. Dejar 2 niveles (Medio/Alto) y fijar `targetFrameRate` (60, o 30 en el menú) para batería; desactivar sombras/HDR/MSAA innecesarios del pipeline URP.
2. **Texturas** (lo más pesado): compresión **ASTC** (6×6 en fondos, 4×4 en UI), tamaño máximo por plataforma, sin mipmaps en UI, sin Read/Write, **Sprite Atlas** para los sprites de madera y UI (reduce draw calls).
3. **UI**: con canvases únicos, dividir en subcanvases solo para lo que cambia a menudo (timer/puntos), `Raycast Target` apagado en textos e imágenes decorativas, evitar `Layout Groups` anidados.
4. **TMP**: las fuentes dinámicas añadidas crecen en atlas; pasar a atlas estático multilenguaje (caracteres ES/EN) para evitar rasterizar en runtime.
5. **Código**: eliminar `FindObjectsByType` en bucles (p. ej. `UiButtonSfx.AttachToAll`, `LocalizedText.AttachToAll` solo en carga de escena: ya es así, mantener), cachear referencias, evitar asignaciones por frame en `OnlineHudController.Update` (strings de `Loc.T`: refrescar solo al cambiar el valor).
6. **Audio**: clips largos en *Streaming*, SFX cortos en *Decompress On Load*, mono, Vorbis calidad ~70.
7. **Build**: IL2CPP + ARM64 (**quitar ARMv7**: reduce tamaño), Managed Stripping en *Medium/High* tras probar, Code Stripping de módulos, App Bundle con compresión de texturas por dispositivo.
8. **Medición**: Profiler en dispositivo (CPU/Memoria/Draw calls) antes y después, objetivos: 60 FPS estables en gama media, < 150 MB de RAM, AAB < 100 MB (a confirmar al medir).

---

## 6. Orden de ejecución y ramas

| # | Rama (sobre la anterior) | Contenido | Riesgo |
|---|---|---|---|
| 1 | `feat/remove-left-handed` | Punto 2 | Bajo |
| 2 | `feat/ui-root-menu` | Punto 1 (+ `UiRoot`) | Medio (escena) |
| 3 | `feat/ui-root-board` | Punto 5 | Medio-alto (HUD y popups) |
| 4 | `feat/responsive-ui` | Punto 4 (área segura, escalado, tablero) | Medio |
| 5 | `feat/login` | Punto 3 | Medio (Google Play solo en dispositivo) |
| 6 | `feat/mobile-gaps` | 3.1 imprescindibles (Atrás, panel de pruebas, sin conexión, ajustes de build) | Bajo-medio |
| 7 | `chore/purge` | Fase de purga | Medio (borrados) |
| 8 | `perf/mobile` | Fase de optimización | Medio |

Cada rama: edición con hand-patch de YAML cuando se toquen escenas (evita ruido de Unity), verificación en Play con capturas en la matriz del punto 4, commit y push.

## 7. Preguntas abiertas (necesitan tu decisión)
1. ¿Se mantiene **landscape** en algún momento o bloqueamos Portrait definitivamente?
2. Monetización: ¿IAP y anuncios entran en 1.0? Decide si se purgan `purchasing`/`levelplay` ahora o se implementan.
3. ¿Hay **modo sin conexión** completo (IA local) desde Login, o solo invitado online?
4. ¿Mínimo Android soportado? Hoy API 25; subirlo a 26–28 reduciría pruebas y tamaño.

---

## 8. Estado de ejecución (puntos 1–5 y parte de «lo que falta»)

| Rama (apiladas) | Contenido | Estado |
|---|---|---|
| `feat/remove-left-handed` | Punto 2: modo zurdo eliminado (código, prefab, escenas, CSV; la clave de PlayerPrefs se borra al arrancar) | Hecho |
| `feat/ui-root-menu` | Punto 1: `UiRoot` (capas Overlays/Toasts), un solo canvas en el menú; ONLINE es el botón del menú y el panel online es una pantalla más | Hecho |
| `feat/ui-root-board` | Punto 5: un solo canvas en el tablero; fondo como `SpriteRenderer` bajo la cámara; popups en la capa Toasts; HUD online bajo pausa y fin de partida | Hecho |
| `feat/responsive-ui` | Punto 4: bloqueo vertical, `match` del canvas según aspecto, `SafeAreaOffset/Fitter`, `BoardCameraFitter` | Hecho |
| `feat/login` | Punto 3: Login con invitado, Google Play, sin conexión, carga, error y reintento; fondos «cover» en menú y online | Hecho (Google Play: solo probable en dispositivo) |
| `feat/mobile-gaps` | Botón Atrás de Android (`BackNavigation`) | Hecho |

**Verificación del encuadre (numérica, tablero, Editor):** 720×1280, 1080×1920, 1080×2340, 1080×2400, 1440×3200 y 1536×2048. En todas el tablero queda dentro de la pantalla y entre el HUD superior y el inferior (sin recortes ni solapes).

### Hallazgos nuevos durante la ejecución (requieren decisión)
1. **`isTrainingMode` está activo en la escena Tablero** (desde el commit «ML-Agent implemented»). Con él activo se desactivan los popups de puntos, la actualización de varios contadores de UI, el efecto del highlight local y el tamaño dinámico del tablero (celdas fijas de 1,1 para el sensor de la IA). En la práctica, la partida local se ejecuta en «modo entrenamiento». Hace falta decidir cómo separar «entrenar la IA» de «jugar con la IA» antes de publicar.
2. `DarithzAgent` sigue jugando el turno del jugador 0 en la escena local y genera errores al salir de Play (`DiceManager.DrawDie`, `GameManager.ResetGame`).
3. Los fondos de Menú, Ajustes y Modo libre estaban estirados a pantalla completa (se deformaban un ~20 % en 20:9); ahora cubren sin deformarse.
4. El campo `Screen.safeArea` del simulador del Editor no cambia con la resolución de la ventana Game: `SafeInsets` ignora áreas seguras que no corresponden a la pantalla actual (solo afecta al Editor).

### Pendiente de este plan
- Ajustes de build recomendados (ProjectSettings, no se tocaron porque el fichero tiene cambios locales tuyos): `targetSdk` fijo, quitar ARMv7, bloquear orientación Portrait en Player Settings, quitar el splash de Unity si la licencia lo permite.
- Fases de **purga** y **optimización** (secciones 4 y 5): no se han empezado; implican borrar assets, quitar paquetes y sacar `ML-Config/results` (1,2 GB) de git, así que se hacen cuando lo confirmes.

---

## 9. Decisiones del producto y cambios posteriores

Respuestas a las preguntas abiertas y lo que cambió en el código:

| Decisión | Efecto |
|---|---|
| Se mantiene **horizontal** | El juego gira en vertical y horizontal (`MobileDisplay` ya no bloquea; el bloqueo vertical del apartado 2 queda sin efecto). En horizontal el HUD cambia de disposición (`OrientationLayout`: jugador a la izquierda, puntos arriba a la derecha, extracción abajo a la derecha), `BoardCameraFitter` encuadra el tablero a toda la altura entre ambos lados y los paneles modales se reducen si no caben (`PanelFit`). Medido en 1920×1080, 2400×1080 y 2560×1600. |
| **IAP y anuncios se mantienen** | `com.unity.purchasing` y `com.unity.services.levelplay` **no se purgan**; hay que implementarlos (pendiente del roadmap). |
| **Sin modo invitado ni sin conexión** | Login solo con Google Play Games (Android). Una sesión guardada entra sola. En el Editor y en builds de desarrollo aparece «ENTRAR (DESARROLLO)» para poder probar sin cuenta. Eliminados `OfflineMode`, el botón sin conexión y sus textos. |
| `targetSdk` y API | Lo sube el equipo al nivel recomendado por Google Play (ProjectSettings no se toca desde aquí). |

### Separación del modo entrenamiento (rama `fix/training-mode-separation`)
- `GameManager.isTrainingMode` solo se activa de verdad si hay un entrenador de ML-Agents conectado (`Academy.IsCommunicatorOn`); jugando, se apaga solo aunque la escena lo tenga marcado. Con eso vuelven los popups, contadores, entrada táctil y el highlight local.
- La geometría del tablero es siempre la fija (celdas de 1,1 centradas): el Grid Sensor de la IA sigue alineado y es la cámara la que se adapta a cada pantalla.
- `DarithzAgent` solo toma el control de un jugador marcado como bot en la sesión (o del jugador 0 cuando hay entrenador).
- Verificado en Play: partida local con juego normal (robar, colocar, puntos 00050, popup y contadores).

---

## 10. Fase de purga (rama `chore/purge`, un commit por borrado)

Criterio: se calculó qué assets alcanzan las escenas del build, `Resources`, `Prefabs`, `Settings` y los ajustes del proyecto (`Tools`/`DharitzPurgeAudit`) y solo se borró lo que nadie referencia. Se conservan `purchasing`, `levelplay`, `GooglePlayGames`, `ExternalDependencyManager` y los paquetes de desarrollo que instaló el equipo.

| Qué | Ahorro | Nota |
|---|---|---|
| `ML-Config/results` y `ML-Agents/Timers` dejan de estar en git (siguen en disco, ignorados) | 1,2 GB en el árbol de trabajo | El historial sigue pesando ~1,1 GB: reducirlo exige reescribir historia (BFG/`filter-repo`), no hecho. Mover `results` fuera de `Assets` evitaría que Unity lo importe. |
| `Materials` (fondos antiguos + vídeo mp4 de 35 MB) | 48 MB | El `VideoPlayer` ya se había retirado con el canvas del fondo. |
| `Background for mobile games, forest` | 101 MB | Sin referencias. |
| `Sounds` (pack de sonidos y prefabs CFXR copiados) | 12 MB | `AudioManager` usa `Assets/Audio`. |
| `JMO Assets` (Cartoon FX Remaster) | 37 MB | Sin referencias. |
| `HintsStarsLite`, `Procedural Text Animator - LITE` | 17 MB | Sin referencias. |
| `Photon` (restos de Fusion), `LeanLocalization` (sustituido por `Loc`) | ~1 MB | |
| Mystic Realms Wooden: `Source`, `Scenes`, `Match-3 Demo`, guía PDF, sets de iconos sin uso | ~105 MB | Se conservan los sprites y prefabs que usa el kit de UI y los scripts (`BasePanel`). |
| TMP `Examples & Extras` (sin referencias) | 9 MB | Se restauraron las fuentes Roboto y Anton porque las usan los tests del propio paquete TMP. |
| Dados antiguos, demos de Juice UI | 6 MB | |
| Paquetes: `timeline`, módulos `director`, `visualscripting` y 13 módulos integrados (cloth, terrainphysics, vehicles, vr, xr, wind, video, umbra, vectorgraphics, unityanalytics, accessibility, adaptiveperformance, ai) | build más pequeño y compilación más rápida | Solo el `manifest.json` (sin los cambios locales del equipo); `packages-lock.json` se regenera. |
| Notas internas y planes de `Assets/Scripts` → `Docs/` | | Ya no se importan como assets. |
| `SceneExporter`, `AdvancedSceneExporter`, `SceneHierarchy.txt`, campos muertos de `GridManager` | | Utilidades de volcado de escena sin uso. |

`Assets` sin `ML-Config` pasó de ~470 MB a ~70 MB.

**Verificación**: compilación completa sin errores, 0 scripts perdidos en las 3 escenas y en los prefabs, partida local de prueba (robar, colocar, puntuar) sin errores en consola y tests EditMode: 173 correctos, 1 fallo (`InputModuleTests`, test del paquete ugui que espera el módulo de entrada antiguo; no depende de la purga).

**No tocado (decisión pendiente)**: paquetes de desarrollo que instalaste (`multiplayer.*`, `vivox`, `antigravity.ide`, `ai.assistant`), el panel de ajustes antiguo de MenuPrincipal (respaldo si falta el kit), reescritura del historial de git.

---

## 11. Fase de optimización (rama `perf/mobile`)

**Corrección de la purga.** Al borrar carpetas enteras de Mystic Realms Wooden (`Source`, `Match-3 Demo`, `SpritesGeneric/V` y `VI`) se llevaron por delante 30 ficheros que las escenas sí usan (iconos de deshacer y repetir, panel de dados, marco del dado actual, botón del libro de patrones, bandera de España). Se detectó al ver cuadros blancos en el tablero. Se restauraron todos los assets cuyo guid sigue referenciado (también en `chore/purge`) y se añadió `Tools/DharitzMissingRefs`, que busca referencias rotas en escenas y prefabs. Queda una referencia rota **anterior a la purga** (`Panel_Retractil / Image`, ya rota en `master`).

| Área | Cambio | Efecto medido o estimado |
|---|---|---|
| Fotogramas | 60 FPS en el tablero, 30 en login y menús; sin vSync de calidad | Menos batería en menús |
| Render | URP con HDR desactivado (juego 2D sin postproceso) | Sin buffer de color de 16 bits por píxel |
| Texturas | ASTC 4x4 / 6x6 en Android, sin mipmaps ni lectura, dados y avatares a 256 px, resto ≤1024 | Memoria de texturas estimada 73 MB → ~9 MB |
| Audio | Música (130 s) en *streaming*; efectos mono Vorbis q0,5; cortos descomprimidos, largos comprimidos en memoria | RAM de audio estimada 25 MB → ~1 MB |
| TMP | Fuentes de respaldo estáticas de 512×512 y no legibles (antes dos atlas dinámicos de 1024×1024 legibles) | Sin rasterizar en el móvil y sin copia en CPU |
| Código | HUD online: textos 4 veces por segundo y solo si cambian (el temporizador solo al cambiar el segundo); `BoardLogic` en caché | Sin strings ni mallas TMP nuevas cada fotograma |
| Build Android | Solo ARM64, *stripping* Low, acelerómetro a 0 Hz, vertical invertida desactivada | AAB sin librerías de 32 bits |

**Medido y descartado**
- *Sprite Atlas* de la interfaz: salían 10,5 Mpíxeles de páginas (los PSD de pictogramas arrastran todos sus recortes) frente a ~8,6 MB de las texturas sueltas ya en ASTC. Reduce *draw calls* pero gasta más memoria; solo merece la pena decidirlo con el Profiler en un dispositivo.
- Quitar *raycast target* de los textos: riesgo de romper el arrastre en listas con poco beneficio.
- *Stripping* Medium/High: necesita un `link.xml` para Netcode/UGS antes de probarlo.

**Pendiente de decisión o de dispositivo**
- `targetSdk` (lo sube el equipo), splash de Unity y el perfil real en un móvil: FPS, RAM y *draw calls* antes y después.
- `EDM4U` reescribió `mainTemplate.gradle` y `AndroidResolverDependencies.xml` al quitar ARMv7 (excluye `armeabi-v7a`, pero también cambió `ndkVersion` y `packaging`); no se ha comprometido porque no se ha podido validar con un build.
- Herramientas nuevas en `Tools ▸ Dharitz ▸ Optimizar` (texturas y audio) para repetir el ajuste si se añaden assets.
