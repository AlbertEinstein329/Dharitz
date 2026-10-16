# Dharitz: plan de trabajo del modo online

Fecha del análisis: 16-10-2026. Rama analizada: `master` (commit `9a054e84` más el arreglo de `ServerState`).

## 0. Estado actual (resumen)

| Pieza | Estado |
|---|---|
| Capa 0 (`Scripts/Core`): reglas puras, `MatchStateDTO`, procesadores Draw/ReDraw/Place/Move/Undo | ✅ Lista y usada en local |
| `GameManager.ServerState` | ✅ Restaurado (lo había borrado el merge `9a054e84`) |
| Scripts de red (`Scripts/Networking`) | ⚠️ Compilan, pero son esqueletos: nadie se suscribe a sus eventos |
| `NetworkManager` / objetos de red en escenas | ❌ No hay ninguno en `Login`, `MenuPrincipal` ni `Tablero` |
| Botones online en `MenuPrincipal` | ❌ `onlinePanel` sin asignar; ningún botón llama a `StartMatchmaking2P/4P` |
| Matchmaking real | ❌ `MultiplayerMatchmaker` es un mock (en Editor “encuentra” `127.0.0.1:7777` a los 3 s y fuera del Editor siempre falla) |
| Paquete `com.unity.services.multiplayer` | ❌ No instalado (sí están `netcode.gameobjects 2.13.3` y `authentication`) |
| Servidor dedicado (Multiplay / Game Server Hosting) | ❌ Sin build target ni configuración |
| Proyecto vinculado a UGS | ✅ `cloudProjectId 26034601-…` |

### Trabajo ya hecho que NO está en `master`

La rama `origin/claude/learn-codebase-x28b1b` tiene 2 commits sin mergear que resuelven buena parte de la fase 2:

- `4d290b9b`: inicio de partida autoritativo en el servidor (`CoreMatchSetup`), `NetworkMatchSnapshot` (estado público replicado tras cada acción), `OnlineMatchPresenter` (pinta los snapshots), `SessionConfig.isOnlineMatch`, enrutado de Draw/ReDraw/Place por RPC, `ServerForceEndTurn`, Undo/Move desactivados online y `NetworkDebugLauncher` (botones Host/Client).
- `e1d915c7`: menú de Editor `DharitzOnlineSetup` que crea los objetos de red en `Tablero`.

Esa rama es anterior al ML-Agent: reemplaza versiones viejas de `GameManager`, `TurnManager`, `GridManager`, `PlacementOrchestrator`, etc. **No hay que mergearla tal cual** (borraría `MLAgents*`, `ResetGame` y el modo entrenamiento). Hay que portar sus archivos nuevos y aplicar a mano sus cambios en los archivos existentes (fase 2).

## 1. Problemas concretos detectados en el código actual

1. **Cada cliente arranca su propia partida.** `GameManager.Start()` crea bolsa y semilla locales en todos los clientes, así que cada uno tiene una bolsa distinta. El servidor debe crear la partida y los clientes solo adoptar su estado.
2. **El servidor usa la lógica visual.** `NetworkGameManager.RequestPlaceDieServerRpc`, `NetworkTurnManager.HandleTurnTimeout` y `ReconnectionManager` llaman a `turnManager.EndTurn()`, que toca UI, `GridManager` y `SaveManager`. En un servidor headless eso rompe.
3. **Los clientes no pintan nada.** `OnServerDiePlaced` no tiene suscriptores. `NotifyDieDrawnClientRpc` actualiza `ServerState` pero no la mano, el tablero ni la UI.
4. **La UI no pasa por la red.** El botón de robar llama a `GameManager.DrawDie()` → `DiceManager` local. La colocación (`GridInteractionManager` → `PlacementOrchestrator`) también es local.
5. **Hay dos “turno actual” desincronizados.** `NetworkGameManager.NetworkCurrentPlayerIndex` y `NetworkTurnManager.NetworkPlayerTurn` no se actualizan juntos, y el temporizador no se reinicia al colocar un dado.
6. **El error de colocación llega a todos.** `NotifyPlacementFailedClientRpc` se envía a todos los clientes y filtra en el cliente. Debe usar `ClientRpcParams` con `TargetClientIds`.
7. **Los slots no sobreviven a una reconexión.** `HandleClientConnected` asigna `slot = Count`: un jugador que se reconecta recibe un slot nuevo. Hay que mapear por `AuthenticationService.PlayerId`, no por `clientId`. Además, `HandlePlayerReconnect` nunca se llama.
8. **Dos inicializaciones de UGS.** `AuthManager` (escena `Login`) y `NetworkBootstrap` hacen `SignInAnonymouslyAsync`. `NetworkBootstrap` además cambia de *profile* en el Editor, lo que genera otro PlayerId.
9. **Nadie escucha `OnMatchFound`**, así que nunca se conecta ni se carga `Tablero`.
10. **Undo/Move** (`UndoCommand`, `MoveCommand`) mutan `ServerState` localmente: online hay que desactivarlos o pasarlos por RPC.
11. **Build Settings con escenas de Photon Fusion** (`FusionIntroShared`, `FusionSampleMenu`) que ya no existen, más la carpeta `Assets/Photon/Fusion` como resto. Se usa Netcode for GameObjects, no Fusion.
12. **Valores fijos.** `maxDicePerPlayer = 52` está duplicado en `PlacementOrchestrator.RefreshPlacementHighlights`, y el tablero 10×8 está escrito a mano.

