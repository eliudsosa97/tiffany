# El Velador · Documento de diseño

| | |
| --- | --- |
| Género | Terror de sigilo en tercera persona |
| Plataforma | Navegador (WebGL), publicado en itch.io |
| Motor | Unity 6.3 LTS (`6000.3.27f1`) con URP |
| Duración | Unos 25 minutos |
| Controles | Teclado y ratón, o control |
| Jam | Spooky Game Jam de Hybridge, 26 al 30 de octubre de 2026 |

Los nombres de la niña y del xolo están pendientes. Mientras tanto el documento dice "la niña" y "el xolo".

## 1. Resumen

Es Día de Muertos. Una niña despierta de noche en un cerro y no sabe que está muerta. Hace un año subió sola a llevarle flores a su abuela y el velador del panteón la mató. Él guarda las almas de los niños que mata dentro de su farol, y la luz del farol son ellos. Esta noche su familia le puso una ofrenda por primera vez, el llamado la sacó del farol, y él sale a buscarla para recuperarla.

La niña sube el cerro a oscuras, recupera cinco recuerdos de esa noche y con cada uno gana un poder. Con los cinco sabe dónde quedó su cuerpo. En la capilla de la cima se manifiesta ante su familia y los guía hasta él. Al encontrar los cuerpos, las almas del farol quedan libres y el Velador se deshace.

El juego es de esconderse de algo que no puedes dañar. Cada paso hace ruido, la luz te ayuda a ver y también te delata, y la energía del alma alcanza para poco.

## 2. Cómo cumple el reto

| Requisito | En El Velador |
| --- | --- |
| Protagonista sobrenatural | Juegas como el alma de una niña que regresa en Día de Muertos. |
| Llega al mundo humano con un propósito | La ofrenda de su familia la llama. Viene a encontrar su cuerpo y a liberar a los niños del farol. |
| Interacción con el mundo humano | Se recarga en las veladoras que puso su familia, mueve objetos de los vivos (botellas, latas, macetas, campanitas) para distraer al Velador, y al final se manifiesta ante su familia y la guía. |
| Gestión de recursos | La energía del alma se gasta al alumbrar y al usar poderes, y solo se recarga en las veladoras. |
| Victoria y derrota | Ganas al llevar a tu familia hasta los cuerpos. Pierdes el intento si el Velador te atrapa y vuelves a la última veladora. |
| Mecánica principal con decisiones | Cada momento pide elegir entre ver y ser vista, correr y hacer ruido, o gastar energía en un poder y quedarte sin luz. |

## 3. Historia

### Hace un año

La niña subió sola al cerro en Día de Muertos a llevarle flores a la tumba de su abuela. Su xolo la siguió. En el panteón, el velador le ofreció acompañarla con su farol porque ya estaba oscuro. La mató a ella y al perro, y escondió los cuerpos en el panteón viejo, más arriba. Su alma quedó dentro del farol con las de otros niños.

Su familia la buscó durante meses. En el cerro siguen los carteles de SE BUSCA y las veladoras que dejaron por el camino.

### Esta noche

Este año la familia le pone por primera vez una ofrenda en la capilla de la cima. El llamado la saca del farol. Despierta en el cerro sin recordar nada, porque su memoria se quedó en el farol. Lo descubre en los primeros dos minutos: en la tumba de su abuela, junto a la foto de la abuela, está la suya.

El Velador sabe que se le escapó y sale a buscarla. Los niños del farol susurran su nombre cuando él está cerca.

### El final

Con los cinco recuerdos, la niña sabe dónde está su cuerpo. Sube a la capilla, donde su familia vela la ofrenda, y se manifiesta ante ellos. La siguen con sus linternas hasta el panteón viejo mientras el Velador ronda la capilla. Cuando la familia encuentra los cuerpos, las almas del farol se liberan y el Velador se deshace.

### Personajes

- **La niña.** Alma de unos nueve años. Brilla tenue. No ataca a nadie: se esconde, distrae y se manifiesta.
- **El Velador.** Cuidador nocturno del panteón. Hace su ronda con un farol de cortinilla y un manojo de llaves en la cintura. No se le puede dañar. Es el único enemigo del juego.
- **El xolo.** Era su perro y también murió esa noche. Ahora es un alma que la guía y le gruñe al Velador.
- **La familia.** Los vivos. Pusieron las veladoras y la ofrenda. Aparecen en la capilla al final.
- **Los niños del farol.** Las otras almas. Se oyen como susurros y aparecen como alucinaciones cuando el pánico sube.

