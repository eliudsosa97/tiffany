# Tiffany · Documento de diseño

| | |
| --- | --- |
| Género | Terror de sigilo en tercera persona |
| Plataforma | Navegador (WebGL), publicado en itch.io |
| Motor | Unity 6.3 LTS (`6000.3.27f1`) con URP |
| Duración | Unos 25 minutos |
| Controles | Teclado y ratón, o control |
| Jam | Spooky Game Jam de Hybridge, 26 al 30 de octubre de 2026 |

## 1. Resumen

Es Día de Muertos. Tiffany, una niña de nueve años, despierta de noche en un cerro y no sabe que está muerta. Hace un año subió sola a llevarle flores a su abuela, y en la tumba la esperaba su abuela. No era ella. Era la bruja del cerro, que se la llevó. La bruja vive de las almas de los niños y las guarda en su fuego: las bolas de fuego que se ven volar sobre el cerro son ellos. Esta noche la familia de Tiffany le puso una ofrenda por primera vez, el llamado la sacó del fuego, y la bruja sale a buscarla.

La niña sube el cerro a oscuras con Tyler, su perro, recupera cinco recuerdos de esa noche y con cada uno gana un poder. Con los cinco sabe dónde quedó su cuerpo. En la capilla de la cima se manifiesta ante su familia y los guía hasta él. Al encontrar los cuerpos, las almas del fuego quedan libres y la bruja se consume.

El juego es de esconderse de algo que no puedes dañar. La bruja caza de dos formas. Volando, como bola de fuego, ve tu luz desde muy lejos. En el suelo es un monstruo que te oye, te huele y se lanza sobre ti. Cada momento pide leer en qué forma está y cambiar de estrategia.

## 2. Cómo cumple el reto

| Requisito | En Tiffany |
| --- | --- |
| Protagonista sobrenatural | Juegas como el alma de una niña que regresa en Día de Muertos. |
| Llega al mundo humano con un propósito | La ofrenda de su familia la llama. Viene a encontrar su cuerpo y a liberar a los niños del fuego. |
| Interacción con el mundo humano | Se recarga en las veladoras que puso su familia, mueve objetos de los vivos (botellas, latas, macetas, campanitas) para distraer a la bruja, y al final se manifiesta ante su familia y la guía. |
| Gestión de recursos | La energía del alma se gasta al alumbrar y al usar poderes, y solo se recarga en las veladoras. |
| Victoria y derrota | Ganas al llevar a tu familia hasta los cuerpos. Pierdes el intento si la bruja te atrapa y vuelves a la última veladora. |
| Mecánica principal con decisiones | Ver o esconder tu luz, correr o pisar despacio, esquivar una embestida o gastar energía en un poder. |

## 3. Historia

### Hace un año

Tiffany subió sola al cerro en Día de Muertos a llevarle flores a la tumba de su abuela. Tyler, su perro, la siguió. En la tumba estaba su abuela, esperándola, y le ofreció acompañarla de regreso porque ya estaba oscuro. No era su abuela. La bruja se las llevó a ella y a Tyler, y escondió los cuerpos en el panteón viejo, más arriba. El alma de la niña quedó en su fuego con las de otros niños.

Su familia la buscó durante meses. En el cerro siguen los carteles de SE BUSCA y las veladoras que dejaron por el camino.

### Esta noche

Este año la familia le pone por primera vez una ofrenda en la capilla de la cima. El llamado la saca del fuego. Despierta en el cerro sin recordar nada, porque su memoria se quedó en el fuego. Lo descubre en los primeros dos minutos: en la tumba de su abuela, junto a la foto de la abuela, está la suya.

La bruja sabe que se le escapó y sale a buscarla. Los niños de su fuego susurran "Tiffany" cuando la bruja está cerca.

### El final

Con los cinco recuerdos, la niña sabe dónde está su cuerpo. Sube a la capilla, donde su familia vela la ofrenda, y se manifiesta ante ellos. La siguen con sus linternas hasta el panteón viejo mientras la bruja ronda la capilla. Cuando la familia encuentra los cuerpos, las almas del fuego se liberan y la bruja se consume.

### Personajes

