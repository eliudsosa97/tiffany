using System;

namespace TiffanyGame
{
    public static class InterfaceTexts
    {
        public const string PauseTitle = "Pausa";
        public const string PauseHint = "Esc o P para seguir";
        public const string NotebookEmpty = "Aún no recuerdas nada";
        public const string NotebookHint = "A / D para pasar de página · Tab para cerrar";
        public const float PowerNoticeSeconds = 3f;

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
