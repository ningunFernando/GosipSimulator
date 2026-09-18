# QWEN.md — DecoupledTemplate

Plantilla reutilizable de arquitectura Unity para futuros juegos. Se construye a partir de
`Assets/Docs/GUIA_PLANTILLA_ARQUITECTURA.md`, que es la fuente autoritativa de las 14 reglas no
negociables, la estructura de assemblies y el orden de construcción en 8 pasos.

**Lee la guía entera antes de escribir código.** Este archivo no la duplica: solo registra lo que
la guía no puede saber — las decisiones tomadas sobre este repo concreto y el entorno real
verificado, que difiere del que la guía asume.

## Alcance acordado con Fernando (2026-09-08)

El objetivo de la plantilla es **el EventBus tipado + el bootstrap desacoplado que arranca los
sistemas en orden verificable**. La cámara **no** es prioridad: el Paso 5 de la guía queda reducido
al mínimo imprescindible. Si algún día se añade cámara, R11 sigue vigente (un solo dueño del
suavizado, nunca Cinemachine y script en cascada).

**Materializado en el Paso 1 (decisión de Fernando, 2026-09-08): la plantilla tiene 7 assemblies, no
las 8 de la guía.** No existen `Assets/_Game/Runtime/CameraRig/` ni `DecoupledTemplate.Camera.asmdef`.
Toda verificación que en la guía diga `→ 8` (el Paso 1 y el *Definition of Done*) se lee `→ 7` aquí.
**Actualización del 2026-09-14:** con `DecoupledTemplate.Pickups` (ver "Más allá de la guía") vuelven a
ser 8, así que la cuenta coincide otra vez con la guía, pero no las assemblies: hay `Pickups`, no `Camera`.
Si se añade cámara, entra como hoja nueva del grafo (referencia a `Core`+`Data`, y solo `Debug` y
`Tests` la referencian) y hay que actualizar estas cuentas.

## Placeholders fijados (sección 2 de la guía)

`{Project}` = `{ROOT_NS}` = `{ASM}` = **`DecoupledTemplate`**, coherente con el nombre del repo. Las
8 assemblies son `DecoupledTemplate.{Core,Data,Player,Save,Pickups,Debug,Tests.EditMode,Tests.PlayMode}` y el
`rootNamespace` de cada una es `DecoupledTemplate.<Módulo>` (`DecoupledTemplate.Tests` en las dos de
tests). Decisión del 2026-09-08 tomada sabiendo que renombrar obliga a regenerar los proyectos y a
tocar todas las referencias: no cambiar a la ligera. La guía pide anotarlos en el `README.md`, que
todavía no existe (Paso 8); hasta entonces viven aquí.

## Proyecto de referencia

HamsterBall — de donde salen los patrones y la auditoría que motiva cada regla. Vive fuera de este
repo y se lee por **ruta absoluta**; no hay directorios incluidos en el contexto ni hace falta.

- Código: `/Users/ningunfernando/Projects/Unity/HamsterBall/HamsterBall/Assets/_Game`
  Relevante para el alcance acordado: `Systems/Core/EventBus.cs`, `Systems/Core/GameEvents.cs`,
  `Systems/Core/Bootstrapper.cs`, `Systems/Core/GameManager.cs`,
  `Systems/Core/StateMachines/`, `Systems/Core/Pool/`, `Systems/Save/`.
- Auditoría (el *porqué*, con el código real de cada bug):
  `/Users/ningunfernando/Projects/Unity/HamsterBall/Docs/AUDITORIA_ARQUITECTURA.md`
- Copia original de la guía:
  `/Users/ningunfernando/Projects/Unity/HamsterBall/Docs/GUIA_PLANTILLA_ARQUITECTURA.md`

**Nunca crear un symlink de HamsterBall dentro de `Assets/`.** Unity sigue symlinks: importaría los
23 scripts del otro proyecto y duplicaría tipos, asmdefs y GUIDs dentro de la plantilla, además de
meter el gameplay específico (bola-hámster, taxis, pasajeros) que la sección 1 de la guía prohíbe.

## Entorno real de este repo (verificado el 2026-09-08)

| Componente | Este repo | La guía asume |
|---|---|---|
| Unity Editor | `6000.6.0f1` | `6000.4.6f1` |
| URP | 17.6.0 | 17.4.0 |
| Input System | 1.20.0 | 1.19.0 |
| Test Framework | 1.8.0 | 1.6.0 |
| UGUI | 2.6.0 | 2.0.0 |
| Cinemachine | **no instalado** | 3.1.6 |

**Decisión: respetar las versiones instaladas.** No downgradear paquetes de Unity para coincidir
con la guía — no aporta nada a la arquitectura y rompe la Library.

**Decisión: no instalar Cinemachine y no tocar `Packages/manifest.json`.** Consecuencia: si llega a
existir un rig de cámara, `CameraRigTarget` es el dueño del suavizado, no Cinemachine.

