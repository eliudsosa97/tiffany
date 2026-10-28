# Tiffany · Documento de diseño

| | |
| --- | --- |
| Género | Terror de sigilo en tercera persona |
| Plataforma | Navegador (WebGL), en itch.io |
| Motor | Unity 6.3 LTS (`6000.3.27f1`) con URP |
| Duración | Unos 25 minutos |
| Controles | Teclado y ratón, o control |
| Jam | Spooky Game Jam de Hybridge, 26 al 30 de octubre de 2026 |

## 1. Resumen

Es Día de Muertos. Tiffany, una niña de nueve años, despierta de noche en un cerro y no sabe que está muerta. Hace un año subió sola a llevarle flores a su abuela, y en la tumba la esperaba su abuela. No era ella. Era la bruja del cerro, y se la llevó. La bruja vive de las almas de los niños y las guarda en su fuego: las bolas de fuego que vuelan sobre el cerro son ellos. Esta noche la familia de Tiffany le puso su primera ofrenda, el llamado la sacó del fuego y la bruja sale a buscarla.

Tiffany sube el cerro a oscuras con Tyler, su perro, y recupera cinco recuerdos de esa noche. Cada uno le da un poder. Con el último sabe dónde quedó su cuerpo. En la capilla de la cima se manifiesta ante su familia y la guía hasta él. Cuando lo encuentran, las almas del fuego quedan libres y la bruja se consume.

A la bruja no se le puede dañar. Solo queda esconderse. Volando, como bola de fuego, ve tu luz desde muy lejos. En el suelo te oye, te huele y se lanza sobre ti. Todo el juego es leer en qué forma está y cambiar de plan.

## 2. Cómo cumple el reto

| Requisito | En Tiffany |
| --- | --- |
| Protagonista sobrenatural | Juegas como el alma de una niña que regresa en Día de Muertos. |
| Llega al mundo humano con un propósito | La llama la ofrenda de su familia. Viene a encontrar su cuerpo y a liberar a los niños del fuego. |
| Interacción con el mundo humano | Se recarga en las veladoras de su familia, mueve objetos de los vivos para distraer a la bruja y al final se manifiesta ante su familia. |
| Gestión de recursos | La energía del alma se gasta en luz y poderes, y solo se recarga en las veladoras. |
| Victoria y derrota | Ganas al llevar a tu familia hasta los cuerpos. Si la bruja te atrapa, vuelves a la última veladora. |
| Mecánica principal con decisiones | Mostrar o esconder tu luz, correr o pisar despacio, esquivar una embestida o gastar energía en un poder. |

## 3. Historia

### Hace un año

Tiffany subió sola al cerro en Día de Muertos a llevarle flores a la tumba de su abuela. Tyler, su perro, la siguió. En la tumba la esperaba su abuela, que le ofreció acompañarla de regreso porque ya estaba oscuro. No era su abuela. La bruja se llevó a la niña y a Tyler, y escondió los cuerpos en el panteón viejo, más arriba. El alma de Tiffany quedó en su fuego, con las de otros niños.

Su familia la buscó durante meses. En el cerro siguen los carteles de SE BUSCA y las veladoras que dejaron.

### Esta noche

Este año la familia le pone su primera ofrenda, en la capilla de la cima. El llamado la saca del fuego. Despierta sin memoria: se le quedó en el fuego. A los dos minutos lo descubre. En la tumba de su abuela, junto a la foto de la abuela, está la suya.

La bruja sabe que se le escapó y sale a buscarla. Cuando está cerca, los niños del fuego susurran "Tiffany".

### El final

Con los cinco recuerdos, Tiffany sabe dónde está su cuerpo. Sube a la capilla, donde su familia vela la ofrenda, y se manifiesta ante ellos. La siguen con sus linternas hasta el panteón viejo mientras la bruja ronda. Cuando encuentran los cuerpos, las almas del fuego quedan libres y la bruja se consume.

### Personajes

