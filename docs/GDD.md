# Tiffany · Documento de diseño

| | |
| --- | --- |
| Género | Terror de sigilo en tercera persona |
| Plataforma | Navegador (WebGL), en itch.io |
| Motor | Unity 6.3 LTS (`6000.3.27f1`) con URP |
| Duración prevista | Unos 25 minutos |
| Controles | Teclado y ratón, o mando |
| Jam | Spooky Game Jam de Hybridge, 26 al 30 de octubre de 2026 |

## 1. Resumen

El jugador controla el alma de Tiffany. Ella despierta de noche en el cementerio, sin recuerdos.
Su familia la llama con una veladora en el altar de casa.
Tiffany recupera cinco recuerdos y vuelve a casa para que su alma quede libre.

Para avanzar, el jugador decide cuándo usar la luz, cuánto ruido hacer y dónde esconder a Tiffany.
Ella gasta energía del alma en luz y poderes para escapar de la bruja.
Se refugia junto a veladoras y cuenta con la compañía de Tyler.
Interviene en el mundo humano al encender veladoras y mover objetos.

## 2. Cómo cumple el reto

| Requisito | Cómo lo cumple Tiffany |
| --- | --- |
| Protagonista sobrenatural | El jugador controla el alma de una niña que regresa en Día de Muertos. |
| Llega al mundo humano con un propósito | La veladora de su familia llama a Tiffany. Debe recuperar cinco recuerdos y volver a casa para liberar su alma. |
| Interacción con el mundo humano | Tiffany enciende veladoras temporales y mueve objetos para distraer a la bruja. |
| Gestión de recursos | La energía del alma se gasta en luz, destellos y poderes. Se recupera junto a las veladoras familiares. |
| Victoria y derrota | El jugador gana al interactuar junto a la veladora de casa con el poder de manifestarse. Si la bruja atrapa a Tiffany, ella vuelve a la última veladora familiar con 50 de energía. Conserva sus recuerdos y poderes. |

El sigilo exige decidir cuándo mostrar la luz, correr, caminar despacio o gastar energía en un poder.

## 3. Historia

Guion completo: [recuerdos.md](recuerdos.md).

Tres meses antes, Tiffany fue con Tyler a llevar flores a la tumba de su abuela.
Se hizo de noche. Tyler le ladró al bosque y salió corriendo. Tiffany fue detrás de él.
Siguió oyendo su ladrido, pero la bruja ya se había comido a Tyler y lo imitaba.
Tiffany pasó por la planta de agua, las vías y el establo hasta llegar a la choza de la bruja.
Allí la bruja la atrapó. Nunca encontraron sus restos.

En Día de Muertos, su familia enciende una veladora y llama a su alma.
Tiffany despierta sin saber que está muerta. Su lápida le permite entenderlo.
Al encontrar la pelota aparece Tyler, que la acompaña y comparte su luz.
La bruja sabe que el alma de Tiffany se le escapó y sale a buscarla.

## 4. Recorrido y recuerdos

Tiffany recorre ocho claros.

| Orden | Claro | Recuerdo del guion | Poder asociado |
| --- | --- | --- | --- |
| 1 | Cementerio | 1. Lápida | Tiffany controla su luz y usa el destello. |
| 2 | Planta de agua | 2. Listón | Enciende veladoras. |
| 3 | Vías del tren | 3. Muñeca | Mueve objetos. |
| 4 | Establo | 4. Zapato | Se desvanece. |
| 5 | Jacal (choza) de la bruja | 5. Brazalete | Se manifiesta ante los vivos. |
| 6 | Lago | | Continúa el regreso. |
| 7 | Carretera | | Continúa el regreso. |
| 8 | Casa de Tiffany | | Llega a la veladora del final. |

En el cementerio Tiffany también recoge la pelota. Con ella aparece Tyler.
Al recoger un recuerdo, el jugador ve un eco de esa noche y recibe el aviso de su poder.
Durante el eco ve el mundo sin color y escenas del pasado en el mismo lugar.
Cada recuerdo incluye además tres dibujos de niña, cada uno con una frase.
El jugador puede volver a ver los dibujos y sus frases en el cuaderno (Tab).
Tiffany puede usar la luz desde el inicio. Los otros poderes se habilitan con su recuerdo.
Con el quinto recuerdo se inician el regreso y la tormenta.