**Excepción (2026-09-13): `jp.shiranui-isuzu.unity-mcp` sí entra en `manifest.json`**, fijado por git
URL a `#v4.3.3`. Es tooling para que los agentes (Claude Code, Qwen) manejen el Editor por MCP, no
una dependencia del juego: todo vive en `Editor/` y no llega a ningún build. La regla de arriba sigue
valiendo para paquetes de runtime. Subir de versión se hace cambiando el tag en el manifest (o con
`isuzu-unity-cli update`), no a mano en `Library/`.

Venían instalados y la guía preferiría que no (`ai.navigation`, `visualscripting`, `timeline`,
`collab-proxy`, más `ai.assistant` y `ai.inference` en pre-release). **Decisión de Fernando
(2026-09-14): quitados** `ai.inference` (y con él `dt.app-ui`, que solo traía ese paquete),
`ai.navigation`, `collab-proxy`, `timeline` y `visualscripting`. Nada del proyecto los usaba, y casi
todos los warnings del build eran shaders de `ai.inference`. También se quitaron sus restos: los defines
`SENTIS_ANALYTICS_ENABLED` y `APP_UI_EDITOR_ONLY` y la entrada de `dt.app-ui` en `EditorBuildSettings`.
`ai.assistant` ya no estaba en el manifest; solo queda
`ProjectSettings/Packages/com.unity.ai.assistant/Settings.json`, que no se tocó. Se mantienen
`ide.rider` e `ide.visualstudio` (integración con el IDE). Es la segunda excepción a "no tocar
`Packages/manifest.json`", y va en el sentido de la guía. Con `timeline` fuera, Unity MCP deja de
ofrecer sus tools de Timeline y Recorder.

## Estructura: desviaciones aceptadas respecto a la sección 4 de la guía

- **Repo plano.** La raíz del repo ES el proyecto Unity. No se crea el wrapper `{Project}/` que
  dibuja la guía. La ruta de trabajo es `<REPO>/Assets/_Game/…`. El wrapper solo servía para alojar
  contenido no-Unity, y `Docs/` ya cumple eso sin necesidad de mover el proyecto.
- **`Docs/` se queda dentro de `Assets/`** (`Assets/Docs/`). Decisión consciente: Unity genera
  `Assets/Docs.meta` y un `.meta` por cada `.md`, y aparecerán en los diffs de git. No moverla más
  adelante sin decidir antes qué hacer con esos `.meta` (ya habrán asignado GUID).
- **Qwen Code se arranca desde la raíz del repo**, no desde `Assets/`, para que este archivo,
  `.qwenignore` y `.qwen/` vivan donde tocan y la memoria de proyecto tenga una clave estable.
- **Sin `Runtime/CameraRig/` ni `{ASM}.Camera`.** Ver "Alcance acordado" arriba. Desde el 2026-09-14
  hay 8 assemblies por `Runtime/Pickups/`, que no está en la sección 4 de la guía.
- **Sin `Art/`, `Audio/`, `Shading/` ni `_Game/Settings/`.** La sección 4 de la guía los dibuja, pero
  ningún paso los llena nunca, y `Assets/Settings/` ya existe con los assets de URP: un segundo
  `Settings/` vacío solo invita a dudar de cuál manda. Una carpeta vacía es la versión-carpeta de los
  stubs que critica M11. Crearlas cuando haya contenido cuesta cero.
- **Sin `_Game/Docs/`.** La guía pone ahí el `ARCHITECTURE.md` del Paso 8; aquí va en `Assets/Docs/`,
  que es donde ya vive la guía.

## Reglas que aplican siempre

- Las 14 reglas de la sección 3 de la guía. Las que más fácil se relajan: **R1** (los 7 asmdef de
  este repo existen antes que cualquier `.cs`), **R3** (grafo acíclico: `Data` no referencia nada,
  `Core` solo a `Data`, los módulos de gameplay nunca entre sí), **R4** (gameplay se comunica solo
  por `EventBus`), **R5** (dominio en C# puro, el `MonoBehaviour` es un adapter fino), **R6** (nada
  de `FindAnyObjectByType` para cablear; quien crea un objeto conserva la referencia y la inyecta),
  **R10** (suscripciones en `OnEnable`/`OnDisable`, nunca en `Awake`), **R12** (cero stubs
  silenciosos), **R13** (ningún `Debug.Log` directo fuera de `Log.cs`).
- **Los movimientos y renombrados de `.cs` se hacen con el Editor de Unity abierto**, nunca desde el
  filesystem. Unity preserva los GUID de los `.meta` al mover dentro del Editor; por terminal se
  rompen las referencias de escenas y prefabs y aparece "Missing Script".
- **Un `.cs` nuevo escrito desde fuera del Editor puede quedarse sin compilar y sin ningún aviso.**
  Pasó el 2026-09-14 con `BootstrapperTests.cs`, creado mientras Unity recargaba el dominio: se importó
  como `MonoScript` de la assembly correcta, pero Unity no lo metió en su lista de fuentes (ni
  reimportarlo ni un build limpio lo arreglaron), así que no hubo error y la suite siguió en verde con
  los tests viejos. Síntoma: la cuenta de tests no sube. **Después de añadir tests, comprobar siempre la
  cuenta.** Arreglo que funcionó: renombrarlo y devolverle el nombre desde el Editor
  (`AssetDatabase.MoveAsset` ida y vuelta), que conserva el GUID y fuerza el alta en la lista.