- **Tiffany.** Alma de una niña de nueve años. Brilla tenue. No ataca a nadie: se esconde, distrae y se manifiesta.
- **La bruja.** La bruja del cerro. Es la única enemiga del juego y no se le puede dañar. Ver 6.8.
- **Tyler.** Su perro, un jack russell terrier. También murió esa noche y ahora es un alma que la guía y le ladra a la bruja. Su pelo blanco se ve en la oscuridad.
- **La familia.** Los vivos. Pusieron las veladoras y la ofrenda. Aparecen en la capilla al final.
- **Los niños del fuego.** Las otras almas. Se oyen como susurros y aparecen como alucinaciones cuando el pánico sube.

## 4. Bucle de juego

1. Avanzas a oscuras hacia el siguiente recuerdo, mirando el cielo y escuchando el cerro.
2. Decides cuándo alumbrar, por dónde pisar y cuándo esconderte, según en qué forma esté la bruja.
3. Gastas energía en luz y poderes para pasar donde está ella.
4. Llegas a una veladora de la familia: te recargas y queda como punto de regreso.
5. Encuentras el recuerdo: ves el eco de esa noche, ganas un poder y la bruja se vuelve más peligrosa.

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

Cada paso hace ruido según el suelo. La bruja lo oye dentro de un radio cuando está en el suelo. Volando no oye. No hay indicador en pantalla: se oye.

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

La niña sostiene las manos juntas, como si cargara una vela, y de ahí sale su luz. Alumbra un cono amplio y cálido sobre el suelo, hacia donde apunta. Con la luz ves el camino, los objetos y a los pájaros amarillos que lo marcan. La bruja, si vuela, la ve desde muy lejos.

El destello ciega a la bruja 2 segundos si está en el suelo y dentro del cono, y corta una embestida a medio aviso. Sirve para escapar, no para ganarle.

### 6.4 Veladoras

- **De la familia.** Están en vasos de vidrio y no se apagan. Recargan la energía, son zona segura (la bruja no entra a su luz ni baja sobre ella) y son el punto de regreso.
- **Las que enciende la niña** (poder 2). Son zona segura mientras duran: 90 segundos, la mitad con lluvia.

### 6.5 Pánico

El pánico sube a oscuras, cerca de la bruja, al oír los susurros o la voz que imita, y cuando un relámpago la muestra. Baja en la luz de una veladora, escondida y quieta, o con Tyler cerca. A oscuras sube más rápido.

| Pánico | Efecto |
| --- | --- |
| 50 % | Pasos que no existen |
| 75 % | Fuegos falsos en el cielo y susurros |
| 90 % | Los niños del fuego en la orilla de la luz. La niña solloza (ruido medio cada pocos segundos) y su luz tiembla. |

Los latidos se aceleran con el pánico y con la cercanía de la bruja.

### 6.6 Esconderse

Matorrales de bugambilia, lápidas grandes, nichos abiertos y la mesa del jacal. Escondida no haces ruido ni hueles. La bruja solo te encuentra si te vio entrar. Cuando busca a pie, revisa el escondite más cercano al ruido. Bajo los árboles tampoco te ve desde el aire.

### 6.7 Poderes

Uno por recuerdo, en este orden:

| Recuerdo | Dónde | Poder |
| --- | --- | --- |
| 1. Flores | Tumba de la abuela | Controlar su luz: prender, apagar y destello |
| 2. Listón | Jacal de la bruja | Encender veladoras apagadas |
| 3. Muñeca | El sendero | Mover objetos a distancia, como poltergeist |
| 4. Zapato | La barranca | Desvanecerse unos segundos |
| 5. Medalla | Panteón viejo | Manifestarse ante los vivos |

**Mover objetos.** Solo objetos visibles bajo su luz o la de una veladora, a 12 m o menos, una vez cada uno. Cada objeto suena distinto y a distinto alcance (botella, lata, maceta, campanita, veladora). Si la bruja está en el suelo, va a revisar el ruido: sirve de señuelo. Volando no lo oye.

**Desvanecerse.** Durante 3 segundos la niña no se ve, no se oye ni se huele. Si la bruja la perseguía, pierde el rastro.

**Manifestarse.** Solo funciona ante los vivos, en el final.

### 6.8 La bruja

Es alta, de más de dos metros, y flaca hasta el hueso. Su cuerpo no es simétrico: un hombro más alto que el otro, un brazo que casi arrastra, dedos larguísimos y la cabeza ladeada. Pelo largo y negro, rebozo roto y descalza. Casi no parece humana. Se mueve a tirones: se queda quieta, tiembla y de pronto se lanza. Esa noche hace un año tomó la cara de la abuela, pero esta noche no se disfraza.