## 2. Plan por fases

Cada fase deja el juego jugable y se puede probar sola.

### Fase 1: Base y limpieza (½ día)

**Configuración**
- [ ] Quitar de *Build Settings* las 2 escenas de Photon y decidir si se borra `Assets/Photon` (con Fusion fuera, también `NetworkProjectConfig.fusion` y `PhotonAppSettings`).
- [ ] Instalar `com.unity.services.multiplayer` (Sessions + Matchmaker + Multiplay). Debe ser compatible con NGO 2.13.
- [ ] *Project Settings ▸ Services*: confirmar que el proyecto está vinculado (lo está) y activar **Authentication**.
- [ ] `NetworkManager`: crear `Assets/Prefabs/Network/NetworkPrefabsList.asset`.

**Scripts**
- [ ] `NetworkBootstrap`: no volver a iniciar sesión si `AuthManager` ya lo hizo. Usar *profile* aleatorio solo con *Multiplayer Play Mode* o un `#define`, no siempre en el Editor.
- [ ] Unificar el “turno actual”: `NetworkTurnManager` lee `NetworkGameManager.NetworkCurrentPlayerIndex` (eliminar `NetworkPlayerTurn` o hacerlo derivado).

### Fase 2: Partida autoritativa en `Tablero` (2-3 días): el núcleo

Portar desde la rama `claude/learn-codebase-x28b1b` (commit `4d290b9b`), adaptándolo al `master` actual.

**Scripts nuevos (copiar y revisar)**
- [ ] `Core/CoreMatchSetup.cs`: `CreateMatch(seed, players, variant)`, `AdvanceTurn`, `DiscardDrawnDie` (puros, sin Unity).
- [ ] `Networking/NetworkMatchSnapshot.cs`: `INetworkSerializable` con tableros comprimidos (reusar `NetworkBoardState`), grupos activos, puntajes, turno, `HasDrawn`, dado en mano y conteo de la bolsa por color. **El orden de la bolsa nunca sale del servidor.**
- [ ] `Networking/OnlineMatchPresenter.cs`: recibe snapshots y aplica la diferencia a `GridManager`, HUD, mano y popups.
- [ ] `Networking/NetworkDebugLauncher.cs`: botones Host/Client/Server para probar en el Editor.

**Scripts a modificar**
- [ ] `SessionConfig`: añadir `public bool isOnlineMatch;` (y `matchMode`, `localPlayerIndex`).
- [ ] `GameManager`:
  - En `Start()`: si `IsOnlineMatch`, **no** llamar a `InitializeBag` ni a `StartTurn` en clientes; esperar el primer snapshot.
  - `DrawDie()` / `UseReDraw()` / `BeginPlacement()` → si es online, llamar a `NetworkGameManager.Instance.Request…ServerRpc()`.
  - Añadir `AdoptServerState(MatchStateDTO)`.
- [ ] `NetworkGameManager`:
  - Iniciar la partida cuando estén todos los slots (`CoreMatchSetup.CreateMatch` con una semilla del servidor).
  - Draw/ReDraw/Place → Capa 0 → `BroadcastSnapshotClientRpc(snapshot)`.
  - Fin de turno con `CoreMatchSetup.AdvanceTurn` (nunca `TurnManager.EndTurn`).
  - `NotifyPlacementFailed` dirigido solo al emisor con `ClientRpcParams`.
  - Mapear slots por `PlayerId` de UGS (arregla la reconexión).
