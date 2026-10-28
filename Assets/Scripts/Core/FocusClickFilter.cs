namespace TiffanyGame
{
    public sealed class FocusClickFilter
    {
        public const float IgnoreAfterFocus = 0.3f;

        bool focused;
        float focusTime;

        public FocusClickFilter(bool focused, float time)
        {
            this.focused = focused;
            focusTime = time;
        }

        public void SetFocus(bool focused, float time)
        {
            if (focused && !this.focused)
                focusTime = time;
            this.focused = focused;
        }

        public bool Accept(bool clickPressed, float time) =>
            clickPressed && focused && time - focusTime >= IgnoreAfterFocus;
    }
}
