# GosipSimulator

Demo en Unity 6 sobre consecuencias sociales. La idea es que cada acción del jugador pueda ser
presenciada, y que lo que un NPC vio cambie cómo te trata después, aunque no estuviera delante: robas
algo, lo ve el hijo del herrero, el rumor llega al padre, y el herrero te cobra más o se niega a
atenderte.

El repo es un fork de [`DecoupledTemplate`](https://github.com/ningunFernando/DecoupledTemplate), una
plantilla de arquitectura cuyo objetivo es un `EventBus` tipado más un bootstrap desacoplado que
arranca los sistemas en un orden verificable. De ahí se heredan el andamiaje y las 14 reglas. La
plantilla nace a su vez de la auditoría de un proyecto anterior (HamsterBall), y cada regla evita un
fallo que ocurrió allí.

**Estado (2026-09-17).** El fork está hecho, renombrado de punta a punta y verificado: compila sin
errores ni warnings y las dos suites pasan (70 EditMode, 12 PlayMode). Los sistemas de chisme **no
existen todavía**. Lo que hay hoy en el repo es la plantilla heredada: arranque, pausa, guardado,
jugador, ciclo de recolección y HUD de desarrollo. La sección [El sistema de chisme](#el-sistema-de-chisme-planeado)
describe lo que se va a construir, no lo que hay.

| Documento | Para qué sirve |
|---|---|
| [`Assets/Docs/ARCHITECTURE.md`](Assets/Docs/ARCHITECTURE.md) | Cómo está hecha: assemblies, flujo de arranque, eventos y módulos |
| [`Assets/Docs/GUIA_PLANTILLA_ARQUITECTURA.md`](Assets/Docs/GUIA_PLANTILLA_ARQUITECTURA.md) | La especificación: 14 reglas, orden de construcción y *Definition of Done*. Es la de la plantilla y se conserva tal cual |
| [`QWEN.md`](QWEN.md) | Decisiones de este repo, estado, pendientes y los fallos del entorno ya diagnosticados. También lo leen los agentes |

## Requisitos

- Unity `6000.6.0f1`.
- Paquetes ya declarados en `Packages/manifest.json`: URP 17.6.0, Input System 1.20.0 y Test
  Framework 1.8.0. El manifest no trae nada que el proyecto no use.
- `jp.shiranui-isuzu.unity-mcp` 4.3.3, también en el manifest, para manejar el Editor desde agentes o
  desde la terminal. Es solo de Editor y no llega a ningún build.

## Abrir y jugar

1. En Unity Hub, *Add project from disk* y elegir la raíz del repo: la raíz es el proyecto de Unity.
2. Abrir `Assets/_Game/Scenes/Scene_Bootstrap.unity` y pulsar Play. **Siempre desde
   `Scene_Bootstrap`**: entrar desde `Scene_Game` no inicializa nada, y la consola lo avisa con un error.

| Acción | Teclado | Gamepad |
|---|---|---|
| Mover | WASD o flechas | Stick izquierdo |
| Pausar y reanudar | Esc | Start |

Las esferas del escenario son pickups: al tocarlas suman moneda y reaparecen en su sitio a los 2
segundos de juego (el contador se detiene en pausa). Arriba a la izquierda, el HUD de desarrollo
muestra si terminó el bootstrap, el estado del juego y la moneda; solo existe en el Editor y en los
development builds.

La partida se guarda al pausar, si hay cambios, y al salir o mandar la aplicación a segundo plano. En
macOS el archivo queda en `~/Library/Application Support/DefaultCompany/GosipSimulator/save.json`.

La consola muestra la secuencia de arranque paso a paso (`Log.Trace`) en el Editor y en los development
builds. En un build de release esas trazas no existen: se eliminan al compilar, salvo que se añada
`GOSIPSIMULATOR_VERBOSE` a *Player Settings > Scripting Define Symbols* para diagnosticarlo.

*Run In Background* está activado, así que el juego sigue corriendo aunque Unity o el build pierdan el
foco.

## Estructura

```
Assets/
├── _Game/
│   ├── Runtime/
│   │   ├── Core/       GosipSimulator.Core     bootstrap, EventBus, estados y pausa, pool, Log
│   │   ├── Data/       GosipSimulator.Data     ScriptableObjects de configuración
│   │   ├── Pickups/    GosipSimulator.Pickups  recolectables sacados del pool
│   │   ├── Player/     GosipSimulator.Player   input y movimiento
│   │   └── Save/       GosipSimulator.Save     guardado en tres capas
│   ├── Debug/          GosipSimulator.Debug    HUD, solo Editor y development builds
│   ├── Tests/
│   │   ├── EditMode/   GosipSimulator.Tests.EditMode
│   │   └── PlayMode/   GosipSimulator.Tests.PlayMode
│   ├── Prefabs/        GameManager, ObjectPoolManager, SaveSystem, Pickup
│   └── Scenes/         Scene_Bootstrap (índice 0), Scene_Game (índice 1)
├── Docs/               ARCHITECTURE.md y la guía
└── InputSystem_Actions.inputactions
```

## Tests

Desde el Editor: `Window > General > Test Runner`, pestaña *EditMode* o *PlayMode*, y *Run All*.

Desde la terminal, con el Editor abierto:

```bash
/Users/ningunfernando/.local/bin/isuzu-unity-cli verify --project GosipSimulator --test --test-mode edit
```

```bash
/Users/ningunfernando/.local/bin/isuzu-unity-cli verify --project GosipSimulator --test --test-mode play
```

La ruta absoluta no es manía: `~/.local/bin` no está en el PATH de un shell no interactivo en esta
máquina. Ver [Unity MCP](#unity-mcp).

Medido el 2026-09-17 en este repo, con el Editor abierto y el CLI de Unity MCP:

| Suite | Tests | Errores esperados en consola |
|---|---|---|
| EditMode | 70 en verde | 6 |
| PlayMode | 12 en verde | 5 |

Los errores son intencionados: cada test que prueba un caso de error declara el mensaje con
`LogAssert.Expect` y falla si no aparece. `verify` los cuenta igualmente porque solo lee la consola, no
sabe cuáles espera cada test. Si el número no coincide, algo ha cambiado de verdad.

**Los tests no tocan tu partida guardada.** Los de PlayMode leen el save real al arrancar, pero el que
comprueba que pausar escribe el archivo lo redirige antes a una carpeta temporal.

### Dos trampas al correr tests desde la terminal

**`Unity -batchmode -runTests -testMode PlayMode` corre EditMode y lo reporta en verde.** Unity acepta
el argumento (aparece tal cual en el log) y aun así construye un filtro `testMode = EditMode`, ejecuta
las 70 de EditMode y escribe un XML con `result="Passed"`. El exit code es 0. Se detecta mirando la
cuenta de tests y los nombres de las fixtures, nunca el exit code. Las corridas de PlayMode se hacen
con el Editor abierto y el CLI, que sí las ejecuta.

**Una corrida de PlayMode con 0 tests no es verde.** El proyecto tiene activadas las *Enter Play Mode
Options* (sin recarga de dominio), y después de entrar en Play con `isuzu-unity-cli call
play_mode_play` las corridas de PlayMode no encuentran ningún test hasta la siguiente recarga de
dominio. Basta con recompilar o con forzar la recarga:

```bash
/Users/ningunfernando/.local/bin/isuzu-unity-cli call execute_code --project GosipSimulator \
  --json '{"code":"EditorUtility.RequestScriptReload(); return \"ok\";"}'
```

## Cómo se hizo el fork y el renombrado

Los tres placeholders de la guía valen aquí `GosipSimulator`:

| Placeholder | Significado | Valor en este repo |
|---|---|---|
| `{Project}` | Nombre del proyecto en PascalCase | `GosipSimulator` |
| `{ROOT_NS}` | Namespace raíz | `GosipSimulator` |
| `{ASM}` | Prefijo de las assemblies | `GosipSimulator` |

El repo arranca del `main` de la plantilla en `adad2b6`, que sigue siendo ancestro de este `main`, y
el renombrado completo es un único commit (`778fff6`) hecho **sin abrir el Editor**, como un pase de
texto sobre la copia recién clonada. La guía y la sección correspondiente de la plantilla piden
hacerlo con el Editor abierto y módulo a módulo; aquí no hizo falta, por una razón concreta:

- Las escenas y los prefabs enlazan cada componente por el **GUID** del script
  (`m_Script: {fileID: 11500000, guid: ..., type: 3}`). `m_EditorClassIdentifier` es solo una pista para
  que el Editor cargue antes.
- Los `.asmdef` se referencian entre sí **por nombre**, no por GUID.
- No se renombró **ningún archivo `.cs`**: solo el `namespace` de dentro. El `.meta` de cada script
  conserva su GUID porque el archivo no se movió.

Así que lo único que había que tocar era texto: `name`, `rootNamespace` y `references` en los 8
`.asmdef`, los `namespace` y `using` de los 45 `.cs`, los `m_EditorClassIdentifier` de las 2 escenas,
los 4 prefabs y `GameConfig_Default.asset`, `Log.VERBOSE`, el `menuName` del `[CreateAssetMenu]`,
`productName` / `metroPackageName` / `metroApplicationDescription` / `projectName`, el prefijo de las
carpetas temporales de dos tests, y el nombre de la solución en `.vscode/settings.json`. Los 8
`.asmdef` se renombraron con `git mv` junto a su `.meta`.

Lo que sigue valiendo de la guía: **mover o renombrar un archivo `.cs` sí se hace desde el Editor**,
porque eso es lo que deja un `.meta` huérfano, y Unity le asigna un GUID nuevo que rompe las escenas.

Cómo se verificó, y qué comprobar si se repite la operación:

1. Fotografiar los GUID de todos los `.meta` de `Assets/` antes y después. El conjunto tiene que ser
   idéntico. Aquí: 95 antes, 95 después, sin diferencias.
2. `git` debe reportar los 8 `.asmdef.meta` como renombres al 100 % (`R100`), es decir, contenido
   intacto. Es la prueba independiente de que los GUID no se tocaron.
3. Ningún `.meta` huérfano y ningún archivo sin `.meta`.
4. Cada `m_EditorClassIdentifier` del proyecto debe resolver a un tipo real de la assembly que dice.
5. Compilar y correr las dos suites.

`companyName` sigue en `DefaultCompany`. Cambiarlo mueve `Application.persistentDataPath`, y aquí no
importa porque el fork no trae ninguna partida guardada; si se cambia más adelante, el save existente
deja de encontrarse.

## Unity MCP

El paquete `jp.shiranui-isuzu.unity-mcp` hace que el Editor sirva MCP en
`http://127.0.0.1:<puerto>/mcp` y responda al CLI `isuzu-unity-cli` (compilar, correr tests, leer la
consola, entrar en Play, editar escenas). El puerto se deriva de la ruta del proyecto, así que este
repo y la plantilla no chocan aunque estén abiertos a la vez.

**En esta máquina el CLI está en `/Users/ningunfernando/.local/bin/isuzu-unity-cli` y ese directorio no
está en el PATH** de un shell no interactivo (`/usr/bin:/bin:/usr/sbin:/sbin`, y `~/.zshrc` no se
carga). Hay que llamarlo por ruta absoluta.

Registro por agente:

- **Qwen Code.** `isuzu-unity-cli setup` no lo conoce (soporta claude-code, claude-desktop, codex,
  cursor, gemini y vscode), así que la entrada va a mano en `.qwen/settings.json`, con la ruta absoluta
  en `command`, que es lo que la documentación de Qwen recomienda para servidores stdio:

  ```json
  "mcpServers": {
    "unity": {
      "command": "/Users/ningunfernando/.local/bin/isuzu-unity-cli",
      "args": ["mcp-stdio", "--project", "GosipSimulator"],
      "timeout": 60000
    }
  }
  ```

  Hace falta reiniciar Qwen Code para que cargue. La skill vive en
  `~/.qwen/skills/isuzu-unity-cli/`, copiada de la de Claude.
- **Claude Code.** `isuzu-unity-cli setup --agent claude-code --mcp`.

El CLI y el paquete de Unity salen juntos de la misma versión: aquí los dos en 4.3.3. Subir uno sin el
otro rompe el emparejamiento, así que `isuzu-unity-cli upgrade` implica cambiar también el tag
`#v4.3.3` del manifest.

## El sistema de chisme (planeado)

No implementado. Es el plan acordado para la primera rebanada, y sirve para que las 14 reglas se
pueden comprobar contra algo concreto.

Cuatro assemblies nuevas, todas hoja sobre `Core` y `Data`, y sin referenciarse entre ellas (R3):

| Assembly | De qué es dueña |
|---|---|
| `GosipSimulator.Gossip` | El grafo de opiniones y la propagación de rumores. Dominio en C# puro (R5) |
| `GosipSimulator.Npcs` | Identidad de los NPCs y percepción: quién presenció qué |
| `GosipSimulator.Actions` | Los verbos del jugador. Valida y publica, no interpreta |
| `GosipSimulator.Shop` | Las condiciones del herrero: multiplicador de precio y negativa |

El recorrido del caso del herrero: el jugador roba y `Actions` publica `OnActionCommitted`; `Npcs`
resuelve quién estaba al alcance y publica un `OnActionWitnessed` por testigo; `Gossip` aplica el delta
de opinión y hace viajar el rumor por el grafo social, decayendo por salto y ponderado por confianza;
cada cambio publica `OnRelationshipChanged`; `Shop` guarda su propio multiplicador y la próxima compra
sale más cara o se rechaza.

La decisión de diseño que sostiene todo: **`Shop` no puede consultar a `Gossip`** (R3), así que el
estado de relaciones se empuja, no se tira. Es el mismo patrón que ya usa `DebugHud` con
`OnProgressChanged`. La consecuencia es el orden de carga, y se resuelve igual que en la plantilla:
`Save` publica `OnRelationshipsRestored` al llegar `OnBootstrapComplete`, y `Gossip` construye su grafo
desde la configuración en `Awake`, así que ninguno de los dos depende del orden en que se atiendan los
handlers.

La persistencia encaja en lo que ya hay: `SaveData` pasa a versión 2 con las opiniones, y
`SaveMigrations` gana el salto de v1 a v2 con su backup (R14).

`Pickups` se queda mientras tanto. Es hoy el único consumidor del pool y lo único que hace que
`SaveSystem` escriba el archivo, y el README de la plantilla avisa de no borrarlo sin reemplazo. Se
revisará cuando `Shop` y `Gossip` sostengan el ciclo.