- **No desuscribir `SceneManager.sceneLoaded` en el `OnDestroy` del `Bootstrapper`.** Cargar la escena
  de juego descarga `Scene_Bootstrap`, y eso destruye el `Bootstrapper` antes de que Unity dispare el
  evento: un `OnDestroy` que desuscribe deja la secuencia muda, la escena cambia pero `StartGame()`
  nunca corre y no hay ni un error en la consola que lo delate. El handler se desuscribe a sí mismo
  como primera línea y con eso basta para no dejar el evento estático colgado. Bug real encontrado al
  probar en Play Mode el 2026-09-08.
- **Todo lo que se escriba tiene un call site y un test que lo ejecuta.** El defecto central de
  HamsterBall fue código con apariencia de terminado que nunca se ejecutó: `EventBus` con 3
  `Publish` y 0 `Subscribe`, `SaveSystem.Save()` con 0 call sites, `ObjectPoolManager.Get()` con 0.
  Si un sistema todavía no va a tener consumidor, no se escribe.
- No commitear ni revertir cambios preexistentes del worktree: son de Fernando.

## Convenciones de escritura (obligatorias para cualquier agente)

Fijadas por Fernando el 2026-09-08. Aplican a todo lo que se escriba en este repo: código,
comentarios, mensajes de commit y documentación.

- **Cero emojis.** Ni en comentarios, ni en código, ni en strings de log, ni en documentación.
- **Cero em-dash (`—`).** En su lugar: coma, punto, dos puntos o paréntesis. Aplica también a la
  documentación en español. Los separadores de sección que la sección 7 de la guía dibuja con
  caracteres de caja (`─`, U+2500) no son em-dash y se mantienen.
- **Código y comentarios en inglés.** Nombres de tipos, métodos y campos, strings de log, XML doc y
  comentarios de código. La sección 7 de la guía ya exige un solo idioma en los logs: inglés.
- **Documentación en español.** `QWEN.md`, `Assets/Docs/*.md`, y en su momento `README.md` y
  `ARCHITECTURE.md`.

El texto preexistente de este archivo y de la guía contiene em-dash escritos antes de esta decisión.
No hacer reescrituras masivas: corregirlos solo en los párrafos que se toquen.

## Estado de la construcción

**Paso 1 completado el 2026-09-08** — commit `27caaa8` (19 carpetas + 7 `.asmdef` + sus `.meta`,
34 archivos). La higiene de git previa va en su propio commit, `36fe949`. Cero `.cs`, como exige R1.

Verificado por script y contra `Logs/Editor.log`, no de memoria: grafo acíclico y sin referencias
colgando · `Data` sin referencias · `Core` → solo `Data` · `Player` y `Save` → `Core`+`Data` y nunca
entre sí · `Debug` y `Tests` hojas · Unity importó los 7 asmdefs (`AssemblyDefinitionImporter` en los
7 `.meta`) y no reescribió el contenido de ninguno · cero `error CS` en el log. Unity informa
*"will not be compiled, because it has no scripts associated with it"* para los 7: es el estado
correcto hasta el Paso 2, no un fallo.

Lo único que sigue sin comprobar es el grafo **visual** en el Editor (`Window → Analysis → Assembly
Dependencies`, o abrir cada `.asmdef` y mirar *References*). La sintaxis con `||` del constraint de
`Debug` sí está resuelta: esa forma la usan paquetes de Unity instalados en esta misma versión
(`Unity.AI.Assistant.Runtime`, `Unity.AppUI`), así que el fallback de la sección 4.1 no hace falta.
El símbolo que usa, en cambio, sí es problemático: ver pendiente 4.

**Paso 2 completado el 2026-09-08** — commit `ac95464`, 13 `.cs` (858 líneas) en
`DecoupledTemplate.Core`. Unity los compiló a `Library/ScriptAssemblies/DecoupledTemplate.Core.dll`
con **cero errores y cero warnings**, comprobado sobre el trozo nuevo de `Logs/Editor.log` y no de
memoria.
Cero `Debug.Log` fuera de `Log.cs` (R13) · namespace en los 13 (R2) · cero `Find` (R6) · dos
`TODO(Fase-4)` con el formato de R12 · cero em-dash y cero emojis.

Dos defectos de la guía, corregidos al escribir el código:

- **§6.1 no compila.** `[Conditional("UNITY_EDITOR", "DEVELOPMENT_BUILD")]` es ilegal:
  `ConditionalAttribute` toma un solo string. La forma válida serían dos atributos apilados, que el
  compilador lee como OR.
- **`DEVELOPMENT_BUILD` está deprecado como directiva de compilación en Unity 6** y genera el warning
  `UAC0009` en cada compilado. `Log.Info` lleva `[Conditional("DEBUG")]`, el símbolo variant-aware
  que el propio aviso recomienda y que cubre la misma intención (Editor + development build).
  Verificado contra `Library/Bee/artifacts/*.dag/DecoupledTemplate.Core.rsp`: `DEBUG` sí está
  definido en el Editor, `DEVELOPMENT_BUILD` no.