- **Tiffany.** El alma de una niña de nueve años. Brilla tenue. No ataca: se esconde, distrae y se manifiesta.
- **La bruja.** La única enemiga. No se le puede dañar. Ver 6.8.
- **Tyler.** Su perro, un jack russell terrier. Murió esa noche con ella y ahora es un alma que la guía y le ladra a la bruja. Su pelo blanco se ve en la oscuridad.
- **La familia.** Los vivos. Pusieron las veladoras y la ofrenda. Aparecen al final, en la capilla.
- **Los niños del fuego.** Las otras almas. Se oyen como susurros y se ven como alucinaciones cuando sube el pánico.

## 4. Bucle de juego

1. Avanzas a oscuras hacia el siguiente recuerdo, mirando el cielo y escuchando.
2. Decides cuándo alumbrar, por dónde pisar y cuándo esconderte, según la forma de la bruja.
3. Gastas energía en luz y poderes para pasar cerca de ella.
4. Llegas a una veladora: te recargas y ahí vuelves si te atrapa.
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

Cada poder se activa al ganarlo.

## 6. Mecánicas

### 6.1 Ruido

Cada paso suena según el suelo. Si la bruja está en el suelo, lo oye dentro de un radio. Volando no oye. No hay indicador: se escucha.

| Suelo | Ruido | Radio caminando |
| --- | --- | --- |
| Tierra | Muy bajo | 2 m |
| Zacate | Bajo | 4 m |
| Losas del panteón | Bajo, con eco | 4 m |
| Grava | Medio | 8 m |
| Hojarasca | Alto | 14 m |
| Ramas secas | Muy alto | 20 m |

Correr duplica el radio y caminar despacio lo reduce a la mitad. La lluvia tapa el ruido y el trueno tapa todo mientras suena.

### 6.2 Energía del alma

Es el recurso del juego. Empieza en 100.

| Uso | Costo |
| --- | --- |
| Luz encendida | 1 por segundo |
| Destello | 50 |
| Encender veladora | 20 |
| Mover un objeto | 15 |
| Desvanecerse | 30 |

Solo se recarga junto a una veladora de la familia, a 20 por segundo. En cero no hay luz ni poderes: toca caminar a oscuras hasta la siguiente veladora. No hay barra. La luz de Tiffany se achica y parpadea cuando queda poca.

### 6.3 Luz

Tiffany junta las manos como si cargara una vela, y de ahí sale su luz: un cono cálido sobre el suelo, hacia donde apunta. Con ella ves el camino, los objetos y los pájaros amarillos que lo marcan. Si la bruja vuela, la ve desde muy lejos.

El destello ciega a la bruja 2 segundos si está en el suelo y dentro del cono, y corta una embestida durante el aviso. Sirve para escapar, no para ganarle.

### 6.4 Veladoras

- **De la familia.** En vasos de vidrio, nunca se apagan. Recargan energía, son zona segura (la bruja no entra a su luz ni aterriza en ella) y son el punto de regreso.
- **Las que enciende Tiffany** (poder 2). Son zona segura durante 90 segundos, la mitad si llueve.

### 6.5 Pánico

Sube a oscuras, cerca de la bruja, al oír los susurros o la voz que imita, y cuando un relámpago la muestra. Baja junto a una veladora, escondida y quieta, o con Tyler cerca. A oscuras sube más rápido.

| Pánico | Efecto |
| --- | --- |
| 50 % | Pasos que no existen |
| 75 % | Fuegos falsos en el cielo y susurros |
| 90 % | Los niños del fuego aparecen en la orilla de la luz. Tiffany solloza (ruido medio cada pocos segundos) y su luz tiembla. |

Los latidos se aceleran con el pánico y con la bruja cerca.

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

**Mover objetos.** Solo objetos bajo su luz o la de una veladora, a 12 m o menos, una vez cada uno. Cada objeto suena distinto y llega a distinta distancia (botella, lata, maceta, campanita, veladora). Si la bruja está en el suelo, va a revisar: sirve de señuelo. Volando no lo oye.

**Desvanecerse.** Durante 3 segundos no te ve, no te oye ni te huele. Si te perseguía, pierde el rastro.

**Manifestarse.** Solo funciona ante los vivos, al final.

### 6.8 La bruja

Mide más de dos metros y es flaca hasta el hueso. Su cuerpo está chueco: un hombro más alto que el otro, un brazo que casi arrastra, dedos larguísimos y la cabeza ladeada. Pelo largo y negro, rebozo roto, descalza. Casi no parece humana. Se mueve a tirones: se queda quieta, tiembla y de pronto se lanza. Hace un año tomó la cara de la abuela. Esta noche no se disfraza.

