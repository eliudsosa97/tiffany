using System;

namespace TiffanyGame
{
    public static class RouteChapters
    {
        public static Chapter Resolve(Chapter current, int clearing)
        {
            if (current == Chapter.Return || clearing < 1 || clearing > 8) return current;
            var reached = (Chapter)Math.Min(clearing - 1, (int)Chapter.WitchHut);
            return reached > current ? reached : current;
        }
    }
}
