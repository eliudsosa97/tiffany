using System;

namespace TiffanyGame
{
    public static class InterfaceTexts
    {
        public const string Title = "Tiffany";
        public const string StartHint = "Clic, Espacio o Start para empezar";
        public const string Continue = "Continuar";
        public const string NewGame = "Nueva partida";
        public const string Controls = "WASD / stick izq.  mover\n"
            + "Ratón / stick der.  apuntar\n"
            + "Shift / L3  correr\n"
            + "Ctrl / L2  despacio\n"
            + "Clic / △  prender la luz\n"
            + "F / R2  destello\n"
            + "Q / □  encender veladora\n"
            + "E / ○  mover objeto\n"
            + "R / R1  desvanecerse\n"
            + "Espacio / ✕  esconderse y recoger\n"
            + "Tab / botón Create  cuaderno\n"
            + "Esc o P / Options  pausa";
        public const string PauseTitle = "Pausa";
        public const string PauseHint = "W/S, flechas o stick/cruceta: elegir\nEspacio / Enter / ✕ / clic: aceptar · Esc / P / Options: seguir";
        public const string PauseResume = "Seguir";
        public const string PauseRespawn = "Volver a la última veladora";
        public const string PauseFullscreen = "Pantalla completa";
        public const string PauseRestart = "Reiniciar partida";
        public const string PauseConfirmRestart = "¿Reiniciar? Se pierde la partida";
        public const string NotebookEmpty = "Aún no recuerdas nada";
        public const string NotebookHint = "A / D para pasar de página · Tab para cerrar";
        public const float PowerNoticeSeconds = 3f;

        public static string ControllerText(string text, bool hasSymbols) => hasSymbols ? text :
            text.Replace("△", "Triángulo").Replace("□", "Cuadrado").Replace("○", "Círculo").Replace("✕", "Equis");

        // Provisional: lo aprueba Eliud.
        public const string EndingStory = "Su familia la vio una última vez. Tiffany pudo descansar.";
        public const string EndingCredits = "Gracias por jugar\n\nEliud Sosa y Claudia.\n\nTyler, el perro del juego, es un perro real.";

        public static string PowerNotice(Power power)
        {
            switch (power)
            {
                case Power.Light: return "Tu luz te obedece · clic o △ prende y apaga, F o R2 para el destello";
                case Power.Candles: return "Puedes encender veladoras · Q";
                case Power.Poltergeist: return "Puedes mover cosas · E";
                case Power.Vanish: return "Puedes desvanecerte · R";
                case Power.Manifest: return "Tu familia puede verte";
                default: throw new ArgumentOutOfRangeException(nameof(power));
            }
        }
    }
}