## 4. Bucle de juego

1. Avanzas a oscuras hacia el siguiente recuerdo, escuchando el cerro y al Velador.
2. Decides cuándo alumbrar, por dónde pisar y cuándo esconderte.
3. Gastas energía en luz y poderes para pasar donde está él.
4. Llegas a una veladora de la familia: te recargas y queda como punto de regreso.
5. Encuentras el recuerdo: ves el eco de esa noche, ganas un poder y el Velador se vuelve más listo.

## 5. Controles

| Acción | Teclado y ratón | Control |
| --- | --- | --- |
| Moverse | WASD | Stick izquierdo |
| Caminar despacio | Ctrl (mantener) | Gatillo izquierdo |
| Correr | Shift (mantener) | Stick izquierdo presionado |
| Apuntar la luz | Ratón | Stick derecho |
| Prender o apagar la luz | F o clic derecho | Botón norte |
| Destello | Clic izquierdo | Gatillo derecho |
| Poder (encender, mover, desvanecerse) | Q, E, R | Botones de los lados |
| Esconderse e interactuar | Espacio | Botón sur |
| Cuaderno de recuerdos | Tab | Select |
| Pausa | Esc | Start |

Los poderes aparecen en los controles conforme se ganan.

## 6. Mecánicas

Los números de esta sección son valores iniciales para afinar jugando.

### 6.1 Ruido

Cada paso hace ruido según el suelo. El Velador lo oye dentro de un radio. No hay indicador en pantalla: se oye.

| Suelo | Ruido | Radio caminando |
| --- | --- | --- |
| Tierra | Muy bajo | 2 m |
| Zacate | Bajo | 4 m |
| Losas del panteón | Bajo, con eco | 4 m |
| Grava | Medio | 8 m |
| Hojarasca | Alto | 14 m |
| Ramas secas | Muy alto | 20 m |

Correr duplica el radio y caminar despacio lo reduce a la mitad. La lluvia tapa el ruido y el trueno tapa cualquier ruido mientras suena.

### 6.2 Energía del alma

Es el recurso del juego. Empieza en 100.

| Uso | Costo |
| --- | --- |
| Luz encendida | 1 por segundo |
| Destello | 50 |
| Encender veladora | 20 |
| Mover un objeto | 15 |
| Desvanecerse | 30 |

Solo se recarga junto a una veladora de la familia, a 20 por segundo. Con la energía en cero no hay luz ni poderes: queda caminar a oscuras hasta la próxima veladora. La energía se ve en la propia luz de la niña, que se achica y parpadea cuando queda poca.

### 6.3 Luz

La niña sostiene las manos juntas, como si cargara una vela, y de ahí sale su luz. Alumbra un cono amplio y cálido sobre el suelo, hacia donde apunta. Con la luz ves el camino, los objetos y a los pájaros amarillos que lo marcan. El Velador también te ve a ti desde lejos.

El destello ciega al Velador 2 segundos si está dentro del cono. Sirve para escapar de una persecución, no para ganarle.

### 6.4 Veladoras

- **De la familia.** Están en vasos de vidrio y no se apagan. Recargan la energía, son zona segura (el Velador no entra a su luz) y son el punto de regreso.
- **Las que enciende la niña** (poder 2). Son zona segura mientras duran: 90 segundos, la mitad con lluvia.

### 6.5 Pánico

El pánico sube a oscuras, cerca del Velador, al oír los susurros y cuando un relámpago lo muestra. Baja en la luz de una veladora, escondida y quieta, o con el xolo cerca. A oscuras sube más rápido.

| Pánico | Efecto |
| --- | --- |
| 50 % | Pasos que no existen |
| 75 % | Faroles falsos a lo lejos y susurros |
| 90 % | Los niños del farol en la orilla de la luz. La niña solloza (ruido medio cada pocos segundos) y su luz tiembla. |