**Volando** es una bola de fuego que recorre el cielo e ilumina el suelo debajo. Ve tu luz desde lejos y no oye nada. Si pasa encima y estás a cielo abierto, su propio fuego te descubre.

**En el suelo** es el monstruo. Oye tus pasos, te huele de cerca y a oscuras casi no ve. Imita voces: llama a Tiffany con la voz de su mamá.

| Estado | Qué hace | Cómo lo notas |
| --- | --- | --- |
| Vuelo | Recorre el cielo como bola de fuego. | Ves la bola y su luz en el suelo. Tyler mira arriba y gime. |
| Aterriza | Vio tu luz. Baja cerca y su fuego se apaga. | La bola cae detrás de los árboles. Ya no sabes dónde está. |
| Acecho | Camina a oscuras hacia el último ruido. Se detiene, escucha y olfatea. | Pasos irregulares, una respiración y la voz de tu mamá. Tyler gruñe. |
| Aviso | Te detectó a menos de 10 m. Se encoge para lanzarse. | Un crujido de huesos. Tienes 0.6 s. |
| Embestida | Se lanza recto hacia donde estabas. No puede girar. | Un golpe de aire. Si cambiaste de dirección, pasa de largo. |
| Recuperación | Frena y se reincorpora. | Tu momento para alejarte o esconderte. |
| Despega | Lleva 8 s sin percibirte. Prende su fuego y vuelve al cielo. | La bola sube. Respiras. |
| Te atrapa | Pierdes el intento. | Un destello de fuego y la pantalla se apaga. |

La persecución es un baile de embestidas. Corriendo eres más rápida que ella cuando camina, pero sus embestidas te alcanzan. Para seguir huyendo hay que cambiar de dirección al oír el aviso. Siempre avisa.

| | Velocidad |
| --- | --- |
| Bruja volando | 6 m/s |
| Bruja en acecho | 1.2 m/s |
| Bruja siguiéndote entre embestidas | 2.5 m/s |
| Embestida (1.2 s, unos 10 m) | 9 m/s |
| Tiffany caminando | 2.2 m/s |
| Tiffany corriendo | 4.0 m/s |

### 6.9 Tormenta

La lluvia apaga el fuego de la bruja y la deja a pie. También tapa tu ruido y el suyo, y apaga las veladoras que enciende Tiffany. Un relámpago muestra todo durante 1 segundo, a ella y a ti. El trueno tapa cualquier ruido. En la barranca hay un relámpago preparado que la muestra justo detrás de ti.

### 6.10 Tyler

Si te quedas quieta unos segundos, te guía al siguiente recuerdo. Su pelo blanco se ve en la oscuridad. Los perros ven a las brujas: Tyler mira al cielo y gime cuando ella vuela cerca, y gruñe cuando está en el suelo a menos de 20 m, aunque no se vea. La bruja no lo ve ni lo oye. Con él cerca, el pánico baja.

### 6.11 Recuerdos

Cada recuerdo tiene dos partes:

- **El eco.** De 10 a 15 segundos de esa noche, en el mismo lugar y con el mundo sin color. La niña viva camina de la mano de su abuela. En cada eco algo de la abuela está más mal: la mano demasiado larga, la cabeza ladeada.
- **Los dibujos.** De 3 a 4 dibujos de niña, en crayón sobre papel viejo, cada uno con una frase. Unos 15 en total. Se pueden volver a ver en el cuaderno.

## 7. Victoria y derrota

- **Victoria.** Con los cinco recuerdos, te manifiestas ante tu familia en la capilla y la guías hasta el panteón viejo.
- **Derrota.** La bruja te atrapa. Vuelves a la última veladora de la familia que tocaste, con la mitad de energía. Los recuerdos se conservan.

## 8. Capítulos

El cerro se sube de abajo hacia arriba.