- [ ] `NetworkTurnManager.HandleTurnTimeout` → `NetworkGameManager.ServerForceEndTurn()` (descarta el dado en mano y avanza). Reiniciar el temporizador en cada cambio de turno.
- [ ] `ReconnectionManager`: eliminar por abandono vía `ServerForceEndTurn`, llamar a `HandlePlayerReconnect` desde `HandleClientConnected` cuando el `PlayerId` ya tenía slot, y reenviar el snapshot completo al reconectado.
- [ ] `CommandManager` / `GridInteractionManager`: desactivar Undo y Move si `IsOnlineMatch` (por ahora).
- [ ] `TurnManager`: en online, solo refrescar la UI (sin decidir turnos).

**Escena `Tablero`: GameObjects**

| GameObject | Componentes | Notas |
|---|---|---|
| `NetworkManager` | `NetworkManager`, `UnityTransport` | Raíz de la escena (o en `MenuPrincipal` con DontDestroyOnLoad, ver fase 3). Player Prefab vacío. Prefab List = `NetworkPrefabsList`. *Enable Scene Management* activado. Tick rate 30. |
| `NetworkServices` | `NetworkBootstrap`, `ReconnectionManager` | DontDestroyOnLoad (ya lo hacen los scripts). |
| `NetworkGame` | `NetworkObject`, `NetworkGameManager`, `NetworkTurnManager`, `EmojiChatManager` | **In-scene placed** NetworkObject, sin *Destroy With Scene*. |
| `OnlineMatchPresenter` | `OnlineMatchPresenter` | Referencias a `GridManager`, `UIManager`, `GameManager`. |
| `NetworkDebugLauncher` (solo dev) | `NetworkDebugLauncher` | Canvas pequeño con botones **Host**, **Client** y **Server**. Desactivar en builds. |

El menú `DharitzOnlineSetup` (commit `e1d915c7`) crea estos objetos automáticamente: conviene portarlo también (va en `Assets/Editor`).

**UI en `Tablero`**
- [ ] Texto **Temporizador de turno** (`TMP`), suscrito a `NetworkTurnManager.OnTimerTick`.
- [ ] Indicador **“Turno de X / Tu turno”** y bloqueo de los botones Draw/ReDraw cuando no es tu turno.
- [ ] Panel **“Esperando jugadores (n/N)…”** que se muestra hasta el primer snapshot.
- [ ] Panel **“Jugador desconectado (30 s)”** (eventos de `ReconnectionManager`).
- [ ] Barra de **emojis** (4-6 botones → `EmojiChatManager.SendEmoji(id)`) y burbuja sobre la fila del jugador (`OnEmojiReceived`).
- [ ] Botón **Abandonar partida** → `NetworkBootstrap.Disconnect()` y cargar `MenuPrincipal`.

**Prueba de cierre de fase:** dos instancias (Multiplayer Play Mode o build + Editor). Una pulsa *Host* y la otra *Client*. Se juega una partida completa 2P sin desincronización y el temporizador fuerza el fin de turno.

### Fase 3: Menú, matchmaking y sesiones (2 días)

**Scripts**
- [ ] `MultiplayerMatchmaker`: sustituir el mock por `MultiplayerService.Instance.MatchmakeSessionAsync(...)` (o primero `CreateSessionAsync` / `JoinSessionByCodeAsync` con Relay para salas privadas, más fácil de probar sin servidor dedicado). Colas `2Player` y `4Player`.
- [ ] Nuevo `OnlineFlowController` (en `MenuPrincipal`): escucha `OnStatusUpdated`, `OnMatchFound` y `OnMatchFailed`. Al encontrar partida, configura `SessionConfig` (`isOnlineMatch = true`, número de jugadores, variante), conecta (Relay o IP:puerto) y carga `Tablero` con `NetworkManager.SceneManager.LoadScene` (el servidor/host decide la escena).
- [ ] `MainMenuManager`: añadir `JoinByCode(string)` y `CreatePrivateRoom()` si se usan salas privadas.

**Escena `MenuPrincipal`: botones y paneles**