**Paso 3 completado el 2026-09-08** — `GameConfigSO.cs` en `DecoupledTemplate.Data` y su cableado en
el `Bootstrapper`, que pasa de un `const GAME_SCENE_NAME` a leer `_gameConfig.GameSceneName`. Con eso
la arista `Core → Data` deja de estar muerta: ya hay un tipo de `Core` usando uno de `Data`. Unity
compila `DecoupledTemplate.Data.dll` y `DecoupledTemplate.Core.dll` con cero errores y cero warnings.
El asset `GameConfig_Default.asset` lo crea Fernando en el Editor, que es justo lo que ejercita el
`[CreateAssetMenu]`: si está mal escrito, no se descubre hasta ese momento.

Consecuencia estructural que conviene no olvidar: **`Data` no referencia nada (R3), así que ningún SO
puede usar tipos de `Core`.** `GameState` vive en `Core.State` y `PoolConfig` en `Core.Pool`, por lo
que un `GameConfigSO` no puede tener un campo de estado inicial ni una lista de pools; cualquier enum
que necesite tiene que declararlo dentro de `Data`. Si algún día se quiere configuración dirigida por
datos, hay dos salidas: mover el enum `GameState` a `Data` (la arista ya existe, y es más barato
antes del Paso 6, cuando aún no hay prefabs ni escenas que referencien `GameManager`), o declarar en
`Data` un enum propio que `Core` traduzca.

`GameConfigSO` lleva un solo campo a propósito: es lo único de `Core` configurable hoy sin romper R3,
y añadir campos sin consumidor para que el archivo parezca más completo sería el M11 que la guía
prohíbe. Tampoco lleva `OnValidate`, porque se dispara en cada pulsación del Inspector y cualquier
reescritura o aviso ahí pelea con quien está editando. La validación vive en el consumidor:
`Bootstrapper.ValidateConfiguration()` comprueba el SO y el nombre de escena **antes** de instanciar
nada y lanza excepción, en vez de fallar al final de la secuencia con un `LoadScene` incomprensible.

**Verificado en Play Mode el 2026-09-08.** Fernando montó adelantando parte del Paso 6
(`Scene_Bootstrap`, `Scene_Game`, `GameManager.prefab`, `ObjectPoolManager.prefab` y el asset
`GameConfig_Default`), y la secuencia sale completa en la consola y termina en `Scene_Game` con
`PlayState` activo: las once líneas de la secuencia más `Scene_Game loaded`, `Menu -> Play`,
`MenuState Exit`, `PlayState Enter` y `Game started`. Del Paso 6 queda el índice 0 de
`Scene_Bootstrap` en Build Settings, meter el `DebugHud` en `Scene_Game`, y comprobar que entrar en
Play desde `Scene_Game` directamente degrada con un error claro en vez de con una NRE.

**Paso 4 completado el 2026-09-08** — las tres capas de `DecoupledTemplate.Save`
(`ISaveStorage` + `JsonSaveStorage` de infraestructura, `ProgressService` de dominio, `SaveSystem`
de adapter) más `SaveData` versionado desde el día 1 y `SaveMigrations` con su cadena. La
verificación de la guía sale limpia: `MonoBehaviour` solo aparece en `SaveSystem.cs`. Compila con
cero errores y cero warnings. El save se escribe en
`~/Library/Application Support/DefaultCompany/DecoupledTemplate/save.json`.

El antiguo pendiente de cómo llegar al save sin romper R3 quedó resuelto con la salida (a): **`Core`
declara `ISaveLifecycle` (`Load`/`Save`) y el `Bootstrapper` instancia el prefab de `SaveSystem` como
`GameObject` sin tipo**, resolviendo el contrato con `GetComponent`, que lanza si el prefab no lo
lleva. `GameManager` no llega a conocer el save: la referencia la conserva el `Bootstrapper`, que es
quien la crea (R6). Es la única abstracción nueva del proyecto y no es especulativa: es la frontera
que R3 obliga a tener entre las dos assemblies.

Detalle de plataforma que la guía da por supuesto y este entorno no cumple: `File.Move(origen,
destino, overwrite)` **no existe** en el nivel de compatibilidad de API del proyecto (CS1501). La
escritura transaccional de `JsonSaveStorage` borra el destino antes de mover: el archivo final nunca
queda truncado, y si el proceso muere entre el borrado y el move, el `.tmp` conserva el payload.

**Verificado en Play Mode el 2026-09-08.** Con `SaveSystem.prefab` creado y asignado al campo nuevo
del `Bootstrapper`, la secuencia sale con sus tres pasos y `[SaveSystem] Loaded save v1.` entre el
paso 1 y el 2. Todavía no se escribe `save.json` en disco: nada muta el progreso, `_dirty` nunca se
activa, y eso es lo correcto. El round-trip de escritura y migración lo prueban los tests del Paso 7
contra una carpeta temporal.

