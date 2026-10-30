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
            + "Shift / L3  correr · Ctrl / L2  despacio\n"
            + "Ratón / stick derecho  apuntar la luz\n"
            + "F o clic derecho / Triángulo  luz · Clic izquierdo / R2  destello\n"
            + "Q / Cuadrado  veladora · E / Círculo  mover · R / R1  desvanecerte\n"
            + "Espacio / Equis  esconderte y recoger\n"
            + "Tab / Create  cuaderno · Esc o P / Options  pausa";
        public const string PauseTitle = "Pausa";
        public const string PauseHint = "Esc, P u Options para seguir";
        public const string NotebookEmpty = "Aún no recuerdas nada";
        public const string NotebookHint = "A / D o cruceta para pasar de página · Tab o Create para cerrar";
        public const float PowerNoticeSeconds = 7f;
        public const string SoulEnergy = "Energía del alma";
        public const string WakeHint = "Sigue a los pájaros amarillos.";
        public const string RouteReminder = "Los pájaros amarillos marcan el camino.";
        public const string StoryHint = "Clic, Enter u Options para seguir";
        public const string GraveObjective = "Toca tu lápida (" + GameInput.InteractBinding + ")";
        public const string BallObjective = "Busca la pelota";
        public const string BirdsObjective = "Sigue a los pájaros amarillos";
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

        // Provisional: lo aprueba Eliud.
        public const string EndingStory = "Su familia la vio una última vez. Tiffany pudo descansar.";
        public const string EndingCredits = "Gracias por jugar\n\nEliud Sosa y Claudia.\n\nTyler, el perro del juego, es un perro real.";

        public static string PowerNotice(Power power)
        {
            switch (power)
            {
                case Power.Light: return "F o clic derecho / Triángulo: prende o apaga tu luz.\n"
                    + "Clic izquierdo / R2: destello para cegar a la bruja en el suelo y escapar.";
                case Power.Candles: return "Q / Cuadrado: enciende una veladora apagada cercana.\n"
                    + "Su luz te protege de la bruja por un tiempo.";
                case Power.Poltergeist: return "E / Círculo: mueve un objeto iluminado a 12 m o menos.\n"
                    + "El ruido distrae a la bruja cuando está en el suelo.";
                case Power.Vanish: return "R / R1: desvanécete durante 3 segundos.\n"
                    + "La bruja no te ve, oye ni huele y pierde tu rastro.";
                case Power.Manifest: return "Espacio / Equis: manifiéstate ante tu familia en la capilla.\n"
                    + "Solo puedes hacerlo ante los vivos, al final.";
                default: throw new ArgumentOutOfRangeException(nameof(power));
            }
        }
    }
}
