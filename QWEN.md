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

**Lo específico del juego ya se ejecuta, pero todavía nadie lo dispara.** Existe la capa de dominio del
chisme, la configuración de la aldea en `Data`, y desde el hito 6 el `GossipManager` que las monta en
`Scene_Game`. Lo que falta es quien publique `OnActionWitnessed`: hasta que exista `Npcs`, la aldea
nunca se entera de nada. El detalle y lo que falta están en [Estado](#estado).

## Placeholders

`{Project}` = `{ROOT_NS}` = `{ASM}` = **`GosipSimulator`**. Hoy hay **9** assemblies, no las 8 de la
plantilla: `GosipSimulator.{Core,Data,Player,Save,Pickups,Gossip,Debug,Tests.EditMode,Tests.PlayMode}`.
El `rootNamespace` de cada una coincide con su nombre, salvo las dos de tests, que comparten
`GosipSimulator.Tests`. Las tres de gameplay que faltan (`Npcs`, `Actions`, `Shop`) entran como hojas
iguales que `Gossip`, referenciando solo `Core` y `Data`, y habrá que actualizar esta cuenta.

Ojo con un detalle: la carpeta se llama `Runtime/Gosip/` y la assembly `GosipSimulator.Gossip`. Dos
grafías de la misma palabra en la misma ruta. No rompe nada, pero conviene elegir una.

## Estado

**Hecho y verificado.** El fork y el renombrado completo (`778fff6`), la documentación reescrita para
este proyecto (`bc03fb4`), la capa de dominio del chisme (`99acc35` y `fc30717`), el hito 6 (`59d5399`):
el adapter `GossipManager` y su cableado en `Scene_Game`, y el hito 7: `SaveData` v2 y la persistencia de
las opiniones. Medido el 2026-09-20 con el Editor abierto y el CLI de Unity MCP:

| Suite | Tests | Errores en consola | Warnings |
|---|---|---|---|
| EditMode | 167 en verde | 6 | 7 |
| PlayMode | 22 en verde | 6 | 2 |

Los 6 y los 6 errores son los esperados: cada test de un caso de error declara su mensaje con
`LogAssert.Expect`. El que subió el número de PlayMode de 5 a 6 es
`AnActionWithNoDefinition_IsRejectedWithAnErrorAndMovesNobody`. Los 7 warnings de EditMode vienen de
`ObjectPoolManagerTests` y `EventBusTests`, que ejercitan a propósito las ramas de aviso (`TestPool`,
`TestEvent`). Cero warnings del compilador.

**Los 2 warnings de PlayMode son nuevos del hito 7 y no son un fallo.** Los dos dicen
`[ObjectPoolManager] Pool 'Pickup' empty. Expanding.`: los cuatro tests de persistencia arrancan el juego
entero, cada arranque pide cuatro pickups, el pool tiene cuatro, y con más arranques seguidos le toca
expandirse. Se comprobó leyendo la consola, no adivinando, y `PickupFlowTests` sigue en verde. Antes de
tratarlo como un fallo, mirar si el número cambia al añadir o quitar tests que arranquen el juego.

De los 167 de EditMode, **70 son heredados de la plantilla y 97 son del chisme y su persistencia**: 29 de
`RelationshipGraph`, 22 de `RumorPropagator`, 24 de `GossipService`, 20 de `RelationshipStore` y 2 nuevos
en `SaveMigrationsTests`. De los 22 de PlayMode, **12 son heredados y 10 nuevos**: 6 en `GossipFlowTests`
y 4 en `RelationshipPersistenceTests`.

**Del chisme existe la capa de dominio y su adapter.** En `Runtime/Gosip/`, cinco tipos con la forma de
cuatro capas que ya usan `Save` y `Pickups`: `RelationshipGraph` y `SocialGraph` son datos puros,
`RumorPropagator` planea el recorrido como datos, `GossipService` muta y publica, y `GossipManager` es
el `MonoBehaviour` fino que construye el servicio desde los assets, se suscribe al bus y llama a `Tick`.
En `Data`, cuatro tipos de configuración (`SocialTie`, `NpcDefinitionSO`, `GossipConfigSO`,
`ActionDefinitionSO`) y siete assets: cuatro NPCs en `SO/NPCS/`, más `Robbery`, `Action_Help` y
`NewGossipConfig`. La aldea es una cadena conectada: `son → blacksmith@90 → villager@50 → elder@40`.

**El call site ya existe, y eso era el hito 6.** `GossipManager` vive en el objeto `SocialGraph` de
`Scene_Game` con los siete assets asignados, y `GossipFlowTests` comprueba en una escena real que un
`OnActionWitnessed` mueve al testigo, que el rumor llega al herrero y al aldeano y muere antes del
anciano, que la pausa lo detiene a medio camino, y que `Restore` no publica. La regla del repo ("todo lo
que se escribe tiene un call site y un test que lo ejecuta") vuelve a cumplirse.

**El hito 7 cerró la persistencia.** `SaveData` va por `CURRENT_VERSION = 2` con una lista de
`RelationshipRow`, y `SaveMigrations` tiene por fin una entrada real, `[1]`, que añade la lista vacía y
conserva moneda y acumulado. `RelationshipStore` es el dominio de lo que se escribe, con la misma
dispersión que `RelationshipGraph`: una opinión de vuelta a cero borra su fila en vez de dejar un cero en
el archivo. `SaveSystem` escucha `OnRelationshipChanged` y publica `OnRelationshipsRestored` al llegar
`OnBootstrapComplete`, así que `Gossip` y `Save` siguen sin conocerse (R3, R4).

**Lo que todavía no ocurre es que alguien publique `OnActionWitnessed`.** El servicio escucha, su `Tick`
corre y lo que mueve se guarda, pero sin `Npcs` nadie presencia nada, así que en una partida la aldea no
se entera. Eso es el hito 8, no un defecto del 6 ni del 7.

**Lo que falta, en orden.** Cada hito es verificable por sí solo.

| # | Hito | Qué desbloquea |
|---|---|---|
| 8 | Assembly `Npcs`: `Npc`, `PerceptionResolver`, `NpcRegistry` | Que alguien presencie algo y publique `OnActionWitnessed` |
| 9 | Assembly `Actions`: `Interactable`, `ActionCatalog`, `InteractionReader`, y la acción `Interact` en `InputSystem_Actions` | Que el jugador pueda robar |
| 10 | Assembly `Shop`: `PricingPolicy`, `Shopkeeper`, y los cinco eventos que faltan | Que el herrero cobre más o se niegue |
| 11 | `DebugHudModel` y `DebugHud` mostrando la opinión | Verlo funcionar sin depurador |

Los hitos 6 y 7 están hechos, y con ellos el ciclo mínimo demostrable por tests: el dominio corre en una
partida y lo que cambia sobrevive a cerrarla. Del 8 al 11 es lo que lo hace jugable.

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

**Una corrida de PlayMode con 0 tests no es verde.** Las *Enter Play Mode Options* están activadas con
`DisableDomainReload` y `DisableSceneReload`, y con eso el descubrimiento de tests de PlayMode devuelve
una lista vacía. La respuesta es `status: completed`, `passed: 0`, `durationSeconds` del orden de 1e-06.

**Corregido el 2026-09-20: forzar la recarga de dominio no basta.** Lo que este archivo decía era
`EditorUtility.RequestScriptReload()`, y ya no funciona, si es que alguna vez fue eso lo que lo
arreglaba. Se probó tres veces seguidas y las tres dieron 0 tests, incluida una corrida que tardó 7
segundos de reloj, o sea que el runner sí entró en Play y aun así no encontró nada. Lo que funciona es
apagar la opción, correr, y volver a encenderla:

```bash
/Users/ningunfernando/.local/bin/isuzu-unity-cli call execute_code --project GosipSimulator \
  --json '{"code":"EditorSettings.enterPlayModeOptionsEnabled = false; return \"off\";"}'
```

```bash
/Users/ningunfernando/.local/bin/isuzu-unity-cli call execute_code --project GosipSimulator \
  --json '{"code":"EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload; return \"on\";"}'
```

Con la opción apagada, las 18 de PlayMode pasan en unos 6 segundos.

**Restaurar el valor es parte del procedimiento, y hay que comprobarlo en disco.** Apagar la opción
reescribe `ProjectSettings/EditorSettings.asset` con `m_EnterPlayModeOptions: 0` en el acto; volver a
encenderla en memoria **no** vuelve a escribir el archivo, así que el repo se queda con la opción
apagada aunque el Editor la muestre encendida. El valor bueno es `3`. Comprobar con `git diff
ProjectSettings/` y, si hace falta, `git checkout -- ProjectSettings/EditorSettings.asset`, que es
seguro porque el estado en memoria ya es el correcto.

El síntoma hermano en EditMode sigue existiendo: el 2026-09-19 una corrida reportó `passed: 1` en 4 ms,
y tras forzar la recarga, 145. **La cuenta de tests es la única señal fiable**, y el síntoma no es
siempre 0.

**`verify --test` puede morir con `server_stopped` y no es un fallo del Editor.** `verify` fuerza una
recompilación, la recompilación provoca una recarga de dominio, y la recarga tira el servidor MCP con la
petición en vuelo. La vía que sí funciona es lanzar y sondear por separado:

```bash
isuzu-unity-cli call test_run     --project GosipSimulator --json '{"mode":"edit"}'
isuzu-unity-cli call test_results --project GosipSimulator --json '{"limit":20}'
```

`test_results` no necesita el hilo principal, así que responde mientras la corrida ocupa el Editor, y
**no acepta `mode`**: sus únicos argumentos son `include_passed` y `limit`. Pasarle `mode` no da un error,
da una respuesta vacía, que se lee como "no hay resultados". El campo que dice el estado es `status`, con
valores `running` y `completed`.

**`execute_code` pasa a segundo plano si el hilo principal lleva unos segundos sin correr.** Devuelve un
`jobId` y un aviso de que no se reintente. La operación suele haber terminado igual: comprobar el
resultado en disco antes de volver a lanzarla, porque reintentar ejecuta el snippet dos veces y no es
idempotente. Para snippets largos, `--file <ruta>` en vez de `--json`, que es lo que la propia
documentación de la tool recomienda cuando el JSON se monta a mano.

**`execute_code` no ve nuestras assemblies.** El snippet se coloca dentro de un cuerpo de método, así que
no admite `using`, y solo trae `System`, `System.Collections`, `System.Collections.Generic`,
`System.Linq`, `System.Threading.Tasks`, `UnityEngine` y `UnityEditor`. `GosipSimulator.Data` no está, y
escribir el nombre completo tampoco compila. La salida es reflexión:
`Type.GetType("GosipSimulator.Data.NpcDefinitionSO, GosipSimulator.Data")` y luego
`ScriptableObject.CreateInstance(Type)`, o `AssetDatabase.LoadAssetAtPath<ScriptableObject>` cuando basta
con el tipo base.

**`AssetDatabase.SaveAssets()` guarda todo lo que esté sucio, incluida la escena abierta.** Útil, y
peligroso: persiste una edición de escena a medio hacer que nadie había guardado a propósito.

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

1. **Resuelto el 2026-09-19: `origin` existe.** Fernando creó el repo y empujó. `origin` apunta a
   `https://github.com/ningunFernando/GosipSimulator.git`. Del remote a la plantilla no queda nada: se
   quitó a propósito, ver [Origen](#origen). El push lo sigue haciendo Fernando.
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
4. **Construir el resto del chisme: hitos 8 a 11.** Los hitos 6 y 7 se cerraron el 2026-09-20: `Gossip`
   tiene sus cinco tipos, `Save` va por v2 con `RelationshipRow` y `RelationshipStore`, y hay 97 tests
   de EditMode más 10 de PlayMode cubriéndolo. Faltan las assemblies `Npcs`, `Actions` y `Shop`. El
   orden está en la tabla de [Estado](#estado) y el diseño en `ARCHITECTURE.md`. **El hito 8 es el
   siguiente y el que más cambia lo que se ve al jugar**, porque es el que hace que alguien presencie
   algo: hasta entonces toda la maquinaria existe y nadie la enciende.
5. **Cabos sueltos de autoría en los assets.** Uno de ellos acaba de encarecerse:
   - `NewGossipConfig.asset` conserva el nombre por defecto del `CreateAssetMenu`, y desde el hito 6
     **ya está referenciado** por el `GossipManager` de `Scene_Game`. Renombrarlo sigue siendo barato,
     pero ahora hay que hacerlo con `MoveAsset` desde el Editor, que conserva el GUID; renombrar el
     archivo desde fuera rompe la referencia de la escena.
   - `Robbery` lleva `baseDelta: -10`, y con `decayPercentPerHop: 40` el rumor llega al herrero en -5 y
     al aldeano en -1, y muere antes del anciano. Para que cruce la aldea hace falta cerca de -50, o
     bajar el decaimiento. Es una decisión de diseño, no un fallo, pero conviene tomarla a propósito.
     Ojo: `GossipFlowTests` afirma esos tres números, así que retocar el asset hace fallar el test, que
     es justo lo que se quiere de un valor que la documentación publica.
   - El `id` de una acción se escribe en `save.json` como `reason` de cada opinión que causa. Hoy es
     `robbery`. Cambiarlo después deja los saves viejos apuntando a una acción que ya no existe.
   - La carpeta `Runtime/Gosip/` contra la assembly `GosipSimulator.Gossip`, y `BlackSmith's Son` con S
     mayúscula contra `The Blacksmith`.
6. **Control táctil sin decidir.** Si el juego acaba siendo para móvil en vertical, ojo: el
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
