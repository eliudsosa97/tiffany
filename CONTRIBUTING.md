# Cómo colaborar

## Flujo

1. Antes de empezar, haz Pull de `main`.
2. Crea una rama para tu tarea: `<tipo>/<tema>`, por ejemplo `art/memory-1`.
3. Haz commits chicos, con título corto en inglés.
4. Sube la rama y abre un Pull Request a `main` con tres partes: por qué, qué cambia y cómo probarlo.
5. Todo entra por PR revisado. Nadie sube directo a `main`.

Si un PR pasa de 300 líneas, pártelo.

## Carpetas

| Área | Carpetas |
| --- | --- |
| Arte y recuerdos | `Assets/Art/Memories/`, `Assets/Audio/`, `Assets/Data/`, `docs/` |
| Código | `Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/` |

La escena `Assets/Scenes/Main.unity` no se edita a mano. La genera `Assets/Editor/SceneBuilder.cs`.

## Dibujos de los recuerdos

- Procreate, lienzo horizontal de 2048 × 1536 px, con el papel viejo dentro del dibujo.
- Crayón y letra de niña, con una frase corta.
- PNG llamado `memory-<recuerdo>-<dibujo>.png` en `Assets/Art/Memories/`. Por ejemplo, `memory-1-2.png`.
- La frase va también en el archivo del recuerdo, en `Assets/Data/`.

## Audio

WAV de 48 kHz, efectos en mono y nombres en inglés con guion bajo, como `witch_step_1.wav`. Todo sonido externo va en `CREDITS.md` con su licencia.

## IA

Si usas IA para algo que entra al juego, anótalo en `USO-DE-IA.md` en el mismo PR.
