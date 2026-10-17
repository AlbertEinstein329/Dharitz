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
- **Panel de pruebas de red visible**: los botones «Start Host / Start Client / Shutdown» se ven arriba a la izquierda en el tablero. Deben estar solo en builds de desarrollo (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`).
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