**Paso 5 empezado el 2026-09-13: `DebugHud` hecho, falta `Player`.** En `Assets/_Game/Debug/`:
`DebugHudModel` (C# puro que arma el texto, testeable en EditMode sin panel ni Play Mode, R5),
`DebugHud` (adapter: se suscribe en `OnEnable` a `OnBootstrapComplete` y `OnGameStateChanged` y
actualiza un `Label`), y `PanelSettings_DebugHud.asset` con su tema `UnityDefaultRuntimeTheme.tss`.
En `Scene_Game` hay un GameObject `DebugHud` con `UIDocument` y `DebugHud`. Tests: 4 nuevos en
`DebugHudModelTests` y uno de PlayMode que arranca desde `Scene_Bootstrap` y lee el `Label`. Total:
46 EditMode y 2 PlayMode, todo en verde (tras el pendiente 5, el 2026-09-14: 50 y 4).

**Decisión de Fernando (2026-09-13): el HUD usa UI Toolkit, no Canvas + TextMeshPro** como dice el
Paso 5 de la guía. La intención de la guía (no usar `OnGUI`, M5) se cumple igual, porque UI Toolkit
es de modo retenido y no redibuja por frame. A cambio: `Debug` no necesita referencias a paquetes (es
un módulo del motor), no hay que importar TMP Essentials, y el `Label` lo crea el script, así que en
un build sin la assembly `Debug` el `UIDocument` queda vacío y no se ve nada. `OnGUI` sigue prohibido.

Detalle que sostiene el diseño: `UIDocument` reconstruye su árbol en su propio `OnEnable` y descarta
lo que se haya añadido antes. `DebugHud` añade el `Label` en su `OnEnable` y funciona porque
`UIDocument` declara `[DefaultExecutionOrder(-100)]`, comprobado por reflexión en `6000.6.0f1`. Si una
versión futura lo cambia, el test de PlayMode del HUD falla.

**Escala del HUD (2026-09-13).** `PanelSettings_DebugHud` usa `ScaleWithScreenSize` con referencia
1080x1920 y `match` 0.5, y el `Label` usa fuente de 32 puntos. Al crear el asset por código había
quedado en `ConstantPhysicalSize` con `referenceDpi` 257, el DPI de la pantalla Retina donde se creó:
medido en 1080x1920, el texto salía a 10.5 px y ocupaba el 1.6% del alto. Medido tras el cambio: 32 px
y 4.4% del alto en vertical 1080x1920; 37.4 px y 9.1% en horizontal 1920x1080. En UI Toolkit `match`
interpola en lineal, no en logarítmico como el `CanvasScaler` de UGUI, así que el mismo tamaño no se ve
igual en las dos orientaciones (escala 1.0 frente a 1.17). Si el juego va a ser horizontal, bajar la
fuente o subir `match` hacia 1 (escala por el alto). `referenceDpi` quedó en 96 para que volver a
`ConstantPhysicalSize` no herede el 257.

**Verificado en Play Mode el 2026-09-13.** Desde `Scene_Bootstrap`: 18 líneas de log, **cero errores
y cero warnings** (desaparecen los dos `Published ... with no subscribers`), y el HUD muestra
`Bootstrap: complete` y `State: Play`. Con eso quedan hechos también dos restos del Paso 6 anotados
arriba: `Scene_Bootstrap` ya está en el índice 0 de Build Settings y `DebugHud` ya está en
`Scene_Game`. Desde `Scene_Game` directamente no explota, y desde el mismo día da un error claro (pendiente 5).

**Paso 5 completado el 2026-09-14: `Player` en 3D con `Rigidbody`.** Decisión de Fernando: 3D en el
plano XZ, `Rigidbody`, teclado y gamepad, sin control táctil por ahora. Tres clases en
`DecoupledTemplate.Player`, que ahora referencia `Unity.InputSystem`:

- `PlayerInputReader` (`Runtime/Player/Input/`): lee `Player/Move` de `InputSystem_Actions` por
  `InputActionReference`, suscrito a `performed` y a `canceled` (§6.8). Solo deja pasar input en
  estado `Play`, que conoce por `OnGameStateChanged` (R4); es el segundo suscriptor real de ese
  evento. Si falta la referencia, registra el error y se deshabilita en `Awake` (R9).
- `PlayerMovement` (C# puro): velocidad horizontal con `Vector3.SmoothDamp` (§6.9), input recortado a
  longitud 1 y constructor que rechaza negativos y NaN.
- `PlayerMover` (adapter): en `FixedUpdate` escribe la velocidad horizontal del `Rigidbody` y conserva
  la vertical para la gravedad. Valida en `OnValidate` y otra vez en `Awake` (R8); con datos
  inválidos registra el error y se deshabilita.

En `Scene_Game`: `Ground` (plano de 50 x 50 m) y `Player` (cápsula en y = 1, rotación congelada,
interpolación), con la cámara en (0, 8, -10) inclinada 35 grados. Tests: `PlayerMovementTests` (9, en
EditMode; el de frame rate compara 30 y 120 fps con una tolerancia calculada simulando la fórmula de
`SmoothDamp`: difiere 0.012 m/s, frente a 0.38 del `Lerp` del reference) y `PlayerMoverTests` (2, en
PlayMode, con teclado virtual: mantener W mueve al jugador y soltarla lo detiene, pero solo en `Play`;
sin bootstrap no se mueve).

Dos detalles que no se ven en el código:

- **Teclado virtual en tests.** Por defecto el Input System solo entrega teclado a la Game view con
  foco, y una corrida lanzada desde terminal nunca lo tiene: las pulsaciones se pierden sin aviso. El
  test pone `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` y lo restaura en
  `TearDown`.
- **`InputSystem_Actions` son las acciones de todo el proyecto** (project-wide), que Unity ya habilita
  al arrancar. `PlayerInputReader` sigue §6.8 y deshabilita `Player/Move` en `OnDisable`: si algún día
  otro sistema lee esa misma acción, destruir al jugador se la apagaría.

**Paso 6 completado el 2026-09-14.** `Scene_Game` tiene cámara, luz, suelo, `Player` y `DebugHud` (sin
Cinemachine, por el alcance acordado). Verificado en Play Mode: desde `Scene_Bootstrap`, cero errores
y cero warnings, con el jugador quieto en (0, 1, 0); desde `Scene_Game`, solo el error del guard. Cero
referencias rotas en las escenas y en los 87 assets de `_Game`.

**Paso 7 al día el 2026-09-14:** 59 tests en EditMode (2.5 s) y 9 en PlayMode (2.0 s), con los 10
mínimos de la guía. Tres de PlayMode cubren la rama de error de `PlayerMover` y `PlayerInputReader`
(R9), y el de `PlayerInputReader` encontró un bug real: **en Unity, `enabled = false` dentro de `Awake`
llama a `OnDisable` en el acto**, antes de que haya corrido ningún `OnEnable`. Ese `OnDisable`
desreferenciaba la acción que faltaba y lanzaba una `NullReferenceException` justo después del error
claro. Ahora solo deshace lo que `OnEnable` hizo de verdad (`_isSubscribed`). Regla para cualquier
`MonoBehaviour` que se deshabilite en `Awake`: su `OnDisable` no puede suponer que `OnEnable` corrió.

**Definition of Done cerrado el 2026-09-14.** Los 10 checks por comando en verde: 7 asmdefs, 39 de 39
archivos con namespace, `Subscribe` solo dentro de `OnEnable`, prefijos de log iguales al nombre de su
clase. El grafo de assemblies se comprobó con `CompilationPipeline` en lugar de la ventana *Assembly
Dependencies* y coincide con la sección 4. Los puntos del Editor también: EditMode y PlayMode en verde,
Play desde `Scene_Bootstrap` sin errores ni warnings, Play desde `Scene_Game` con un error claro, cero
referencias rotas, `Scene_Bootstrap` en el índice 0 y el development build compilado (pendiente 4).

**Paso 8 completado el 2026-09-14.** `Assets/Docs/ARCHITECTURE.md` (grafo, flujo de arranque, eventos,
módulos, tests y resumen de reglas) y `README.md` en la raíz (requisitos, cómo abrir y jugar,
estructura, cómo correr los tests con su cuenta y duración, y cómo renombrar la plantilla para un juego
nuevo). `.gitignore` y `.qwenignore` ya cumplían. Con esto están hechos los 8 pasos de la guía, dentro
del alcance acordado (sin cámara).

**Más allá de la guía, 2026-09-14: pausa y ciclo de recolección.** Una revisión del runtime (sin contar
tests) mostró que tres sistemas solo corrían dentro de los tests: el pool (`Get` y `Return` sin ninguna
llamada y 0 pools configurados), el guardado (nada mutaba el progreso, así que `save.json` no se
escribía nunca) y la pausa (nada pedía `Paused`). Es justo el defecto central de HamsterBall que la
sección 1 de la guía prohíbe. Decisión de Fernando: resolverlo con un ciclo mínimo de recolección
(opción A), aunque roce el "nada de gameplay específico".

- **Pausa.** Acción `Player/Pause` (Esc y Start) → `PlayerInputReader` publica `OnPauseRequested` →
  `GameManager.TogglePause` alterna `Play` y `Paused`, y en cualquier otro estado la ignora con
  `Log.Info` (su primer call site) → `PausedState` pone `Time.timeScale` a 0 y al salir restaura el
  valor anterior. El input sigue llegando porque corre en tiempo sin escalar.
- **Recolección.** Assembly nueva `DecoupledTemplate.Pickups`, que solo referencia `Core` y `Data`. Con
  ella la plantilla vuelve a tener 8 assemblies, aunque no las de la guía (hay `Pickups`, no `Camera`).
  `PickupSpawner` saca un `Pickup` del pool por cada punto al llegar `OnBootstrapComplete`; cuando el
  jugador (tag `Player`) lo toca, lo devuelve, programa su reaparición con `RespawnQueue` (C# puro, en
  tiempo escalado, así que se detiene en pausa) y publica `OnPickupCollected`. `SaveSystem` lo convierte
  en moneda con `ProgressService.Earn` y guarda al entrar en `Paused` si hay cambios. El HUD muestra la
  moneda desde `OnProgressChanged`. `Pickup.prefab` y el pool `Pickup` (4 objetos) están en `Prefabs/`.
- **Bug real encontrado: los managers morían con `Scene_Bootstrap`.** Solo `GameManager` se marcaba con
  `DontDestroyOnLoad`. `ObjectPoolManager` y `SaveSystem` se destruían al cargar `Scene_Game`: el pool
  no existía durante el juego, y `OnApplicationPause` y `OnApplicationQuit` del save nunca corrían. El
  test de arranque no lo detectaba porque usaba `Assert.IsNotNull`, que no ve un `UnityEngine.Object`
  destruido. Ahora el `Bootstrapper` marca con `DontDestroyOnLoad` todo lo que instancia, y el test usa
  la comparación de Unity y comprueba que `SaveSystem` sobrevive. **Regla para cualquier test: con
  objetos de Unity, `Assert.IsTrue(obj != null)`, nunca `Assert.IsNotNull(obj)`.**
- **Tests:** 70 en EditMode (2.7 s) y 12 en PlayMode (4.6 s). Nuevos: `TogglePause` en
  `GameManagerTests` (con un `timeScale` previo de 0.5, para probar que se restaura y no se asume 1),
  moneda en `DebugHudModelTests`, `RespawnQueueTests`, `PauseFlowTests` (Esc con teclado virtual pausa,
  congela al jugador y reanuda) y `PickupFlowTests` (recoger da moneda, el pickup vuelve al pool y
  reaparece en su punto, y pausar escribe el save, redirigido a una carpeta temporal para no tocar el
  real). Los tests que pausan restauran `Time.timeScale` en su `TearDown`: un fallo a mitad de pausa
  congelaría todos los tests siguientes.
- **Verificado en Play Mode desde `Scene_Bootstrap`:** pool vivo, un solo `SaveSystem`, 4 pickups en sus
  puntos, pausa y reanudación por el bus con `timeScale` 0 y 1, y cero errores y cero warnings, también
  al salir de Play. Con frames avanzados a mano (ver la nota siguiente), el ciclo completo en una partida
  real: recoger sube la moneda a 1 y el HUD la muestra, pausar escribe `save.json` en disco
  (`{"saveVersion":1,"currency":1,"totalEarned":1}`, el primer save que la plantilla ha escrito nunca) y
  el pickup reaparece en su punto a los 2 s de juego. Esc enviado con `input_key` a la Game view no llega
  al Input System (son eventos de ventana del Editor), así que la tecla física solo está cubierta por el
  test con teclado virtual.
- **`Run In Background` está desactivado en *Player Settings*.** Con Unity sin foco, Play Mode no avanza
  ni un frame aunque figure como en marcha: una prueba manual lanzada desde la terminal se queda en `Menu`
  sin ningún error. Para verificar sin foco hay que avanzar frames con `play_mode_step`, que deja warnings
  internos de Unity (`JobTempAlloc ... older than 4 frames`) que no salen jugando normal. Los tests no se
  ven afectados. **Decidido el 2026-09-14: activado.** Comprobado sin foco y sin `play_mode_step`: el
  arranque llega a `Play` en menos de un segundo.
- `ProjectSettings/TimeManager.asset` cambió solo: Unity 6.6 reserializó `Fixed Timestep` como fracción
  (2822399/141120000 = 0.02). Es el mismo valor, del mismo tipo de cambio que ya se commiteó en `59713b4`.

### Pendientes

1. **La guía existe dos veces** (`Assets/Docs/` aquí y `HamsterBall/Docs/`). La autoritativa es la de
   este repo; si se edita, la otra diverge en silencio. (El pendiente de rastrear `QWEN.md`,
   `.qwenignore` y `Assets/Docs/` quedó resuelto por Fernando en el commit `2e4ed28`.)
2. **Resuelto el 2026-09-13.** `OnBootstrapComplete` y `OnGameStateChanged` ya tienen suscriptor
   (`DebugHud`), y Play Mode desde `Scene_Bootstrap` sale sin warnings.
3. **Resuelto el 2026-09-14.** `Log.Info` tiene su primer call site real: `GameManager.TogglePause`
   cuando ignora una petición de pausa fuera de `Play` o `Paused`.
4. **Resuelto el 2026-09-14: el constraint de `Debug` funciona en builds reales.** Dos builds de macOS
   (Mono) con `isuzu-unity-cli`, escritos fuera del repo, y cada uno ejecutado sin ventana
   (`-batchmode -nographics`) unos 15 s para leer el log del player:
   - **Development** (44 s de build): incluye `DecoupledTemplate.Debug.dll`, y la secuencia sale
     completa y sin warnings.
   - **Release** (15 s): no incluye `DecoupledTemplate.Debug.dll`, que es lo buscado. El log confirma las
     dos consecuencias esperadas: `The referenced script on this Behaviour (Game Object 'DebugHud') is
     missing!` y `[EventBus] Published OnBootstrapComplete with no subscribers.`, porque en release
     nadie más escucha ese evento. `OnGameStateChanged` no avisa: lo escucha `PlayerInputReader`.
     **Rehecho tras la pausa y la recolección:** el build de release ya no muestra ese warning, porque
     `PickupSpawner` también escucha `OnBootstrapComplete`. Sigue el aviso del script ausente de
     `DebugHud`, y el player carga el `save.json` escrito desde el Editor (moneda 1).
   - `DEVELOPMENT_BUILD` en `defineConstraints` no dispara `UAC0009` en ninguna compilación (cero
     apariciones en `Logs/Editor.log`), así que se deja como está.
   - Los 628 warnings del build eran prácticamente todos de shaders de `com.unity.ai.inference` (Sentis).
     **Resuelto el 2026-09-14:** con el paquete quitado, el build pasó a 3 warnings.
   - `DECOUPLEDTEMPLATE_VERBOSE` estaba definido para Standalone, así que `Log.Trace` compilaba también en
     release. **Resuelto el 2026-09-14:** `Log.Trace` lleva `[Conditional("DEBUG")]` y
     `[Conditional(VERBOSE)]` apilados (se leen como OR) y el símbolo se quitó de *Player Settings*.
     Comprobado con builds reales: el development build escribe las 19 trazas del arranque y el de
     release ninguna. Para diagnosticar un release, añadir el símbolo a mano.
5. **Resuelto el 2026-09-13: Play Mode desde `Scene_Game` da un error claro.** Antes degradaba en
   silencio (consola vacía, HUD en `pending`/`unknown`). Ahora `Bootstrapper.CheckEntryScene`, con
   `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, registra un `Log.Error` si la primera escena
   cargada depende del bootstrap, y dice desde qué escena entrar. La regla vive en
   `DependsOnBootstrap`: índice de build mayor que 0, salvo la escena del Test Runner. Se descartó
   ponerlo en `DebugHud`: detectar el arranque es asunto del bootstrap, y así funciona en cualquier
   escena y sin la assembly `Debug`.
   - **Las escenas fuera del build (índice -1) no avisan a propósito**: son escenas de prueba sueltas.
   - **La escena del Test Runner (`InitTestScene<guid>`) se excluye por nombre.** Durante una corrida
     de PlayMode reporta un índice de build positivo (visto en `6000.6.0f1` con Test Framework 1.8.0).
     La primera versión del guard suponía -1 y cada corrida empezaba con un error falso en la consola.
   - Tests: `BootstrapperTests` (la regla, 4 casos, incluido `InitTestScene` con índice positivo),
     `EntryGuard_FromGameScene_LogsClearError` (espera el error y ninguna excepción en los frames
     siguientes) y `EntryGuard_FromBootstrapScene_LogsNothing`. Los dos de PlayMode comprueban además
     que el atributo sigue puesto, porque invocan el método a mano.
   - Verificado: tras la corrida de PlayMode la consola solo tiene el error que espera el test. En Play
     Mode manual, desde `Scene_Game` sale un solo error y nada más, y desde `Scene_Bootstrap` cero
     errores y cero warnings.

6. **Una corrida de PlayMode puede informar 0 tests y parecer verde.** El proyecto tiene las *Enter Play
   Mode Options* activadas desde el primer commit (`m_EnterPlayModeOptions: 3`: sin recarga de dominio
   ni de escena). Comprobado el 2026-09-14: tras entrar en Play con `play_mode_play` de Unity MCP,
   `verify --test --test-mode play` ejecuta 0 tests hasta la siguiente recarga de dominio (recarga: 6
   tests; Play manual: 0; otra recarga: 6). No se probó con el botón de Play ni desde la ventana del
   Test Runner. Solución de uso, anotada en el README: forzar `EditorUtility.RequestScriptReload()`
   antes. **Decidido el 2026-09-14: se mantienen**, porque entrar en Play es mucho más rápido; el
   aviso de las corridas con 0 tests queda documentado en el README.
   Mientras sigan activadas, los estáticos sobreviven entre sesiones de Play: hoy lo aguantan `EventBus`
   (lo limpia el `Bootstrapper`) y `GameManager.Instance` (se anula en `OnDestroy`), y cualquier estático
   nuevo tiene que resetearse igual.
7. **Los tests de PlayMode no compilarían en un player sin development build.** `Tests.PlayMode`
   referencia `Debug` (`BootstrapSequenceTests` lee el HUD), y en un build de release esa assembly no
   existe. Solo afecta a correr los tests de PlayMode en un player, que se hace con development build;
   en el Editor no pasa nada. Deducido del grafo, no comprobado con un build de tests. Se deja así.

### Notas para el trabajo siguiente

- **Referencias a paquetes en los asmdef.** El `autoReferenced` de un paquete solo afecta a las
  assemblies predefinidas de Unity (`Assembly-CSharp`), no a las nuestras: cualquier tipo de un paquete
  nuevo necesita su referencia explícita (como `Unity.InputSystem` en `Player`) o da un `CS0246`
  despistado.
- **Control táctil sin decidir.** Si el juego acaba siendo para móvil en vertical (Fernando prueba en
  1080x1920), ojo: el `OnScreenStick` del Input System funciona sobre UGUI, no sobre UI Toolkit.
- **`namespace DecoupledTemplate.Debug` sombrea `UnityEngine.Debug`.** Dentro de ese namespace,
  `Debug.Log(...)` resuelve al namespace y da `CS0118`. R13 (todo logging por `Log.cs`) lo hace
  improbable. Ojo: los dos asmdef de tests ya referencian `Debug`, así que un `Debug.Log` sin calificar
  dentro de `DecoupledTemplate.Tests` también daría `CS0118`.
