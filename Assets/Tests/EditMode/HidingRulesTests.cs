using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class HidingRulesTests
    {
        [TestCase(false, false, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, true, true, true)]
        [TestCase(true, false, false, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, false)]
        public void InteractionChangesHidingOnlyWhenAllowed(bool hidden, bool pressed, bool inside, bool expected)
        {
            Assert.AreEqual(expected, HidingRules.NextHidden(hidden, pressed, inside));
        }
    }
}