## 5. Bucle de juego

1. Tiffany explora el camino y busca el siguiente recuerdo.
2. El jugador escucha los pasos y vigila la forma de la bruja.
3. Elige entre avanzar despacio, correr, esconder a Tiffany o gastar energía.
4. Tiffany se acerca a una veladora familiar para recargarse y fijar el regreso.
5. Recoge recuerdos y usa sus poderes para superar los pasos del recorrido.
6. Tras el brazalete, cruza el lago y la carretera hacia casa.

Antes de recoger el brazalete, Tiffany necesita completar los cuatro retos del camino.
En el último combina encender veladoras, distraer a la bruja y desvanecerse.

## 6. Controles

| Acción | Teclado y ratón | DualSense / gamepad |
| --- | --- | --- |
| Movimiento | WASD o flechas | Stick izquierdo |
| Paso lento | Ctrl, mantenido | L2, gatillo izquierdo, mantenido |
| Carrera | Shift, mantenido | L3, stick izquierdo presionado y mantenido |
| Dirección de la luz | Ratón | Stick derecho |
| Encendido o apagado de la luz | F o clic derecho | Triángulo (norte) |
| Destello | Clic izquierdo | R2, gatillo derecho |
| Encendido de veladoras | Q | Cuadrado (oeste) |
| Movimiento de objetos | E | Círculo (este) |
| Desvanecimiento | R | R1, hombro derecho |
| Interacción: recoger, esconderse, salir o activar el final | Espacio | Cruz (sur) |
| Cuaderno | Tab | Create / Select |
| Cambio de página | A / D o flechas izquierda / derecha | Cruceta izquierda / derecha |
| Pausa o continuación | Esc o P | Options / Start |
| Inicio desde el título | Clic izquierdo, Espacio o Enter | Options / Start o cruz (sur) |
| Selección de opción del título | W / S o flechas arriba / abajo | Stick izquierdo o cruceta arriba / abajo |

Al correr, Tiffany orienta el cuerpo hacia el movimiento.
El jugador apunta con el ratón o el stick derecho cuando ella camina o va despacio.

## 7. Energía del alma y veladoras

Tiffany comienza con 100 de energía. Esa es también su capacidad máxima.

| Acción | Consumo de energía |
| --- | --- |
| Luz encendida | 1 por segundo |
| Destello | 50 |
| Encendido de una veladora | 20 |
| Movimiento de un objeto | 15 |
| Desvanecimiento | 30 |

Tiffany recupera 20 por segundo al estar junto a una veladora familiar.
Esa veladora permanece encendida incluso con lluvia y fija su punto de regreso.
Dentro de una zona segura de veladora, Tiffany queda protegida de la bruja.

Las veladoras que Tiffany enciende duran 90 segundos y también forman zonas seguras.
Con ellas abre pasos bloqueados por la oscuridad.

El jugador lee la energía en el alcance y el parpadeo de la luz de Tiffany.
Al agotarla se apaga su luz. Ella puede seguir moviéndose y buscar una veladora.
Si Tyler está cerca, le comparte su luz cuando ella se queda sin energía.

## 8. Luz y poderes

Tiffany alumbra el suelo hacia donde apunta. La luz le permite ver, pero la expone.
El destello aturde a la bruja durante 2 segundos si está en el suelo,
dentro del cono de luz y sin un obstáculo que bloquee el destello.
Tiffany puede interrumpir su aviso de embestida para ganar tiempo y escapar.

Tiffany enciende la veladora apagada más cercana, hasta a 2 metros.
Necesita el poder del listón, energía suficiente y que no esté lloviendo.

Mueve objetos habilitados hasta a 12 metros para producir ruido y distraer a la bruja.
Necesita que estén bajo su luz o en una zona iluminada por veladora.
Cada objeto queda usado al moverlo. En los retos el señuelo se recupera al reintentar.

Al desvanecerse, Tiffany deja de ser detectable durante 3 segundos.
Corta el seguimiento de la bruja y puede cruzar los pasos que vigila.

## 9. Ruido y escondites

Los pasos de Tiffany producen ruido según el suelo y cómo camina.
La bruja lo escucha cuando está en el suelo.

| Suelo | Alcance de los pasos al caminar |
| --- | --- |
| Tierra | 2 metros |
| Zacate | 4 metros |
| Losas | 4 metros |
| Grava | 8 metros |
| Hojarasca | 14 metros |
| Ramas secas | 20 metros |

