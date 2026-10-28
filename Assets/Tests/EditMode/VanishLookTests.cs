using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class VanishLookTests
    {
        [TestCase(0f, 1f)]
        [TestCase(Vanish.Duration, 1f)]
        [TestCase(Vanish.Duration - VanishLook.FadeTime * 0.5f, 0.5f)]
        [TestCase(Vanish.Duration * 0.5f, 0f)]
        [TestCase(VanishLook.FadeTime * 0.5f, 0.5f)]
        [TestCase(-1f, 1f)]
        public void VisibilityFadesOutAndBackIn(float remaining, float expected)
        {
            Assert.AreEqual(expected, VanishLook.Visibility(remaining, Vanish.Duration), 1e-5f);
        }

        [TestCase(1f, true)]
        [TestCase(0f, false)]
        public void FigureIsSteadyAtFullVisibilityAndInvisibility(float visibility, bool expected)
        {
            for (int i = 0; i < 300; i++)
                Assert.AreEqual(expected, VanishLook.FigureShown(visibility, i * 0.01f));
        }

        [Test]
        public void FigureFlickersAtHalfVisibility()
        {
            bool sawShown = false;
            bool sawHidden = false;
            for (int i = 0; i < 300; i++)
            {
                bool shown = VanishLook.FigureShown(0.5f, i * 0.01f);
                sawShown |= shown;
                sawHidden |= !shown;
            }

            Assert.IsTrue(sawShown);
            Assert.IsTrue(sawHidden);
        }
    }
}
