using System;

namespace TiffanyGame
{
    public static class InterfaceTexts
    {
        public const string Title = "Tiffany";
        public const string StartHint = "Clic, Espacio u Options para empezar";
        public const string Continue = "Continuar";
        public const string NewGame = "Nueva partida";
        public const string Controls = "WASD / stick izquierdo  moverte\n"
            + "Ratón / stick derecho  apuntar la luz\n"
            + "Shift / L3 / R2  correr\n"
            + "Ctrl / L2  despacio\n"
            + "Clic / Triángulo  prender la luz\n"
            + "mantén clic / △ / F  destello\n"
            + "Q / Cuadrado  encender veladora\n"
            + "E / Círculo  mover objeto\n"
            + "R / R1  desvanecerte\n"
            + "Espacio / Equis  esconderte y recoger\n"
            + "Tab / Create  cuaderno\n"
            + "M / L1  mapa\n"
            + "Esc o P / Options  pausa";
        public const string PauseTitle = "Pausa";
        public const string PauseActions = "Moverte\nApuntar la luz\nCorrer\nDespacio\n"
            + "Luz (toque) / destello (mantén)\nEncender veladora\nMover objeto\nDesvanecerte\n"
            + "Esconderte y recoger\nCuaderno\nMapa\nPausa";
        public const string PauseKeyboard = "WASD\nRatón\nShift\nCtrl\nClic / F\nQ\nE\nR\nEspacio\nTab\nM\nEsc / P";
        public const string PauseController = "Stick izq.\nStick der.\nL3 / R2\nL2\n△\n□\n○\nR1\n✕\nCreate\nL1\nOptions";
        public const string PauseHint = "W/S o cruceta: elegir · Espacio / ✕: aceptar · Esc / Options: seguir";
        public const string PauseResume = "Seguir";
        public const string PauseRespawn = "Volver a la última veladora";
        public const string PauseFullscreen = "Pantalla completa";
        public const string PauseRestart = "Reiniciar partida";
        public const string PauseConfirmRestart = "¿Reiniciar? Se pierde la partida";
        public const string NotebookEmpty = "Aún no recuerdas nada";
        public const string NotebookHint = "A / D o cruceta para pasar de página · Tab o Create para cerrar";
        public const string MemoryHint = "Espacio / Cruz para continuar";
        public const float PowerNoticeSeconds = 7f;
        public const string SoulEnergy = "Energía del alma";
        public const string RouteReminder = "Los pájaros amarillos marcan el camino.";
        public const string StoryHint = "Clic, Enter u Options para seguir";
        public const string GraveObjective = "Toca tu lápida (" + GameInput.InteractBinding + ")";
        public const string BallObjective = "Busca la pelota";
        public const string TylerNotice = "Tyler te sigue. Si te detienes, te lleva al siguiente recuerdo.\n"
            + "Gruñe cuando la bruja anda cerca. Junto a él tienes menos miedo y te presta su luz.\n"
            + "M / L1: abre el mapa del camino.";
        public const string BirdsObjective = "Sigue a los pájaros amarillos · M / L1: mapa";
        public const string TouchPrompt = GameInput.InteractBinding + ": recordar";
        public const string CollectPrompt = GameInput.InteractBinding + ": recoger";
        public const string HidePrompt = GameInput.InteractBinding + ": esconderte";
        public const string LeavePrompt = GameInput.InteractBinding + ": salir";
        public const string KindlePrompt = GameInput.KindleBinding + ": encender";
        public const string RechargePrompt = "Recargar energía (automático)";

        public static string StoryLine(int index) => index switch
        {
            0 => "Hace tres meses, Tiffany siguió el ladrido de su perro hasta el bosque. Nunca volvió.",
            1 => "Esta noche, Día de Muertos, su familia le encendió una veladora.",
            2 => "Su alma despertó. La bruja también.",
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public static string ControllerText(string text, bool hasSymbols) => hasSymbols ? text :
            text.Replace("△", "Triángulo").Replace("□", "Cuadrado").Replace("○", "Círculo").Replace("✕", "Equis");

        // Provisional: lo aprueba Eliud.
        public const string EndingStory = "Su familia la vio una última vez. Tiffany pudo descansar.";
        public const string EndingCredits = "Gracias por jugar\n\nEliud Sosa y Claudia.\n\nTyler, el perro del juego, es un perro real.";

        public static string PowerNotice(Power power)
        {
            switch (power)
            {
                case Power.Light: return "Clic / Triángulo: prende o apaga tu luz.\n"
                    + "Mantén clic / Triángulo / F durante 3 segundos: destello de 360° para cegar a la bruja en el suelo y escapar.";
                case Power.Candles: return "Q / Cuadrado: enciende una veladora apagada cercana.\n"
                    + "Su luz te protege de la bruja por un tiempo.";
                case Power.Poltergeist: return "E / Círculo: mueve un objeto iluminado a 12 m o menos.\n"
                    + "El ruido distrae a la bruja cuando está en el suelo.";
                case Power.Vanish: return "R / R1: desvanécete durante 3 segundos.\n"
                    + "La bruja no te ve, oye ni huele y pierde tu rastro.";
                case Power.Manifest: return "Espacio / Equis: manifiéstate ante tu familia en tu casa.\n"
                    + "Solo puedes hacerlo ante los vivos, al final.";
                default: throw new ArgumentOutOfRangeException(nameof(power));
            }
        }
    }
}
