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
```

Con el editor abierto, el modo batch falla.

## Git

- `main` está protegida. Todo entra por PR revisado y se mergea con squash.
- Ramas `<tipo>/<tema>`, por ejemplo `feat/noise-by-ground` o `art/memory-1`.
- PR de 200 a 300 líneas como máximo.
- Reglas de commit y PR en la skill `git-flow`.

Skills del repo en `.agents/skills/`, enlazadas desde `.claude/skills/`.
