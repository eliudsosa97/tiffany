# Guía para agentes de código

Para cualquier agente que trabaje en este repo. Antes de cambiar una mecánica, lee `docs/GDD.md`.

## Proyecto

Juego de terror y sigilo en Unity 6.3 LTS `6000.3.27f1` con URP, para navegador (WebGL) en itch.io. Sin multijugador.

## Idioma

- Código, identificadores, nombres de archivo y títulos de commit y PR en inglés.
- Comentarios, cuerpos de commit y PR, y textos del juego en español.

## Convenciones de código

- Todo el código vive en el namespace `TiffanyGame`. Las herramientas de editor en `TiffanyGame.EditorTools` y los tests en `TiffanyGame.Tests`.
- La lógica va en clases de C# simples y testeables (ruido, energía del alma, pánico, estados de la bruja, recuerdos, poderes). Los `MonoBehaviour` son delgados: leen la escena, llaman a la lógica y aplican el resultado.
- Solo Input System. Toda la entrada pasa por `GameInput`.
- UI con `UnityEngine.UI.Text` y la fuente `LegacyRuntime.ttf`. Sin TextMeshPro.
- Sin asignaciones de memoria por cuadro en `Update`: nada de LINQ, strings concatenados, `Find` ni `GetComponent` en el ciclo caliente.
- Materiales y shaders como assets, nunca creados en tiempo de ejecución.
- Audio de efectos en 2D (`spatialBlend = 0`) salvo que el diseño pida posición.
- Frases, dibujos y ecos de los recuerdos viven en `Assets/Data/`, no en código.

## Cómo cambiar código

- Cambios quirúrgicos: solo lo que pide la tarea.
- Busca código reutilizable antes de escribir nuevo. Abstrae solo cuando aparece un segundo uso real.
- Un refactor va en un PR aparte de la funcionalidad.
- Comentarios mínimos: solo un constraint que el código no puede expresar, en una línea. Antes de cada PR se aplica la skill `clean-comments`.
- No agregues paquetes a `Packages/manifest.json` sin aprobación.

## Escena generada

`Assets/Editor/SceneBuilder.cs` genera `Assets/Scenes/Main.unity` completa. **Regenerar la escena borra lo editado a mano.** Se cambia el builder y se regenera. La escena regenerada se commitea solo en `main`.

## Presupuesto de rendimiento

La meta es 60 fps en el navegador con p95 del cuadro menor a 20 ms. Medido el 28-oct en Brave sobre un M2, con la ruta en gris de 6f02142: los ocho claros dan 60 fps y p95 de 17.6 a 18.6 ms con canvas de 1600x900 y de 2400x1350. Por cuadro hay entre 18 y 33 llamadas de dibujo y entre 3.4 y 7.8 mil triángulos (mediana de 6 s). La memoria WASM es de 96 MB. En la escena hay entre 21 y 64 renderers y entre 1 y 4 luces por claro. El cementerio es el más cargado.

Los techos son provisionales. El M2 va sobrado y falta validarlos en un portátil con gráficos integrados.

Por claro, con la bruja en pantalla:

- Renderers bajo `Route <n> <nombre>`, contados con `GetComponentsInChildren<Renderer>(true)` en la escena generada: 200 como máximo.
- Llamadas de dibujo por cuadro: 150 como máximo.
- Triángulos por cuadro: 150 mil como máximo.
- Luces de punto o foco en pantalla: 8 como máximo. Cada objeto recibe 4 luces adicionales como máximo (límite del asset de URP para web), así que una quinta luz cerca no se ve en él.
- Sombras: solo las de la luna, porque el asset de URP para web no tiene sombras de luces adicionales. Lo estático de la ruta va sin sombras.
- Memoria WASM: 256 MB como máximo.

Por modelo:

- Lo estático lleva `BatchingStatic` y comparte material con lo de su claro.
- Si un claro pasa los techos, la vegetación repetida pasa a `Graphics.RenderMeshInstanced` y sus posiciones en un ScriptableObject, no como un objeto por planta. Su material lleva `enableInstancing`, que `GeneratedMaterials.Get` no activa. Lo que la bruja usa como cobertura y los escondites (`HideVolume`) siguen siendo objetos con collider.
- Texturas de 1K como máximo y comprimidas en el import.
- Sin efectos de posproceso nuevos en `Mobile_Renderer` sin medir antes y después.

Quien agregue modelos a la ruta mide antes y después y pone las cifras en el PR. Cómo se mide: build web servida con `python3 -m http.server` y el Brave de `/Applications` manejado por CDP. Cada claro se abre con `?stop=Route <n> <nombre>`, que vive en la rama `feat/web-capture-stop` y se aplica solo en local porque no entra a la build pública. En cada claro se miden 6 s con `requestAnimationFrame`, con DPR 1 y 2. Las llamadas de dibujo y los triángulos se cuentan envolviendo `drawElements`, `drawArrays`, `drawElementsInstanced` y `drawArraysInstanced` del contexto WebGL (los triángulos con instancing se multiplican por las instancias), y se reporta el cuadro más alto además de la mediana.

## Estructura

```
Assets/
  Scripts/      Core, Player, Witch, Soul, Noise, Powers, Memories, World, UI, Audio, Debug
  Editor/       SceneBuilder, BuildTools y herramientas de editor
  Tests/        EditMode (lógica) y PlayMode (prueba de humo)
  Data/         recuerdos y textos
  Art/Memories/ dibujos de los recuerdos
  Audio/        grabaciones y efectos
docs/           GDD y documentos del equipo
```

## Pruebas

- Lógica: tests EditMode del Unity Test Framework.
- Una prueba de humo PlayMode que carga la escena y juega unos segundos.
- Un arreglo trae un test que falla sin el arreglo.
- Lo visual se comprueba con el recorrido de capturas (`-autocapture`) y mirando las imágenes.

## Comandos

Con el editor de Unity cerrado. En macOS, `UNITY` suele ser `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity`.

```sh
# Regenerar la escena
$UNITY -batchmode -projectPath . -executeMethod TiffanyGame.EditorTools.SceneBuilder.BuildFromCommandLine -logFile -
# Tests
$UNITY -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -
# Builds
$UNITY -batchmode -projectPath . -executeMethod TiffanyGame.EditorTools.BuildTools.BuildWeb -logFile -
$UNITY -batchmode -projectPath . -executeMethod TiffanyGame.EditorTools.BuildTools.BuildMac -logFile -
# Recorrido con capturas del build de Mac
"Builds/Mac/tiffany.app/Contents/MacOS/Tiffany" -autocapture <carpeta> -screen-width 1600 -screen-height 900 -screen-fullscreen 0
# Video: cuadros PNG a 30 fps durante los segundos indicados
"Builds/Mac/tiffany.app/Contents/MacOS/Tiffany" -capturevideo <carpeta> -captureseconds 10 -screen-width 1600 -screen-height 900 -screen-fullscreen 0
"Builds/Mac/tiffany.app/Contents/MacOS/Tiffany" -motiondemo -capturevideo <carpeta> -captureseconds 10 -screen-width 1600 -screen-height 900 -screen-fullscreen 0
"Builds/Mac/tiffany.app/Contents/MacOS/Tiffany" -playthrough -logFile <archivo> -screen-width 1600 -screen-height 900 -screen-fullscreen 0 # Salidas: 0 completa, 1 fallo, 2 completa con faltantes. Con -capturevideo, la partida decide la salida.
```

Con el editor abierto, el modo batch falla.

## Git

- `main` está protegida. Todo entra por PR revisado y se mergea con squash.
- Ramas `<tipo>/<tema>`, por ejemplo `feat/noise-by-ground` o `art/memory-1`.
- PR de 200 a 300 líneas como máximo.
- Reglas de commit y PR en la skill `git-flow`.

Skills del repo en `.agents/skills/`, enlazadas desde `.claude/skills/`.