Los latidos se aceleran con el pánico y con la intención del Velador.

### 6.6 Esconderse

Matorrales de bugambilia, lápidas grandes, nichos abiertos y la mesa de la caseta. Escondida no haces ruido. El Velador solo te encuentra si te vio entrar. Cuando sospecha, revisa el escondite más cercano al ruido.

### 6.7 Poderes

Uno por recuerdo, en este orden:

| Recuerdo | Dónde | Poder |
| --- | --- | --- |
| 1. Flores | Tumba de la abuela | Controlar su luz: prender, apagar y destello |
| 2. Listón | Caseta del velador | Encender veladoras apagadas |
| 3. Muñeca | El sendero | Mover objetos a distancia, como poltergeist |
| 4. Zapato | La barranca | Desvanecerse unos segundos |
| 5. Medalla | Panteón viejo | Manifestarse ante los vivos |

**Mover objetos.** Solo objetos visibles bajo su luz o la de una veladora, a 12 m o menos, una vez cada uno. Cada objeto suena distinto y a distinto alcance (botella, lata, maceta, campanita, veladora). El Velador va a revisar el ruido: sirve de señuelo.

**Desvanecerse.** Durante 3 segundos la niña no se ve ni se oye. Si el Velador la perseguía, pierde el rastro.

**Manifestarse.** Solo funciona ante los vivos, en el final.

### 6.8 El Velador

| Estado | Qué hace | Cómo lo notas |
| --- | --- | --- |
| Ronda | Recorre su camino con el farol abierto. | Ves su luz y oyes las llaves. |
| Sospecha | Oyó o vio algo. Va al punto y revisa el escondite más cercano. | Se detiene y gira el farol. |
| Caza | Cierra el farol y sujeta las llaves. Te busca a oscuras. | Un "clac" metálico y luego silencio. Los niños susurran tu nombre. |
| Persecución | Te vio. Va directo a ti, más lento que tú corriendo. | Pasos rápidos y latidos al máximo. |
| Te atrapa | Pierdes el intento. | La pantalla se cierra como la cortinilla del farol. |

Si te pierde de vista unos 8 segundos, vuelve a la ronda. Siempre avisa antes de atacar: primero nota, luego se acerca y al final te alcanza.

| | Velocidad |
| --- | --- |
| Velador en ronda | 1.4 m/s |
| Velador persiguiendo | 3.6 m/s |
| Niña caminando | 2.2 m/s |
| Niña corriendo | 4.0 m/s |

### 6.9 Tormenta

La lluvia tapa tu ruido y el suyo, y apaga las veladoras que enciende la niña. Un relámpago muestra todo durante 1 segundo, a él y a ti. El trueno tapa cualquier ruido. En la barranca hay un relámpago guionado que lo muestra justo detrás de ti.

### 6.10 El xolo

Te guía hacia el siguiente recuerdo si te quedas quieta unos segundos. Gruñe bajito cuando el Velador está a menos de 20 m, aunque no se vea. Es un alma: el Velador no lo ve ni lo oye. Cerca de él el pánico baja.

### 6.11 Recuerdos

Cada recuerdo tiene dos partes:

- **El eco.** De 10 a 15 segundos de esa noche en el mismo lugar, con el mundo desaturado. La niña viva camina junto al Velador, que todavía es amable.
- **Los dibujos.** De 3 a 4 dibujos de niña, con crayón en papel viejo y una frase en letra infantil. Son unos 15 en total y se pueden volver a ver en el cuaderno.

## 7. Victoria y derrota

- **Victoria.** Con los cinco recuerdos, manifestarte ante tu familia en la capilla y guiarla hasta el panteón viejo.
- **Derrota.** El Velador te atrapa. Vuelves a la última veladora de la familia que tocaste, con la energía a la mitad. Los recuerdos ganados se conservan.

## 8. Capítulos

Se sube el cerro de abajo hacia arriba.

