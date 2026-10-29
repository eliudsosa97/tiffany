using System;

namespace TiffanyGame
{
    public static class InterfaceTexts
    {
        public const string Title = "Tiffany";
        public const string StartHint = "Clic, Espacio o Start para empezar";
        public const string Continue = "Continuar";
        public const string NewGame = "Nueva partida";
        public const string Controls = "WASD  moverte\n"
            + "Shift  correr · Ctrl  despacio\n"
            + "Ratón  apuntar la luz\n"
            + "F  prender la luz · Clic  destello\n"
            + "Q E R  poderes\n"
            + "Espacio  esconderte y recoger\n"
            + "Tab  cuaderno · Esc o P  pausa";
        public const string PauseTitle = "Pausa";
        public const string PauseHint = "Esc o P para seguir";
        public const string NotebookEmpty = "Aún no recuerdas nada";
        public const string NotebookHint = "A / D para pasar de página · Tab para cerrar";
        public const float PowerNoticeSeconds = 3f;

        // Provisional: lo aprueba Eliud.
        public const string EndingStory = "Su familia la vio una última vez. Tiffany pudo descansar.";
        public const string EndingCredits = "Gracias por jugar\n\nEliud Sosa y Claudia.\n\nTyler, el perro del juego, es un perro real.";

        public static string PowerNotice(Power power)
        {
            switch (power)
            {
                case Power.Light: return "Tu luz te obedece · F prende y apaga, clic para el destello";
                case Power.Candles: return "Puedes encender veladoras · Q";
                case Power.Poltergeist: return "Puedes mover cosas · E";
                case Power.Vanish: return "Puedes desvanecerte · R";
                case Power.Manifest: return "Tu familia puede verte";
                default: throw new ArgumentOutOfRangeException(nameof(power));
            }
        }
    }
}
