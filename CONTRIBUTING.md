# Cómo colaborar

## Flujo

1. Antes de empezar, baja lo último de `main` (en GitHub Desktop: Fetch origin y luego Pull).
2. Crea una rama para tu tarea: `<tipo>/<tema>`, por ejemplo `art/memory-1` o `data/memory-texts`.
3. Haz commits chicos con un título corto en inglés y, si hace falta, un cuerpo en español.
4. Sube la rama y abre un Pull Request hacia `main` con tres partes: por qué, qué cambia y cómo probarlo.
5. Un PR se revisa antes de entrar. Nadie empuja directo a `main`.

Un PR pequeño se revisa rápido. Si pasa de unas 300 líneas, pártelo.

## Carpetas

Cada área tiene sus carpetas, para no pisarnos:

| Área | Carpetas |
| --- | --- |
| Arte y narrativa de los recuerdos | `Assets/Art/Memories/`, `Assets/Audio/`, `Assets/Data/`, `docs/` |
| Código y escena | `Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/` |

La escena `Assets/Scenes/Main.unity` no se edita a mano: la genera `Assets/Editor/SceneBuilder.cs`.

## Dibujos de los recuerdos

Se hacen en Procreate y se ven a pantalla completa en el cuaderno de recuerdos.

1. Lienzo horizontal de 2048 × 1536 px, con el papel viejo incluido en el dibujo.
2. Crayón y letra de niña. Cada dibujo lleva una frase corta.
3. Exporta como PNG desde Acciones, Compartir, PNG.
4. Nombre del archivo: `memory-<recuerdo>-<dibujo>.png`, por ejemplo `memory-1-2.png` para el segundo dibujo del primer recuerdo.
5. Guárdalo en `Assets/Art/Memories/` y escribe la frase en el archivo de datos del recuerdo en `Assets/Data/`.

## Audio

WAV de 48 kHz. Efectos en mono. Nombres en inglés con guion bajo, por ejemplo `keys_jingle_1.wav`. Cada sonido externo se acredita en `CREDITS.md` con su licencia.

## Inteligencia artificial

Si usas IA para algo que entra al juego, agrégalo a `USO-DE-IA.md` en el mismo PR.