Al correr se duplica ese alcance. Al caminar despacio se reduce a la mitad.
Con lluvia también se reduce a la mitad.
Tiffany se esconde en los puntos disponibles junto a lápidas, arbustos y cajas.
Al entrar deja de moverse y apaga su luz. El jugador interactúa otra vez para que salga.
La bruja puede encontrarla si la vio entrar.
También puede revisar un escondite cercano al ruido que escuchó.
La cobertura sobre Tiffany evita que la bruja la vea desde el aire.

## 10. La bruja

En vuelo, la bruja es una bola de fuego. Puede detectar la luz de Tiffany a distancia.
Si pasa cerca y Tiffany está descubierta, puede verla aunque ella apague la luz.
En el suelo la detecta por vista, oído y olfato.
Desde las vías se oye cómo imita el ladrido de Tyler para atraerla.

| Estado | Qué ocurre |
| --- | --- |
| Vuelo | La bruja patrulla el cielo y se acerca a la luz de Tiffany. |
| Sospecha | Se detiene para comprobar una señal de luz o ruido. |
| Aterrizaje | Baja y pierde el fuego para buscar a Tiffany a pie. |
| Acecho | La busca, sigue señales y puede revisar escondites. |
| Aviso | Tiffany tiene 0.8 segundos antes de que la bruja lance una embestida. |
| Embestida | Tiffany esquiva una carrera recta que dura hasta 1.2 segundos. |
| Recuperación | La bruja hace una pausa después de la embestida. Tiffany puede aprovecharla para alejarse. |
| Aturdimiento | El destello detiene a la bruja durante 2 segundos. |
| Despegue | La bruja vuelve al cielo al perder el rastro de Tiffany, fuera del regreso. |
| Captura | El intento termina y Tiffany vuelve al punto de regreso. |

En el cementerio la bruja solo vuela.
En la planta de agua ya puede aterrizar. Desde el establo puede embestir a Tiffany.
Al avanzar aparecen búsquedas, vigilancia cerca de veladoras y emboscadas.
Durante el regreso la persigue a pie y no vuelve al cielo.

## 11. Pánico, compañía y ambiente

El recorrido transcurre en una noche oscura, con luna y niebla.
El pánico de Tiffany sube en la oscuridad, cerca de la bruja y cuando esta usa el ladrido como señuelo.
Baja junto a veladoras, al quedarse quieta en un escondite y con Tyler cerca.
Al llegar al 50 %, el jugador oye pasos falsos. Desde el 75 % también oye susurros.
Desde el 90 %, la luz de Tiffany tiembla y ella solloza, produciendo ruido fuera del escondite.
El latido suena con más fuerza y rapidez al subir el pánico o acercarse la bruja a pie.
Tyler sigue a Tiffany. Si ella se detiene unos segundos y él no reacciona a una amenaza,
la guía hacia el siguiente recuerdo pendiente.
Le advierte de la bruja con sus reacciones y gruñe al detectarla cerca en el suelo.

## 12. Tormenta y regreso

La tormenta solo aparece después del quinto recuerdo, durante el regreso.
Tiffany cruza el lago y la carretera mientras la bruja la persigue a pie.
Los relámpagos iluminan el entorno durante un segundo.
Durante el trueno, Tiffany puede moverse sin que la bruja escuche sus ruidos.
La lluvia apaga las veladoras temporales e impide que Tiffany vuelva a encenderlas.
Las veladoras familiares le permiten refugiarse y recargarse.

## 13. Victoria y derrota

El jugador gana cuando Tiffany llega a la veladora de casa con el poder de manifestarse e interactúa
a menos de 2.5 metros, sin gastar energía. Aparecen un fundido, el cierre y los créditos.
En la historia, Tiffany se muestra ante su familia y su alma queda libre.
El intento se pierde si la bruja la atrapa. Aparecen fuego y un fundido antes del regreso.
Tiffany vuelve junto a la última veladora familiar que tocó, con 50 de energía.
Conserva los recuerdos y los poderes que obtuvo.

## 14. Equipo

Eliud Sosa y Claudia.

Uso de IA: [USO-DE-IA.md](../USO-DE-IA.md). Créditos: [CREDITS.md](../CREDITS.md).