**Volando** es una bola de fuego. Recorre el cielo del cerro e ilumina el suelo debajo de ella. Ve la luz de la niña desde lejos y no oye nada. Si pasa encima y estás a cielo abierto, su propio fuego te descubre.

**En el suelo** es el monstruo. Oye tus pasos, te huele de cerca y a oscuras casi no ve. Puede imitar voces: llama a Tiffany con la voz de su mamá.

| Estado | Qué hace | Cómo lo notas |
| --- | --- | --- |
| Vuelo | Recorre el cielo como bola de fuego. | Ves la bola y la luz que proyecta. Tyler mira arriba y gime. |
| Aterriza | Vio tu luz. Baja cerca de donde la vio y su fuego se apaga. | La bola cae detrás de los árboles y desaparece. Ya no sabes dónde está. |
| Acecho | A pie y a oscuras, va hacia el último ruido. Se detiene, escucha y olfatea. | Pasos irregulares, una respiración y la voz de tu mamá. Tyler gruñe. |
| Aviso | Te detectó a menos de 10 m. Se encoge antes de lanzarse. | Un crujido de huesos. Tienes 0.6 s para reaccionar. |
| Embestida | Se lanza en línea recta hacia donde estabas. No puede girar. | Un golpe de aire. Si cambiaste de dirección, pasa de largo. |
| Recuperación | Se frena y se reincorpora. | Tu oportunidad de abrir distancia o esconderte. |
| Despega | Pasaron 8 s sin percibirte. Su fuego se enciende y vuelve al cielo. | La bola sube. Respiras. |
| Te atrapa | Pierdes el intento. | Un destello de fuego y la pantalla se apaga. |

La persecución es un baile de embestidas. Corriendo eres más rápida que ella cuando camina. Ella te alcanza con embestidas, y la única forma de seguir huyendo es cambiar de dirección cuando oyes el aviso. Siempre avisa antes de lanzarse.

| | Velocidad |
| --- | --- |
| Bruja volando | 6 m/s |
| Bruja en acecho | 1.2 m/s |
| Bruja siguiéndote entre embestidas | 2.5 m/s |
| Embestida (1.2 s, unos 10 m) | 9 m/s |
| Niña caminando | 2.2 m/s |
| Niña corriendo | 4.0 m/s |

### 6.9 Tormenta

La lluvia apaga el fuego de la bruja y la obliga a ir a pie. También tapa tu ruido y el suyo, y apaga las veladoras que enciende la niña. Un relámpago muestra todo durante 1 segundo, a ella y a ti. El trueno tapa cualquier ruido. En la barranca hay un relámpago guionado que la muestra justo detrás de ti.

### 6.10 Tyler

Te guía hacia el siguiente recuerdo si te quedas quieta unos segundos. Su pelo blanco se ve en la oscuridad. Los perros ven a las brujas: Tyler mira al cielo y gime cuando ella vuela cerca, y gruñe cuando está en el suelo a menos de 20 m, aunque no se vea. Es un alma: la bruja no lo ve ni lo oye. Cerca de él el pánico baja.

### 6.11 Recuerdos

Cada recuerdo tiene dos partes:

- **El eco.** De 10 a 15 segundos de esa noche en el mismo lugar, con el mundo desaturado. La niña viva camina de la mano de su abuela. En cada eco algo de la abuela está un poco más mal: la mano demasiado larga, la cabeza ladeada.
- **Los dibujos.** De 3 a 4 dibujos de niña, con crayón en papel viejo y una frase en letra infantil. Son unos 15 en total y se pueden volver a ver en el cuaderno.

## 7. Victoria y derrota

- **Victoria.** Con los cinco recuerdos, manifestarte ante tu familia en la capilla y guiarla hasta el panteón viejo.
- **Derrota.** La bruja te atrapa. Vuelves a la última veladora de la familia que tocaste, con la energía a la mitad. Los recuerdos ganados se conservan.

## 8. Capítulos

Se sube el cerro de abajo hacia arriba.

