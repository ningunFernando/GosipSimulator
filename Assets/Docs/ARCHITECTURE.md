# Arquitectura de DecoupledTemplate

Plantilla reutilizable de arquitectura para juegos en Unity 6. Su objetivo es un **EventBus tipado**
más un **bootstrap desacoplado que arranca los sistemas en un orden verificable**, con reglas que
impiden repetir los fallos auditados en el proyecto de referencia (HamsterBall).

- La especificación completa (14 reglas, 8 pasos, *Definition of Done*) está en
  [`GUIA_PLANTILLA_ARQUITECTURA.md`](GUIA_PLANTILLA_ARQUITECTURA.md).
- Las decisiones tomadas sobre este repo, el estado de cada paso y los pendientes están en
  [`QWEN.md`](../../QWEN.md), en la raíz del repo.
- Cómo abrir la plantilla, correr los tests y adaptarla a un juego nuevo: [`README.md`](../../README.md).

Este documento describe **cómo está hecha la plantilla hoy**, no cómo debería hacerse: donde el repo
se aparta de la guía, lo dice.

## Assemblies

Ocho assemblies. La guía también dibuja ocho, pero no las mismas: aquí no hay `Camera` (fuera del
alcance acordado) y sí hay `Pickups`, el módulo mínimo que da uso real al pool y al guardado. Todas
usan el prefijo `DecoupledTemplate` y su namespace raíz coincide con el nombre de la assembly.

| Assembly | Referencia | Contenido |
|---|---|---|
| `Data` | nada | ScriptableObjects de configuración (`GameConfigSO`) |
| `Core` | `Data` | `Log`, `EventBus` y eventos, `GameManager` y estados (pausa incluida), `Bootstrapper`, pool de objetos, contrato `ISaveLifecycle` |
| `Save` | `Core`, `Data` | Guardado en tres capas; convierte recolecciones en progreso y guarda al pausar |
| `Player` | `Core`, `Data`, `Unity.InputSystem` | Input (movimiento y pausa) y movimiento del jugador |
| `Pickups` | `Core`, `Data` | Objetos recolectables que salen del pool y vuelven a él |
| `Debug` | `Core`, `Data`, `Player` | HUD de desarrollo. Solo compila con `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| `Tests.EditMode` | `Core`, `Data`, `Save`, `Player`, `Debug`, `Pickups` | Tests de lógica pura, solo Editor |
| `Tests.PlayMode` | `Core`, `Data`, `Save`, `Player`, `Debug`, `Pickups`, `Unity.InputSystem` | Tests de extremo a extremo con escenas reales |

```
   Debug              Tests.EditMode / Tests.PlayMode
     │                 (referencian todos los módulos)
     ↓
   Player          Save          Pickups
     │               │              │
     └───────────────┼──────────────┘
                     ↓
                   Core
                     ↓
                   Data
```

Reglas del grafo (R3): `Data` no referencia nada del proyecto; `Core` solo a `Data`; los módulos de
gameplay (`Player`, `Save`, `Pickups`) referencian `Core` y `Data` y **nunca entre sí**; `Debug` y los
tests son hojas. Cuando un módulo necesita a otro, la respuesta es un evento: `Pickups` no sabe que
`Save` existe, solo publica `OnPickupCollected`.

Dos consecuencias que no se ven a simple vista:

- **`Data` no puede usar tipos de `Core`.** Un ScriptableObject no puede tener un campo `GameState`
  ni una lista de `PoolConfig`: esos tipos viven en `Core`.
- **`Core` no puede ver `Save`.** Por eso `Core` declara `ISaveLifecycle` (`Load`/`Save`) y el
  `Bootstrapper` instancia el prefab de `SaveSystem` como `GameObject` sin tipo y resuelve la
  interfaz con `GetComponent`.

## Flujo de arranque

`Scene_Bootstrap` está en el índice 0 de Build Settings y solo contiene un GameObject con el
`Bootstrapper`, que tiene asignados en el Inspector los tres prefabs de sistema y el
`GameConfig_Default`.

```mermaid
sequenceDiagram
    participant B as Bootstrapper
    participant GM as GameManager
    participant S as SaveSystem
    participant P as ObjectPoolManager
    participant Bus as EventBus
    participant G as Scene_Game

    B->>B: ValidateConfiguration (lanza si falta algo)
    B->>Bus: ClearAllSubscriptions
    B->>GM: Instantiate + DontDestroyOnLoad (estado Menu)
    B->>P: Instantiate + DontDestroyOnLoad
    B->>S: Instantiate + DontDestroyOnLoad y GetComponent<ISaveLifecycle>
    Note over B: yield un frame
    B->>S: Load
    Note over B: yield un frame
    B->>GM: RegisterManagers(pool)
    B->>P: InitializePools
    Note over B: yield un frame
    B->>G: LoadScene (el Bootstrapper se destruye con su escena)
    G-->>B: sceneLoaded (el handler se desuscribe solo)
    B->>Bus: Publish OnBootstrapComplete
    B->>GM: StartGame
    GM->>Bus: Publish OnGameStateChanged (Menu a Play)
