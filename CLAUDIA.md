# Instructivo de Claudia

Este archivo es solo para ti. Dice qué te toca, en qué orden y cómo entregarlo. Se borra antes de hacer público el repo.

## Qué te toca

Eres la directora de arte y narrativa de los recuerdos. El código, la escena y la bruja los hacen Eliud y sus agentes. Tú haces lo que cuenta la historia de esa noche:

1. El guion de los cinco recuerdos.
2. Los dibujos de cada recuerdo, en Procreate.
3. El archivo de datos de cada recuerdo, con las frases de los dibujos.
4. Si sobra tiempo: el storyboard de los ecos y los carteles de SE BUSCA.

No necesitas Unity para nada de esto. Instalarlo es decisión tuya y puede esperar.

## Calendario

| Cuándo | Qué |
| --- | --- |
| Miércoles 28 | Leer el juego (30 min). Guion de los cinco recuerdos. Dibujos y datos del recuerdo 1. |
| Jueves 29 | Dibujos y datos de los recuerdos 2 a 5. |
| Viernes 30, 9:00 | Todo entregado. A esa hora se congela el juego. |
| Viernes 30, 12:00 | Entrega de la jam. |

Si no da tiempo, es mejor tener tres recuerdos completos que cinco a medias. Ve uno por uno.

## Antes de empezar: tu Mac

Una sola vez:

1. El repo se renombró de `el-velador` a `tiffany`. Si ya lo tenías clonado, sigue funcionando porque GitHub redirige el nombre viejo. Si no, clónalo en GitHub Desktop: File, Clone repository, `eliudsosa97/tiffany`.
2. Clona también `eliudsosa97/taller-videojuegos` en la misma carpeta que `tiffany`.
3. En la Terminal, dentro de `taller-videojuegos`, corre `./link.sh ../tiffany`. Con eso tu Claude carga el modo aprendiz cuando lo abras dentro de `tiffany`.
4. Abre Claude dentro de `tiffany` y dile: "Lee CLAUDIA.md y ayúdame con el paso en el que voy".

## Paso 0. Entender el juego (30 minutos)

Lee en este orden, nada más:

1. `README.md`: de qué trata, en un párrafo.
2. `docs/GDD.md`, secciones 1 (Resumen), 3 (Historia), 6.7 (Poderes), 6.11 (Recuerdos) y 8 (Capítulos).

Lo esencial: Tiffany es el alma de una niña de nueve años. Hace un año, en Día de Muertos, subió sola al cerro a llevarle flores a su abuela. En la tumba la esperaba su abuela, pero era la bruja disfrazada, y se la llevó. Esta noche Tiffany despierta sin memoria y sube el cerro recuperando cinco recuerdos de esa noche. Cada recuerdo le da un poder y el último le dice dónde quedó su cuerpo.

## Lo que ya está fijo de esa noche

Esto ya está decidido en el juego. Lo demás lo inventas tú.

- Era Día de Muertos, hace un año. Tiffany tenía nueve años y subió sola al cerro a llevarle flores a la tumba de su abuela, que ya había muerto.
- Tyler, su perro, la siguió.
- En la tumba la esperaba su abuela y le ofreció acompañarla de regreso porque ya estaba oscuro. No era su abuela: era la bruja del cerro con la cara de la abuela.
- La bruja se llevó a Tiffany y a Tyler cerro arriba. Los dos murieron esa noche.
- La bruja escondió los cuerpos en el panteón viejo, cerca de la cima. El alma de la niña quedó en su fuego con las de otros niños.
- Su familia la buscó durante meses. Dejaron carteles de SE BUSCA y veladoras por el camino.

## Los personajes que vas a dibujar

- **Tiffany.** Cómo es lo decides tú: pelo, ropa, lo que traiga. Tenía nueve años. Lo que dibujes se vuelve su aspecto en el juego y en los carteles.
- **La abuela.** También la decides tú. En los primeros recuerdos se ve como una abuela normal. Recuerdo a recuerdo se va notando que es la bruja.
- **La bruja.** Ya está descrita en `docs/GDD.md`, sección 6.8: más de dos metros, flaca hasta el hueso, un hombro más alto que el otro, un brazo que casi arrastra, dedos larguísimos, la cabeza ladeada, pelo largo y negro, rebozo roto y descalza.
- **Tyler.** Es un perro real: un jack russell terrier blanco. Pídele fotos a Eliud.
- **Los objetos de cada recuerdo** (flores, listón, muñeca, zapato, medalla): cómo son y por qué los traía la niña lo decides tú.

## Límite de contenido

Las bases de la jam no permiten contenido +18, gore extremo ni discriminación. La niña muere, pero no se dibuja cómo. El miedo va en lo que está mal en la abuela, no en sangre ni heridas.

## Paso 1. El guion de los cinco recuerdos

Los recuerdos ya tienen objeto y lugar. Lo que pasa en cada uno lo decides tú:

| Recuerdo | Objeto | Lugar | Poder que gana |
| --- | --- | --- | --- |
| 1 | Flores | Tumba de la abuela | Controlar su luz |
| 2 | Listón | Jacal de la bruja | Encender veladoras |
| 3 | Muñeca | El sendero | Mover objetos |
| 4 | Zapato | La barranca | Desvanecerse |
| 5 | Medalla | Panteón viejo | Manifestarse ante los vivos |

Van en orden de subida y cuentan esa noche de principio a fin: el 1 empieza en la tumba y el 5 termina donde quedaron los cuerpos. Lo que pasó en medio (por qué pasaron por el jacal, el sendero y la barranca, y cómo llegó cada objeto a cada lugar) lo inventas tú. Para cada recuerdo escribe:

- **Qué pasó ahí esa noche**, en tres o cuatro frases.
- **El eco:** de 10 a 15 segundos que el jugador ve en ese lugar. La niña viva camina de la mano de su abuela. En cada eco algo de la abuela está un poco más mal (la mano demasiado larga, la cabeza ladeada). En el 1 casi no se nota y en el 5 ya es la bruja.
- **Los dibujos:** tres por recuerdo (quince en total), cada uno con una frase corta en letra de niña.

Guárdalo en `docs/recuerdos.md`. Puede ser tan simple como una lista por recuerdo.

## Paso 2. Los dibujos

Son los dibujos que la niña hizo, como si fueran suyos. Se ven a pantalla completa en el cuaderno de recuerdos.

1. Lienzo horizontal de 2048 × 1536 px, con el papel viejo incluido en el dibujo.
2. Crayón y letra de niña. Cada dibujo lleva su frase escrita.
3. Exporta como PNG: Acciones, Compartir, PNG.
4. Nombre: `memory-<recuerdo>-<dibujo>.png`. Por ejemplo, `memory-1-2.png` es el segundo dibujo del recuerdo 1.
5. Va en `Assets/Art/Memories/`.

## Paso 3. El archivo de datos de cada recuerdo

Un archivo por recuerdo en `Assets/Data/Memories/`, llamado `memory-<n>.json`. Ejemplo del recuerdo 1:

```json
{
  "id": 1,
  "title": "Flores",
  "place": "Tumba de la abuela",
  "drawings": [
    { "image": "memory-1-1", "phrase": "..." },
    { "image": "memory-1-2", "phrase": "..." },
    { "image": "memory-1-3", "phrase": "..." }
  ]
}
```

- `id` es el número del recuerdo, de 1 a 5.
- `image` es el nombre del PNG sin `.png`.
- `phrase` es la frase del dibujo, la misma que escribiste en él. El juego la usa como subtítulo.
- El poder no va aquí: lo pone el código según el número.
- Las comillas y las comas importan. Si el archivo está mal escrito, el juego se salta ese recuerdo y avisa por qué. Tu Claude te puede revisar el formato.

## Paso 4. Si sobra tiempo

- **Storyboard de los ecos.** Tres o cuatro viñetas por eco: dónde está la niña, dónde está la abuela, qué hacen. Sirve para armar la escena. Va en `docs/`.
- **Carteles de SE BUSCA** con la foto de Tiffany, para pegarlos en el cerro. PNG en `Assets/Art/`.

## Cómo entregar

Un Pull Request por recuerdo. Así cada uno se revisa rápido y entra aunque el siguiente no esté listo.

1. En GitHub Desktop: Fetch origin y luego Pull, para tener lo último de `main`.
2. Crea una rama: `art/memory-1` (luego `art/memory-2`, etc.).
3. Agrega los PNG, el JSON y lo de `docs/recuerdos.md` de ese recuerdo.
4. Commit con un título corto en inglés, por ejemplo `Add memory 1 drawings and data`.
5. Push y abre el Pull Request hacia `main`. En la descripción: qué recuerdo es y qué trae.
6. La torre lo revisa y te deja comentarios. Lo mergea la torre, tú no.

Lo tuyo no se corrige en estilo: solo se te pide cambiar algo si rompe el juego (nombre del archivo, tamaño del PNG, formato del JSON).

## Lo que no tocas

- `Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/`: el código.
- `Assets/Scenes/`: la escena se genera sola por código.
- Tus carpetas son `Assets/Art/Memories/`, `Assets/Audio/`, `Assets/Data/` y `docs/`.

## Tu Claude

Tu Claude trabaja en modo aprendiz: te explica antes de hacer y te deja hacer a ti las partes chicas. Pídele cosas concretas de este instructivo, por ejemplo: "ayúdame a revisar que `memory-1.json` esté bien escrito" o "explícame cómo abrir el PR del recuerdo 1". El guion y los dibujos son tuyos: úsalo para ordenar ideas, no para que los escriba.

Si te atoras más de 20 minutos en algo técnico, escríbele a Eliud.