| Elemento | Acción (OnClick) |
|---|---|
| Botón **Online** en el panel principal | `MainMenuManager.ShowOnlinePanel` |
| `OnlinePanel` (asignar al campo `onlinePanel` de `MainMenuManager`; ahora está vacío) | — |
| ↳ Botón **Partida rápida 2J** | `MainMenuManager.StartMatchmaking2P` |
| ↳ Botón **Partida rápida 4J** | `MainMenuManager.StartMatchmaking4P` |
| ↳ Botón **Crear sala privada** (opcional) | `MainMenuManager.CreatePrivateRoom` |
| ↳ Campo **Código** + botón **Unirse** (opcional) | `MainMenuManager.JoinByCode` |
| ↳ Texto de **estado** (TMP) | lo actualiza `OnlineFlowController` |
| ↳ Botón **Cancelar** | `MainMenuManager.CancelMatchmaking` |
| ↳ Botón **Volver** | `MainMenuManager.ShowMainPanel` |
| GameObject `NetworkManager` + `NetworkServices` | Si el flujo arranca aquí, mover estos objetos de `Tablero` a `MenuPrincipal` (DontDestroyOnLoad) para que la conexión sobreviva al cambio de escena. **Solo debe existir un `NetworkManager`.** |

**Configuración UGS (Dashboard)**
- [ ] Matchmaker: crear las colas `2Player` y `4Player` con sus reglas (tamaño de equipo 1, 2 o 4 equipos) y el *pool* por defecto.
- [ ] Si se usa Relay para el host: activar **Relay** y usar `UnityTransport` con `RelayServerData`.

### Fase 4: Servidor dedicado (Multiplay / Game Server Hosting) (2-3 días)

- [ ] Instalar el módulo **Dedicated Server** para Linux en Unity Hub.
- [ ] Crear un *Build Profile* “Linux Dedicated Server” con `Tablero` (y una escena `ServerBoot` mínima) y el define `DEDICATED_SERVER`.
- [ ] `MultiplayServerManager`: usar `MultiplayService.Instance.ReadyServerForPlayersAsync()`, leer la *allocation* (puertos y payload de matchmaking con número de jugadores) y apagarse al terminar la partida o quedarse vacío.
- [ ] El servidor no debe cargar UI pesada: separar la lógica en `ServerBoot` o desactivar los canvas con `#if UNITY_SERVER`.
- [ ] Dashboard: *Build* → *Build Configuration* (comando y puertos) → *Fleet* (región) → enlazar la fleet al pool del Matchmaker.
- [ ] Probar el servidor localmente: `Dharitz.x86_64 -batchmode -nographics -port 7777`.

### Fase 5: Robustez y cierre (2 días)

- [ ] Reconexión completa: al volver, el cliente recibe el snapshot completo y retoma su slot.
- [ ] Fin de partida online: el servidor calcula las multas (`CoreSessionProcessor`), envía los resultados y cada cliente guarda monedas en Cloud Save (no `SaveManager` del servidor).
- [ ] Undo/Move online vía RPC (`CoreUndoProcessor` y `CoreMoveProcessor` ya existen en Core).
- [ ] Antitrampas básico: límite de RPCs por segundo y validar siempre `senderId` → slot.
- [ ] Telemetría mínima: partidas iniciadas, abandonos, desincronizaciones.
- [ ] Quitar `NetworkDebugLauncher` de las builds de release.

## 3. Orden recomendado y estimación

1. Fase 1: ½ día
2. Fase 2: 2-3 días ← **aquí empieza a funcionar el online de verdad (Host/Client)**
3. Fase 3: 2 días ← jugable entre dos dispositivos con salas o matchmaking
4. Fase 4: 2-3 días ← solo si se quiere servidor dedicado; con Relay + host se puede lanzar sin él
5. Fase 5: 2 días

Total aproximado: 9-11 días de trabajo.

## 4. Estado de ejecución (16-10-2026)

**Hecho y verificado en Unity (compila sin errores; prueba Host de 1 jugador: robar y colocar pasan por el servidor y el cliente refleja tablero, puntaje y turno):**

- Fase 1: escenas de Photon fuera de *Build Settings*. `NetworkBootstrap` ya no se toca (sigue inicializando UGS).
- Fase 2 (scripts): `CoreMatchSetup`, `NetworkMatchSnapshot`, `OnlineMatchPresenter`, `NetworkDebugLauncher`, `NetworkGameManager`, `NetworkTurnManager`, `ReconnectionManager` y `NetworkBoardState` portados de la rama online. `GameManager` enruta Draw/ReDraw/Place al servidor cuando `SessionConfig.isOnlineMatch`; Undo/Move quedan desactivados online.
- Fase 2 (escena `Tablero`): `NetworkManager` + `UnityTransport`, `NetworkGame` (NetworkObject, NetworkGameManager, NetworkTurnManager, EmojiChatManager, NetworkDebugLauncher), `NetworkServices` (NetworkBootstrap, ReconnectionManager) y `OnlineHud` (HUD por código: sala de espera, turno, temporizador, avisos de desconexión, emojis y botón SALIR; más `OnlineAutoConnect`).
- Fase 3 (menú): `OnlineMenuController` en `MenuPrincipal` (botón ONLINE → crear sala 2/4 jugadores o unirse por IP). Carga `Tablero` y `OnlineAutoConnect` arranca Host/Client. Verificado: crear sala 2J carga `Tablero` online y queda esperando al segundo jugador.
- Corregido: el host se desconectaba a sí mismo al arrancar, y el `DarithzAgent` de la escena robaba dados en partidas online (ahora queda inerte).
- Menú de Editor `Tools ▸ Dharitz ▸ Online` (1: montar `Tablero`, 1b: montar `MenuPrincipal`, 2/3: alternar modo prueba).