```

Puntos del diseño que tienen un porqué concreto:

- **Validar antes de instanciar.** Una configuración incompleta lanza excepción al principio, no a
  mitad de la secuencia con los managers ya vivos (R9, A3).
- **El `Bootstrapper` es dueño de la vida de los managers** y los marca con `DontDestroyOnLoad` al
  crearlos. Cargar `Scene_Game` destruye todo lo que quede en `Scene_Bootstrap`: cuando solo
  `GameManager` se marcaba a sí mismo, el pool y el guardado morían en ese momento y las referencias a
  ellos seguían pareciendo asignadas.
- **`ClearAllSubscriptions` solo aquí**, antes de instanciar nada, para que ningún suscriptor
  persistente quede sordo tras recargar una escena (A1).
- **El handler de `sceneLoaded` se desuscribe a sí mismo.** No se hace en `OnDestroy`: cargar
  `Scene_Game` destruye el `Bootstrapper` antes de que Unity dispare el evento, y un `OnDestroy` que
  desuscribe deja el juego sin arrancar y sin ningún error en la consola.
- **`OnBootstrapComplete` se publica al cargar la escena, no al final de la secuencia**, porque los
  objetos de `Scene_Game` se suscriben en su `OnEnable` durante esa carga (R10).
- **Inyección explícita (R6).** El `Bootstrapper` conserva lo que instancia y se lo pasa a
  `GameManager`; los objetos de escena leen el pool de `GameManager.PoolManager` cuando llega
  `OnBootstrapComplete`. Nada del runtime usa `FindAnyObjectByType`.

### Entrar en Play desde otra escena

`Bootstrapper.CheckEntryScene` corre con `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`. Si la
primera escena cargada está en el build con índice mayor que 0, registra un error que dice desde qué
escena entrar. Las escenas fuera del build (índice -1) no avisan, y la escena propia del Test Runner
(`InitTestScene`) se excluye por nombre porque durante una corrida reporta un índice positivo.

## EventBus y eventos

`EventBus` es estático y tipado por `struct`. Avisa con un warning cuando se publica un evento sin
suscriptores, lo que hace visible un bus decorativo (A1). Las suscripciones se hacen siempre en
`OnEnable` y se retiran en `OnDisable` (R10).

| Evento | Lo publica | Lo escuchan |
|---|---|---|
| `OnBootstrapComplete` | `Bootstrapper`, al cargar `Scene_Game` | `DebugHud`, `PickupSpawner` |
| `OnGameStateChanged` | `GameManager`, solo si la transición cambió el estado | `DebugHud`, `PlayerInputReader`, `SaveSystem` (guarda al entrar en `Paused`) |
| `OnPauseRequested` | `PlayerInputReader`, con Esc o Start | `GameManager` (alterna `Play` y `Paused`) |
| `OnPickupCollected` | `PickupSpawner`, cuando el jugador toca un pickup | `SaveSystem` (lo convierte en moneda) |
| `OnProgressChanged` | `ProgressService`, tras mutar el progreso | `SaveSystem` (marca el save como sucio), `DebugHud` |

### El ciclo de recolección

Es el único recorrido que ejercita a la vez el pool, el bus, el guardado y el HUD en tiempo de juego:

```mermaid
flowchart LR
    Pool[ObjectPoolManager] -->|Get| Spawner[PickupSpawner]
    Spawner -->|coloca| Pickup
    Player -->|toca| Pickup
    Pickup -->|Collect| Spawner
    Spawner -->|Return y RespawnQueue| Pool
    Spawner -->|OnPickupCollected| Save[SaveSystem]
    Save -->|Earn| Progress[ProgressService]
    Progress -->|OnProgressChanged| Save
    Progress -->|OnProgressChanged| HUD[DebugHud]
