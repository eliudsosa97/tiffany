namespace TiffanyGame
{
    public sealed class FocusClickFilter
    {
        public const float IgnoreAfterFocus = 0.3f;

        bool focused;
        float focusTime;
        // El player WebGL arranca creyéndose enfocado aunque el iframe no tenga foco.
        bool consumeFirstClick;

        public FocusClickFilter(bool focused, float time, bool consumeFirstClick)
        {
            this.focused = focused;
            focusTime = time;
            this.consumeFirstClick = consumeFirstClick;
        }

        public void SetFocus(bool focused, float time)
        {
            if (focused && !this.focused)
                focusTime = time;
            this.focused = focused;
        }

        public bool Accept(bool clickPressed, float time)
        {
            if (clickPressed && consumeFirstClick)
            {
                consumeFirstClick = false;
                return false;
            }

            return clickPressed && focused && time - focusTime >= IgnoreAfterFocus;
        }
    }
}
