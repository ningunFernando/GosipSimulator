# QWEN.md - GosipSimulator

Demo en Unity 6 sobre consecuencias sociales: las acciones del jugador las pueden presenciar los NPCs,
y lo que vieron cambia cómo le tratan después, aunque no estuvieran delante. El caso que abre el
proyecto es el del herrero: robas algo, lo ve su hijo, el rumor llega al padre, y el herrero te cobra
más o se niega a atenderte.

Este archivo registra lo que la guía no puede saber: las decisiones tomadas sobre este repo concreto,
su estado real, los pendientes, y los fallos del entorno que ya están diagnosticados. La fuente
autoritativa de las 14 reglas sigue siendo
`Assets/Docs/GUIA_PLANTILLA_ARQUITECTURA.md`. **Lee la guía entera antes de escribir código.**

Cómo está hecho el proyecto hoy: `Assets/Docs/ARCHITECTURE.md`. Cómo abrirlo y correr los tests:
`README.md`.

## Origen

Fork de [`DecoupledTemplate`](https://github.com/ningunFernando/DecoupledTemplate), que es una
plantilla de arquitectura y no un juego. El punto de partida es su `main` en `adad2b6`, que sigue
siendo ancestro de este `main`, así que la historia de la plantilla está entera en este repo.

**No hay ningún remote a la plantilla, y es deliberado.** Al configurar el fork se añadió uno llamado
`upstream` con la URL de push sustituida por un centinela, para poder traer arreglos futuros de la
plantilla sin riesgo de escribir en ella. Se quitó el 2026-09-17, decisión de Fernando, porque dejaba
el repo en un estado que se presta a un error caro: sin `origin` configurado, `git push` no tiene
destino y git sugiere `git push --set-upstream upstream main`, o sea, empujar los commits de este
juego a la plantilla. Lo único que lo impedía era ese centinela, y tenía forma de configuración a
medio rellenar, así que se leía como un olvido y no como un bloqueo. Si algún día hace falta traer
algo de la plantilla: añadir el remote, hacer fetch, y volver a quitarlo.

De la plantilla se hereda todo el andamiaje: `EventBus` tipado, bootstrap ordenado, estados y pausa,
pool de objetos, guardado en tres capas con migraciones, jugador 3D con `Rigidbody`, el ciclo de
recolección y el HUD de desarrollo. También las 14 reglas y el motivo de cada una, que viene de la
auditoría de HamsterBall.

**Lo específico del juego no existe todavía.** Ni una línea de chisme, ni relaciones, ni NPCs, ni
tienda. Quien lea este repo esperando encontrar el simulador va a encontrar la plantilla renombrada.

## Placeholders

`{Project}` = `{ROOT_NS}` = `{ASM}` = **`GosipSimulator`**. Las 8 assemblies son
`GosipSimulator.{Core,Data,Player,Save,Pickups,Debug,Tests.EditMode,Tests.PlayMode}` y el
`rootNamespace` de cada una coincide con su nombre, salvo las dos de tests, que comparten
`GosipSimulator.Tests`.

## Estado

**Hecho y verificado (2026-09-17).** El fork y el renombrado completo, en el commit `778fff6`. Las dos
suites en verde con el Editor abierto y el CLI de Unity MCP:

| Suite | Tests | Errores en consola | Compilado |
|---|---|---|---|
| EditMode | 70 en verde | 6 | 0 errores, 7.3 s |
| PlayMode | 12 en verde | 5 | 0 errores, 3.1 s |

Los errores de la consola son los esperados: cada test de un caso de error declara su mensaje con
`LogAssert.Expect`. Cero warnings. Cero `error CS` también en la compilación por lote previa.

**Por hacer.** Todo el gameplay. El diseño acordado está en la última sección de `ARCHITECTURE.md` y en
el `README.md`, en ambos sitios marcado como planeado.

## Decisiones tomadas

**El renombrado se hizo sin abrir el Editor**, como un pase de texto sobre la copia recién clonada, en
un solo commit. La guía de la plantilla y su README piden hacerlo con el Editor abierto y módulo a
módulo; aquí no hizo falta, y la razón importa porque cambia cómo se aborda la próxima vez:

- Las escenas y los prefabs enlazan componentes por el **GUID** del script. `m_EditorClassIdentifier`
  es solo una pista de carga rápida para el Editor.
- Los `.asmdef` se referencian entre sí **por nombre**, no por GUID.
- No se renombró **ningún archivo `.cs`**, solo el `namespace` de dentro. Sin archivo movido, el
  `.meta` conserva su GUID.

Lo que sí sigue valiendo de la guía: mover o renombrar un `.cs` se hace desde el Editor, porque eso es
lo que deja un `.meta` huérfano y le hace a Unity asignar un GUID nuevo.

La verificación no fue visual sino numérica, y es la que hay que repetir si se vuelve a hacer algo
parecido: el conjunto de GUID bajo `Assets/` idéntico antes y después (95 y 95), los 8 `.asmdef.meta`
reportados por git como `R100` (contenido intacto), ningún `.meta` huérfano ni archivo sin `.meta`,
cada `m_EditorClassIdentifier` del proyecto resolviendo a un tipo real, y las dos suites en verde.

**`companyName` se queda en `DefaultCompany`.** Cambiarlo mueve `Application.persistentDataPath`. En el
fork no importaba porque no trae ninguna partida guardada, y se dejó para no mezclar dos cambios; si se
cambia más adelante, el save que exista deja de encontrarse.

**`Pickups` se queda.** Es hoy el único consumidor del pool y lo único que hace que `SaveSystem`
escriba el archivo. El README de la plantilla avisa de no borrarlo sin reemplazo, porque sin consumidor
el pool y el guardado vuelven a ser código que nunca se ejecuta, que es justo el defecto central de
HamsterBall. Se revisará cuando `Shop` y `Gossip` sostengan el ciclo.

**El estado de relaciones se empuja, no se tira.** `Shop` no puede referenciar `Gossip` (R3), así que
cada consumidor cachea lo suyo a partir de `OnRelationshipChanged`. Se descartó mover el almacén a
`Core`: convertiría un concepto de gameplay en dependencia de todos. El detalle y la resolución del
orden de carga están en `ARCHITECTURE.md`.

**Ni push ni PR desde el agente.** El trabajo se deja commiteado y verificado en local, y Fernando hace
el push y abre el PR. Preguntar antes de empujar, de abrir un PR o de borrar ramas o worktrees.

## Fallos del entorno ya diagnosticados

No volver a diagnosticarlos desde cero. Todos comprobados el 2026-09-17 en esta máquina.

**`Unity -batchmode -runTests -testMode PlayMode` corre EditMode y lo reporta en verde.** Unity acepta
el argumento (queda tal cual en el log) y aun así construye un filtro `testMode = EditMode`, ejecuta
las 70 de EditMode y escribe un XML con `result="Passed"` y exit code 0. Quitar `-nographics` no lo
arregla. Un verde de PlayMode por línea de comandos no vale nada: hay que mirar la cuenta de tests y
los nombres de las fixtures. Las corridas de PlayMode se hacen con el Editor abierto y el CLI, que sí
las ejecuta de verdad.

**`-nographics` vacía los runtime settings de URP.** El primer import por lote reescribió
`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` y dejó `m_RuntimeSettings.m_List` en
`[]`, perdiendo 17 entradas. Es pérdida de datos, no reserialización. Se descartó el cambio y se
comprobó que el archivo volvía a sus 93 `rid:`. La corrida sin `-nographics` no lo toca. Regla:
después de cualquier `-batchmode -nographics`, mirar `git status` y descartar lo que haya cambiado en
`Assets/Settings/`.

**`~/.local/bin` no está en el PATH del shell que abre Qwen Code.** El PATH es
`/usr/bin:/bin:/usr/sbin:/sbin` y `~/.zshrc` no se carga, porque el shell es `bash -c` no interactivo.
`isuzu-unity-cli` está instalado en `/Users/ningunfernando/.local/bin/isuzu-unity-cli` y hay que
llamarlo por ruta absoluta. No se tocó `~/.zshrc`, que ya dio problemas antes con rutas viejas del SDK
de Flutter.

**El CLI y el paquete de Unity salen de la misma versión.** Aquí los dos en 4.3.3, y el manifest fija
el paquete por tag `#v4.3.3`. `isuzu-unity-cli upgrade` avisa de que hay 4.4.2: subir el CLI sin subir
el tag del manifest rompe el emparejamiento. No se subió.

**`isuzu-unity-cli setup` no conoce Qwen Code.** Soporta claude-code, claude-desktop, codex, cursor,
gemini y vscode. La entrada de MCP para Qwen va a mano en `.qwen/settings.json` y hace falta reiniciar
Qwen Code para que cargue. La skill se copió de `~/.claude/skills/isuzu-unity-cli/` a
`~/.qwen/skills/isuzu-unity-cli/`, con una nota local arriba sobre la ruta absoluta; un `setup` o un
`update` la sobreescribe.

**El guard del shell niega cualquier comando git contra una ruta fuera del directorio de trabajo.** Ni
`git -C <ruta> log`, ni `git remote -v`, ni `git --git-dir=`. Solo pasó `git ls-remote`. Consecuencia:
no se pudo comprobar si la plantilla tenía cambios sin commitear, y un clon solo captura lo
commiteado. Sí pasó `git clone <ruta de fuera> <destino dentro>`.

**Una corrida de PlayMode con 0 tests no es verde.** Las *Enter Play Mode Options* están activadas (sin
recarga de dominio), y después de entrar en Play con `play_mode_play` las corridas de PlayMode no
encuentran ningún test hasta la siguiente recarga. Forzarla:

```bash
/Users/ningunfernando/.local/bin/isuzu-unity-cli call execute_code --project GosipSimulator \
  --json '{"code":"EditorUtility.RequestScriptReload(); return \"ok\";"}'
```

**Un `.cs` nuevo escrito desde fuera del Editor puede quedarse sin compilar y sin ningún aviso.** Le
pasó a la plantilla con `BootstrapperTests.cs`, creado mientras Unity recargaba el dominio: se importó
como `MonoScript` de la assembly correcta pero Unity no lo metió en su lista de fuentes, y la suite
siguió en verde con los tests viejos. Síntoma: la cuenta de tests no sube. **Después de añadir tests,
comprobar siempre la cuenta.** Arreglo que funcionó: renombrar el archivo y devolverle el nombre desde
el Editor, con `AssetDatabase.MoveAsset` ida y vuelta.

**Con el Editor abierto es más seguro, no menos.** Tenerlo corriendo permite forzar el refresh y
comprobar la cuenta de tests por MCP después de cada tanda de archivos nuevos, que es justo lo que el
punto anterior pide.

**En Safe Mode el servidor MCP no existe, aunque el Editor esté abierto.** Safe Mode no compila los
scripts, así que el paquete `unity-mcp` nunca arranca su servidor ni escribe su descriptor. Síntoma:
`isuzu-unity-cli projects` responde *No running Unity Editor found* con el Editor abierto en pantalla.
No falta el Editor: hay errores de compilación. Comprobado el 2026-09-19 contra un proceso de Unity vivo
desde hacía cuatro horas. La consecuencia práctica importa, porque un error de compilación deja al
agente ciego justo cuando más falta hace el MCP: la salida de `projects` no distingue "Editor cerrado"
de "Editor en Safe Mode", y afirmar lo primero sin más prueba es un diagnóstico falso.

**`health` reporta el uptime del servidor MCP, no el del Editor.** Se reinicia con cada recarga de
dominio, y `verify --test` recompila, así que correr los tests lo pone a cero. Se lee como un reinicio
del Editor y no lo es. Para el uptime real: `ps -o pid,etime -p <pid>` con el pid que da `projects`.

**Abrir un segundo Editor sobre un proyecto ya abierto termina al momento y sin avisar.** Sale con
código 0 porque la instancia existente tiene el bloqueo del proyecto, y el CLI pasa a hablar con la que
ya estaba corriendo. Se lee fácil como "mi Editor se cerró solo", cuando lo que salió fue el segundo y
las verificaciones siguen valiendo porque corren en la instancia viva.

**`asset_broken_references` solo mira la escena abierta.** Reporta `scope: "scene"` y
`objectsScanned` con lo que haya cargado, así que con `Scene_Bootstrap` abierta escanea un objeto y no
dice nada de `Scene_Game`. Para cubrir las dos hay que abrirlas, o fiarse de la suite de PlayMode, que
las carga de verdad y comprueba que los managers sobreviven y que el HUD recibe los eventos.

## Conocimiento heredado que sigue valiendo

Todo esto se descubrió construyendo la plantilla y está pagado. No repetirlo.

- **Los managers los marca `DontDestroyOnLoad` el `Bootstrapper`, no ellos solos.** Cargar `Scene_Game`
  destruye todo lo que quede en `Scene_Bootstrap`. Cuando solo `GameManager` se marcaba a sí mismo, el
  pool y el guardado morían en ese momento y las referencias seguían pareciendo asignadas.
- **`SceneManager.sceneLoaded` no se desuscribe en el `OnDestroy` del `Bootstrapper`.** Cargar la escena
  de juego destruye el `Bootstrapper` antes de que Unity dispare el evento, y un `OnDestroy` que
  desuscribe deja el juego sin arrancar y sin ningún error en la consola. El handler se desuscribe a sí
  mismo como primera línea.
- **`enabled = false` dentro de `Awake` llama a `OnDisable` en el acto**, antes de cualquier `OnEnable`.
  Cada `OnDisable` solo puede deshacer lo que su `OnEnable` hizo de verdad. Bug real que producía una
  `NullReferenceException` justo después del error claro.
- **Con objetos de Unity, `Assert.IsTrue(obj != null)`, nunca `Assert.IsNotNull(obj)`.** El segundo no
  ve un `UnityEngine.Object` destruido, y por eso el test de arranque no detectó el bug de los managers.
- **`File.Move(origen, destino, overwrite)` no existe en el nivel de API del proyecto** (CS1501). La
  escritura transaccional de `JsonSaveStorage` borra el destino antes de mover.
- **`Data` no puede usar tipos de `Core`.** `GameState` vive en `Core.State` y `PoolConfig` en
  `Core.Pool`, así que ningún ScriptableObject puede tener un campo de esos. Cualquier enum que
  necesite `Data` se declara dentro de `Data`. Esto va a importar en cuanto la configuración de NPCs y
  de chisme quiera enums compartidos.
- **`namespace GosipSimulator.Debug` sombrea `UnityEngine.Debug`.** Dentro de ese namespace, un
  `Debug.Log(...)` sin calificar da `CS0118`. R13 lo hace improbable, pero los dos asmdef de tests
  también referencian `Debug`.
- **Referencias a paquetes en los asmdef.** El `autoReferenced` de un paquete solo afecta a
  `Assembly-CSharp`, no a nuestras assemblies: cualquier tipo de un paquete nuevo necesita su
  referencia explícita, como `Unity.InputSystem` en `Player`. Si falta, el error es un `CS0246`
  despistado.
- **`OnBootstrapComplete` se publica al cargar la escena, no al final de la secuencia**, porque los
  objetos de `Scene_Game` se suscriben en su `OnEnable` durante esa carga (R10).
- **`EventBus` avisa cuando se publica sin suscriptores.** Es deliberado: un bus decorativo es
  indistinguible de uno roto. Si un evento nuevo no tiene suscriptor todavía, ese warning va a salir y
  no hay que silenciarlo, hay que no publicar el evento hasta que exista.

## Convenciones de escritura

Obligatorias para cualquier agente, en código, comentarios, mensajes de commit y documentación.

- **Cero emojis.** Ni en comentarios, ni en código, ni en strings de log, ni en documentación.
- **Cero em-dash (`U+2014`).** En su lugar: coma, punto, dos puntos, guion normal o paréntesis. Los
  separadores de sección con caracteres de caja (`─`, U+2500) no son em-dash y se mantienen.
- **Código y comentarios en inglés.** Nombres de tipos, métodos y campos, strings de log, XML doc y
  comentarios.
- **Documentación en español.** `QWEN.md`, `README.md` y `Assets/Docs/*.md`.
- Los comentarios explican el **por qué**, no el qué, y citan la regla o el fallo que evitan
  (`R4`, `M6`, `C6`, `A1`). Es el estilo de todo el código heredado.

La guía existe dos veces: `Assets/Docs/` aquí y `HamsterBall/Docs/`. La autoritativa para este repo es
la de aquí, pero no se edita: es la especificación de la plantilla y se conserva tal cual, con sus
placeholders. Si diverge de la de HamsterBall, da igual, ninguna se va a volver a sincronizar.

## Pendientes

1. **Falta el remote `origin`, y ahora mismo no queda ningún remote.** Fernando crea el repo
   `GosipSimulator` en GitHub y pasa la URL; entonces `git remote add origin <url>` y
   `git push -u origin main`. `gh` no está instalado en esta máquina, así que el repo no se puede
   crear desde el agente, y el push lo hace Fernando.
2. **Resuelto el 2026-09-17: `.qwen/settings.json` está commiteado y acotado.** Fernando commiteó en
   `9d8313a` los permisos que la sesión había ido acumulando, y el commit siguiente los acotó. El
   resultado, comparado con lo que traía la plantilla:
   - **Fuera `Read(//Users/ningunfernando/.qwen/**)`**, que daba acceso al `settings.json` global de
     Qwen y por tanto a una clave de API en claro. La clave nunca estuvo en el repo, pero el permiso
     sí viajaba con él, y lo tendría cualquiera que clonara el proyecto.
   - **Fuera `Bash(* *)`**, que auto-aprobaba cualquier comando de dos palabras o más. Fuera también
     `Bash(esac)`, `Bash(do *)`, `Bash(done)` y `Bash(command *)`: artefactos del parser de permisos
     sobre palabras clave del shell, no intenciones reales. Tres de los cuatro venían de la plantilla.
   - **Se conservan** los `Read(...)` de la plantilla, del Editor y del CLI, más `ls`, `python3` y
     `curl`, y se añade el CLI de Unity MCP por ruta absoluta, que es la vía de verificación del
     proyecto.
   - **No se añadió `Bash(git *)` a propósito**: con ese comodín quedarían auto-aprobados `git push` y
     `git reset --hard`, y la convención es que el push lo hace Fernando.

   Si el repo deja de ser privado o entra un colaborador, revisar otra vez los `Read(...)`, porque
   viajan con el repo y son rutas de esta máquina.
3. **No se pudo comprobar si la plantilla tenía cambios sin commitear** en el momento del fork, por el
   guard del shell. `git -C ../Unity/DecoupledTemplate status` desde fuera de esta sesión lo resuelve.
   Si había algo, este fork no lo tiene.
4. **Construir el sistema de chisme.** Cuatro assemblies nuevas (`Gossip`, `Npcs`, `Actions`, `Shop`),
   los eventos en `Core`, los ScriptableObjects en `Data`, `SaveData` v2 con su migración, y los tests.
   El diseño está en `ARCHITECTURE.md`.
5. **Control táctil sin decidir.** Si el juego acaba siendo para móvil en vertical, ojo: el
   `OnScreenStick` del Input System funciona sobre UGUI, no sobre UI Toolkit, y el HUD es UI Toolkit.

## Notas para el trabajo siguiente

- Los archivos nuevos se escriben con el Editor abierto y se comprueba la cuenta de tests después, por
  el fallo del `.cs` que no se compila.
- Cada módulo de gameplay nuevo es una hoja sobre `Core` y `Data`. Si dos de ellos se necesitan, la
  respuesta es un evento, no una referencia.
- Todo lo que se escribe tiene un call site y un test que lo ejecuta. Si un sistema todavía no va a
  tener consumidor, no se escribe.
- Los eventos nuevos van en `Assets/_Game/Runtime/Core/Events/GameEvents.cs`, como structs, con su
  región y su XML doc diciendo quién los publica y quién los escucha.