```

`SaveSystem` escribe el archivo al entrar en `Paused` (si hay cambios), además de en
`OnApplicationPause(true)` y `OnApplicationQuit`.

## Módulos

### Core

- **`Log`**: el único sitio que llama a `UnityEngine.Debug` (R13). `Trace` e `Info` compilan con `DEBUG`
  (Editor y development builds) y desaparecen de un build de release, llamada y string incluidos. `Trace`
  compila además si se define `DECOUPLEDTEMPLATE_VERBOSE`, para diagnosticar un release a propósito.
  `Warn` y `Error` siempre compilan.
- **`GameManager` y la state machine**: el estado actual se deriva de la máquina en cada lectura,
  nunca se guarda en un segundo campo (R7). Pedir un estado sin implementación (`GameOver`) registra
  un error y no muta nada.
- **Pausa**: `GameManager.TogglePause` alterna `Play` y `Paused` cuando llega `OnPauseRequested`; en
  cualquier otro estado la petición se ignora. `PausedState` pone `Time.timeScale` a 0 y al salir
  restaura el valor anterior. El input sigue llegando durante la pausa porque corre en tiempo sin
  escalar; la física, `FixedUpdate` y los temporizadores con `deltaTime` se detienen.
- **`ObjectPoolManager`**: los objetos salen activos también cuando el pool se expande, y devolver dos
  veces el mismo objeto se ignora con un aviso (C1, M3). Hoy tiene un pool, `Pickup`, con 4 objetos.

### Save

| Capa | Tipo | Responsabilidad |
|---|---|---|
| Infraestructura | `ISaveStorage`, `JsonSaveStorage` | Leer y escribir disco con escritura transaccional (`.tmp` y luego mover) |
| Dominio | `ProgressService`, `SaveMigrations`, `SaveData` | Mutar el progreso validando invariantes; migrar versiones en cadena |
| Adapter | `SaveSystem` | Ciclo de vida de Unity: `persistentDataPath`, pausa del juego y de la aplicación, eventos del bus |

El save lleva `saveVersion` desde el primer día y se migra, nunca se borra: antes de migrar se hace
backup (R14). `OnApplicationPause(true)` es el disparador de guardado de plataforma, porque
`OnApplicationQuit` no es fiable en móvil (C6); la pausa del juego es el punto de guardado dentro de la
partida.

### Player

| Tipo | Clase | Responsabilidad |
|---|---|---|
| Adapter de input | `PlayerInputReader` | Lee `Player/Move` y `Player/Pause` por `InputActionReference` (M9). Solo deja pasar el movimiento en estado `Play`, que conoce por `OnGameStateChanged`; la pausa la publica como petición |
| Dominio | `PlayerMovement` | Calcula la velocidad horizontal con `Vector3.SmoothDamp` (M2). Recorta el input a longitud 1 y rechaza valores negativos o NaN |
| Adapter físico | `PlayerMover` | En `FixedUpdate` aplica esa velocidad al `Rigidbody` y conserva la vertical para la gravedad |

Los adapters validan sus referencias y valores en `OnValidate` y otra vez en `Awake` (R8); si algo
falta, registran el error y se deshabilitan (R9). Ojo con un detalle de Unity: `enabled = false`
dentro de `Awake` llama a `OnDisable` en el acto, antes de cualquier `OnEnable`, así que cada
`OnDisable` solo deshace lo que su `OnEnable` hizo de verdad. En `Scene_Game` el `Player` es una
cápsula con el tag `Player`, rotación congelada e interpolación, sobre un plano.

### Pickups

| Tipo | Clase | Responsabilidad |
|---|---|---|
| Adapter | `PickupSpawner` | Mantiene un pickup en cada punto: los saca del pool al llegar `OnBootstrapComplete`, los devuelve al recogerlos y publica `OnPickupCollected`. Al desactivarse devuelve los suyos, porque viven bajo el contenedor `DontDestroyOnLoad` del pool |
| Adapter | `Pickup` | Trigger con valor (mínimo 1). Detecta al jugador por el tag del `Rigidbody` y avisa a su spawner, que se le asigna en cada salida del pool (`IPoolable`) |
| Dominio | `RespawnQueue` | Qué puntos esperan reaparición y cuánto falta. Un `Tick` sin tiempo transcurrido no libera nada, que es lo que la detiene en pausa |

Entrar en Play directamente desde `Scene_Game` no hace aparecer pickups: sin bootstrap no hay pool.

### Debug

`DebugHud` usa **UI Toolkit** (la guía pide Canvas con TextMeshPro; la decisión está en `QWEN.md`).
Un `UIDocument` con `PanelSettings_DebugHud` y un `Label` creado por código muestra si terminó el
bootstrap, el estado actual y la moneda (desde el primer `OnProgressChanged`). La lógica del texto
vive en `DebugHudModel`, en C# puro. Como el `Label` lo crea el script, en un build sin la assembly
`Debug` el `UIDocument` queda vacío.

En un build de release, además, el log del player avisa de que el componente `DebugHud` no tiene
script. Es lo esperado: la assembly `Debug` no entra en ese build (comprobado con builds reales el
2026-09-14). `OnBootstrapComplete` no avisa de falta de suscriptores porque también lo escucha
`PickupSpawner`; antes del ciclo de recolección ese warning sí salía en release.

## Tests

Los tests forman parte de la plantilla: cubren los bugs reales de la auditoría y demuestran que los
sistemas están conectados.

- **EditMode** (lógica pura, milisegundos): `EventBus`, state machine, `GameManager` (pausa incluida),
  pool, save (almacenamiento, migraciones, progreso), regla de la escena de entrada, texto del HUD,
  movimiento del jugador (incluido que se sienta igual a 30 y a 120 fps) y `RespawnQueue`.
- **PlayMode** (escenas reales): el arranque completo desde `Scene_Bootstrap` y que los managers
  sobrevivan a él, que el HUD reciba los eventos, el error al entrar desde `Scene_Game`, un teclado
  virtual que mueve al jugador solo en `Play` y lo detiene al soltar, Esc que pausa, congela al jugador
  y reanuda, y el ciclo de recolección completo, con el save redirigido a un archivo temporal. También
  que `PlayerMover` y `PlayerInputReader` se deshabiliten con un error claro si les falta
  configuración.

El número de tests y su duración están en el `README.md`.

## Resumen de las 14 reglas

| # | Regla |
|---|---|
| R1 | Assembly Definitions antes del primer `.cs` |
| R2 | Namespace en todos los tipos |
| R3 | Grafo de assemblies acíclico; módulos de gameplay nunca se referencian entre sí |
| R4 | Los módulos de gameplay se comunican solo por `EventBus` |
| R5 | Lógica de dominio en C# puro; el `MonoBehaviour` es un adapter fino |
| R6 | Nada de `FindAnyObjectByType` para cablear; quien crea, inyecta |
| R7 | Una sola fuente de verdad por concepto |
| R8 | Validar campos serializados en `OnValidate` y en `Awake` |
| R9 | Nunca degradar en silencio: deshabilitar, lanzar o usar un fallback válido |
| R10 | Suscripciones solo en `OnEnable`/`OnDisable` |
| R11 | Un solo dueño por responsabilidad |
| R12 | Cero stubs silenciosos; pendientes como `TODO(Fase-N)` |
| R13 | Ningún `Debug.Log` fuera de `Log.cs` |
| R14 | El save se migra, nunca se borra |