**Pendiente (necesita más de una máquina/instancia, cuentas o decisiones tuyas):**

- Probar una partida real 2J con dos instancias (Multiplayer Play Mode o una build + el Editor). Hasta ahora solo se probó con 1 jugador.
- Matchmaking por cola (UGS): instalar `com.unity.services.multiplayer`, crear las colas `2Player`/`4Player` en el Dashboard y sustituir el mock de `MultiplayerMatchmaker`. Hoy el mock solo sirve en el Editor.
- Slots por `PlayerId` de UGS para reconexión real (hoy un cliente que cae pierde su sitio) y reenvío de snapshot al reconectar.
- Fase 4 (servidor dedicado Multiplay/Linux) y Fase 5 (Undo/Move online, resultados y Cloud Save, antitrampas).
- Nota de Editor: si el puerto 7777 queda ocupado tras parar Play con la red activa, reinicia Unity (el socket queda colgado). Apaga la red antes de parar Play.

### Matchmaking por cola (16-10-2026, parte 2)

- Instalado `com.unity.services.multiplayer` 2.4.1. `MultiplayerMatchmaker` ya no es un mock: usa `MatchmakeSessionAsync` (quick join + Relay). Filtra por una propiedad indexada `dharitzMode` (2p/4p), porque `MaxPlayers` no es filtrable. Si no hay sala libre, crea una y espera.
- Menú: «Partida rápida 2/4 jugadores» → carga `Tablero` → `OnlineAutoConnect` lanza la búsqueda → el SDK arranca Host/Client sobre el `NetworkManager` de la escena.
- Verificado contra Unity Services: login anónimo, Lobby (sesión creada) y Relay (join code) responden bien.
- **Bloqueo abierto:** al arrancar el Host por Relay (DTLS) el Editor lanza `Burst failed to compile the function pointer SendCallback` (UnityTLS). Con UDP directo (salas por IP) el Host sí arranca. Probar: *Jobs ▸ Burst ▸ Enable Compilation* desactivado en el Editor, o una build Windows/Android. Sin este paso no se ha podido ver una partida rápida completa.
- Pendiente: slots por `PlayerId` de UGS para reconexión, y fases 4-5.

### Fase 5 (parcial, 16-10-2026, parte 3)

- **Reconexión por PlayerId:** `NetworkGameManager` ya no asigna el slot al conectar. Cada máquina (host incluido) llama a `RegisterPlayerServerRpc(PlayerId de UGS)`. Si el id ya tenía slot, se le devuelve (también si el ClientId cambió) y se envía un snapshot completo; si estaba eliminado por abandono, se rechaza. `MultiplayerMatchmaker.ReconnectAsync()` vuelve a la sesión y el HUD muestra un botón RECONECTAR cuando hay caída a mitad de partida.
- **Fin de partida online:** cada cliente suma sus monedas y guarda en Cloud Save (`OnlineMatchPresenter.RewardLocalPlayer`).
- **Verificado:** regresión de 1 jugador (Host, robar y colocar replicados) tras el cambio. **Sin verificar:** reconexión real con dos instancias (cerrar el cliente de Multiplayer Play Mode y pulsar RECONECTAR).
- **Decisión pendiente (Undo/Move online):** el turno online termina al colocar (no hay pausa de transición), así que el Undo "del último dado" local no tiene hueco. Hace falta decidir una regla: (a) fase de confirmar turno, (b) Undo solo del propio turno antes de confirmar, o (c) dejarlo desactivado online. Move depende de lo mismo.
- **Pendiente:** fase 4 (servidor dedicado Linux + Multiplay) y antitrampas básicas (límite de RPCs).