| Capítulo | Zona | Recuerdo | La bruja | Momento clave |
| --- | --- | --- | --- | --- |
| Prólogo | Pie del cerro y panteón del pueblo | | Una bola de fuego cruza el cielo a lo lejos. | Aprendes a moverte a oscuras. Encuentras la tumba con tu foto. |
| 1 | Tumba de la abuela | Flores | Vuela lejos y gira hacia tu luz. | Primera luz. Aprendes a apagarla cuando ella pasa. |
| 2 | Jacal de la bruja | Listón | Vuela y regresa a su jacal. | Entras mientras ella vuela. La ves aterrizar por primera vez. |
| 3 | El sendero | Muñeca | Aterriza al ver tu luz y te busca a pie. | Primer acecho. Aprendes a usar señuelos. |
| 4 | La barranca | Zapato | La tormenta la deja a pie. Embestidas. | Persecución. El relámpago la muestra detrás de ti. |
| 5 | Panteón viejo | Medalla | Fuegos falsos en el cielo. Solo uno es ella. | Descubres dónde está tu cuerpo. |
| Final | Capilla de la cima | | Rodea la capilla volando y baja alrededor. | Te manifiestas ante tu familia y la guías. |

## 9. Arte y ambientación

Noche cerrada con luna visible. El cerro se basa en el Cerro de Bugambilias (Jalisco), con fotos y grabaciones tomadas en el lugar: pinos, encinos, bugambilias, grava y hojarasca. Realismo estilizado, personajes con proporciones normales y poca visibilidad: solo se ve con la luz de la niña, las veladoras, el fuego de la bruja y los relámpagos.

La bruja rompe esas proporciones a propósito. Es lo único del juego que se ve mal hecho, demasiado largo y torcido.

La cámara va en tercera persona, alta y en diagonal, siguiendo a la niña. La luz de la niña cae plana sobre el suelo, como una linterna de mano, nunca como un haz en el aire.

Los recuerdos cambian de registro: el mundo se desatura y los dibujos son de crayón sobre papel viejo, hechos a mano en Procreate.

## 10. Audio

- **Ambiente.** Domina el cerro: grillos, viento, hojas, animales lejanos y silencios. Grabaciones propias del Cerro de Bugambilias y sonidos con licencia CC0.
- **La bruja.** El fuego que crepita y zumba al volar, el golpe al aterrizar, sus pasos irregulares, el crujido de huesos antes de cada embestida y la voz que imita. Una risa, muy rara vez.
- **Tyler.** Gemidos, gruñidos y ladridos, grabados del perro real.
- **Tensión.** Latidos que se aceleran con el pánico y con la cercanía de la bruja. Música de tensión mínima en el acecho y la persecución.
- **Susurros.** Los niños del fuego dicen "Tiffany" cuando la bruja está cerca.

## 11. Interfaz

Casi sin interfaz. La energía se ve en la luz de la niña y el ruido se oye. Hay un aviso corto al ganar cada poder, el cuaderno de recuerdos con los dibujos, la pausa y subtítulos para las frases de los dibujos y los ecos.

## 12. Técnica

- Unity `6000.3.27f1`, URP y build WebGL para itch.io. Meta: 60 fps en un navegador de laptop.
- Input System con teclado, ratón y control.
- Sin materiales ni shaders creados en tiempo de ejecución, porque WebGL quita las variantes que no ve en el build.
- La lógica (ruido, energía, pánico, estados de la bruja) vive en clases de C# probadas con el Unity Test Framework. Los componentes de Unity solo la conectan con la escena.
- La bruja vuela por puntos en el aire y camina en el suelo con NavMesh.
- El contenido de los recuerdos (frases, dibujos, ecos) vive en archivos de datos para editarlo sin tocar código.

## 13. Alcance y prioridades

Pesa más una mecánica pulida que muchos sistemas. El orden de trabajo es:

1. **Núcleo.** Moverse, ruido por suelo, la bruja con sus dos formas y sus embestidas, luz y energía, veladoras de la familia, atrapar y regresar. Una zona jugable de punta a punta, en el navegador. Se afina hasta que dé miedo antes de sumar lo demás.
2. **Progresión.** Los cinco poderes, los cinco recuerdos con eco y dibujos, los capítulos con la bruja cada vez más peligrosa, Tyler y el pánico.
3. **Atmósfera y final.** Tormenta, alucinaciones, voces imitadas, el final con la familia y pulido.

Si el tiempo no alcanza, se recorta en este orden: la tormenta queda solo en la barranca, se quitan los fuegos falsos, se quitan las alucinaciones de niños y el final se vuelve una escena sin control.

## 14. Equipo y uso de IA

- Eliud Sosa: dirección, diseño de juego y programación.
- Clau (clauperspectiva): dirección de arte y narrativa de los recuerdos, dibujos.

El uso de inteligencia artificial está declarado por área en [USO-DE-IA.md](../USO-DE-IA.md). Los créditos de recursos externos están en [CREDITS.md](../CREDITS.md).