| Capítulo | Zona | Recuerdo | El Velador | Momento clave |
| --- | --- | --- | --- | --- |
| Prólogo | Pie del cerro y panteón del pueblo | | No aparece. Se oyen sus llaves a lo lejos. | Aprendes a moverte a oscuras. Encuentras la tumba con tu foto. |
| 1 | Tumba de la abuela | Flores | Lejos, con el farol abierto. | Primer recuerdo y primera luz. |
| 2 | Caseta del velador | Listón | Cruza tu camino. | Entras a su caseta mientras él hace la ronda. |
| 3 | El sendero | Muñeca | Cierra el farol cuando oye algo. | Primera caza a oscuras. Aprendes a usar señuelos. |
| 4 | La barranca | Zapato | Deja la ronda y te busca. | Tormenta y persecución. El relámpago lo muestra detrás de ti. |
| 5 | Panteón viejo | Medalla | Su panteón, con faroles falsos. | Descubres dónde está tu cuerpo. |
| Final | Capilla de la cima | | Rodea la capilla. | Te manifiestas ante tu familia y la guías. |

## 9. Arte y ambientación

Noche cerrada con luna visible. El cerro se basa en el Cerro de Bugambilias (Jalisco), con fotos y grabaciones tomadas en el lugar: pinos, encinos, bugambilias, grava y hojarasca. Realismo estilizado, personajes con proporciones normales y poca visibilidad: solo se ve con la luz de la niña, las veladoras, el farol del Velador y los relámpagos.

La cámara va en tercera persona, alta y en diagonal, siguiendo a la niña. La luz de la niña cae plana sobre el suelo, como una linterna de mano, nunca como un haz en el aire.

Los recuerdos cambian de registro: el mundo se desatura y los dibujos son de crayón sobre papel viejo, hechos a mano en Procreate.

## 10. Audio

- **Ambiente.** Domina el cerro: grillos, viento, hojas, animales lejanos y silencios. Grabaciones propias del Cerro de Bugambilias y sonidos con licencia CC0.
- **El Velador.** Llaves al caminar, el "clac" de la cortinilla al cerrar el farol y sus pasos en cada suelo.
- **Tensión.** Latidos que se aceleran con el pánico y con la intención del Velador. Música de tensión mínima en la caza y la persecución.
- **Susurros.** Los niños del farol dicen el nombre de la niña cuando él caza cerca.

## 11. Interfaz

Casi sin interfaz. La energía se ve en la luz de la niña y el ruido se oye. Hay un aviso corto al ganar cada poder, el cuaderno de recuerdos con los dibujos, la pausa y subtítulos para las frases de los dibujos y los ecos.

## 12. Técnica

- Unity `6000.3.27f1`, URP y build WebGL para itch.io. Meta: 60 fps en un navegador de laptop.
- Input System con teclado, ratón y control.
- Sin materiales ni shaders creados en tiempo de ejecución, porque WebGL quita las variantes que no ve en el build.
- La lógica (ruido, energía, pánico, estados del Velador) vive en clases de C# probadas con el Unity Test Framework. Los componentes de Unity solo la conectan con la escena.
- El contenido de los recuerdos (frases, dibujos, ecos) vive en archivos de datos para editarlo sin tocar código.

## 13. Alcance y prioridades

Pesa más una mecánica pulida que muchos sistemas. El orden de trabajo es:

1. **Núcleo.** Moverse, ruido por suelo, el Velador con sus cinco estados, luz y energía, veladoras de la familia, atrapar y regresar. Una zona jugable de punta a punta, en el navegador. Se afina hasta que dé miedo antes de sumar lo demás.
2. **Progresión.** Los cinco poderes, los cinco recuerdos con eco y dibujos, los capítulos con el Velador cada vez más listo, el xolo y el pánico.
3. **Atmósfera y final.** Tormenta, alucinaciones, el final con la familia y pulido.

Si el tiempo no alcanza, se recorta en este orden: la tormenta queda solo en la barranca, se quitan los faroles falsos, se quitan las alucinaciones de niños y el final se vuelve una escena sin control.

## 14. Equipo y uso de IA

- Eliud Sosa: dirección, diseño de juego y programación.
- Clau (clauperspectiva): dirección de arte y narrativa de los recuerdos, dibujos.

El uso de inteligencia artificial está declarado por área en [USO-DE-IA.md](../USO-DE-IA.md). Los créditos de recursos externos están en [CREDITS.md](../CREDITS.md).

## 15. Pendiente

- Nombre de la niña y del xolo.
