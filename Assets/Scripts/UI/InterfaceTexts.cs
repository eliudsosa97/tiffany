using System;

namespace TiffanyGame
{
    public static class InterfaceTexts
    {
        public const string PauseTitle = "Pausa";
        public const string PauseHint = "Esc o P para seguir";
        public const string NotebookEmpty = "Todavía no hay recuerdos";
        public const string NotebookHint = "A / D para pasar de página · Tab para cerrar";
        public const float PowerNoticeSeconds = 3f;

        public static string PowerNotice(Power power)
        {
            switch (power)
            {
                case Power.Light: return "Tu luz te obedece";
                case Power.Candles: return "Puedes encender veladoras";
                case Power.Poltergeist: return "Puedes mover objetos";
                case Power.Vanish: return "Puedes desvanecerte";
                case Power.Manifest: return "Puedes manifestarte";
                default: throw new ArgumentOutOfRangeException(nameof(power));
            }
        }
    }
}