| Capítulo | Zona | Recuerdo | La bruja | Momento clave |
| --- | --- | --- | --- | --- |
| Prólogo | Pie del cerro y panteón del pueblo | | Una bola de fuego cruza el cielo a lo lejos. | Aprendes a moverte a oscuras. Encuentras la tumba con tu foto. |
| 1 | Tumba de la abuela | Flores | Vuela lejos y gira hacia tu luz. | Primera luz. Aprendes a apagarla cuando pasa. |
| 2 | Jacal de la bruja | Listón | Vuela y regresa a su jacal. | Entras mientras ella vuela. La ves aterrizar por primera vez. |
| 3 | El sendero | Muñeca | Aterriza al ver tu luz y te busca a pie. | Primer acecho. Aprendes a usar señuelos. |
| 4 | La barranca | Zapato | La tormenta la deja a pie. Embestidas. | Persecución. El relámpago la muestra detrás de ti. |
| 5 | Panteón viejo | Medalla | Fuegos falsos en el cielo. Solo uno es ella. | Descubres dónde está tu cuerpo. |
| Final | Capilla de la cima | | Rodea la capilla volando y baja alrededor. | Te manifiestas ante tu familia y la guías. |

## 9. Arte y ambientación

Noche cerrada con luna. El cerro está basado en el Cerro de Bugambilias (Jalisco), con fotos y grabaciones de ahí: pinos, encinos, bugambilias, grava y hojarasca. Realismo estilizado, personajes con proporciones normales y poca visibilidad. Solo se ve con la luz de Tiffany, las veladoras, el fuego de la bruja y los relámpagos.

La bruja es lo único con proporciones rotas: demasiado larga y torcida, como mal hecha.

La cámara va en tercera persona, alta y en diagonal, detrás de Tiffany. Su luz cae plana sobre el suelo, como una linterna, nunca como un haz en el aire.

Los recuerdos cambian de estilo: el mundo pierde el color y los dibujos son de crayón sobre papel viejo, hechos a mano en Procreate.

## 10. Audio

- **Ambiente.** Grillos, viento, hojas, animales lejanos y silencios. Grabado en el Cerro de Bugambilias, más sonidos con licencia CC0.
- **La bruja.** El fuego que crepita al volar, el golpe al aterrizar, sus pasos irregulares, el crujido de huesos antes de cada embestida y la voz que imita. Muy de vez en cuando, una risa.
- **Tyler.** Gemidos, gruñidos y ladridos del perro real.
- **Tensión.** Latidos que se aceleran con el pánico y con la bruja cerca. Música mínima en el acecho y la persecución.
- **Susurros.** Los niños del fuego dicen "Tiffany" cuando la bruja está cerca.

## 11. Interfaz

Casi nada en pantalla. La energía se ve en la luz y el ruido se oye. Hay un aviso corto al ganar cada poder, el cuaderno de recuerdos, la pausa y subtítulos para las frases de los dibujos y los ecos.

## 12. Técnica

- Unity `6000.3.27f1` con URP, build WebGL para itch.io. Meta: 60 fps en una laptop.
- Input System con teclado, ratón y control.
- Materiales y shaders guardados como assets, nunca creados al jugar.
- La lógica (ruido, energía, pánico, la bruja) está en clases de C# con tests. Los componentes de Unity solo la conectan con la escena.
- La bruja vuela por puntos en el aire y camina con NavMesh.
- Frases, dibujos y ecos viven en archivos de datos, fuera del código.

## 13. Alcance y prioridades

Orden de trabajo:

1. **Núcleo.** Moverse, ruido por suelo, la bruja con sus dos formas y sus embestidas, luz y energía, veladoras, atrapar y regresar. Una zona jugable en el navegador, pulida hasta que dé miedo.
2. **Progresión.** Los cinco poderes, los cinco recuerdos, los capítulos, Tyler y el pánico.
3. **Atmósfera y final.** Tormenta, alucinaciones, voces imitadas, el final con la familia y pulido.

Si no da tiempo, se recorta en este orden: la tormenta queda solo en la barranca, se quitan los fuegos falsos, se quitan las alucinaciones de niños y el final se vuelve una escena sin control.

## 14. Equipo y uso de IA

Eliud Sosa y Claudia.

Uso de IA: [USO-DE-IA.md](../USO-DE-IA.md). Créditos: [CREDITS.md](../CREDITS.md).
