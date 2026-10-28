using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class WebDebugQueryTests
    {
        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("https://localhost/playthrough", false)]
        [TestCase("https://localhost/?playthrough", true)]
        [TestCase("?playthrough=1", true)]
        [TestCase("?playthrough=", true)]
        [TestCase("?a=1&playthrough", true)]
        [TestCase("?playthrough&a=1", true)]
        [TestCase("?playthroughs", false)]
        [TestCase("?Playthrough", false)]
        [TestCase("?a=playthrough", false)]
        [TestCase("?a=1#?playthrough", false)]
        [TestCase("https://localhost/#?playthrough", false)]
        [TestCase("?playthrough#ignored", true)]
        [TestCase("?", false)]
        [TestCase("?&&playthrough&&", true)]
        public void HasMatchesOnlyTheExactQueryName(string url, bool expected) =>
            Assert.AreEqual(expected, WebDebugQuery.Has(url, "playthrough"));

        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("https://localhost/value", null)]
        [TestCase("?value", null)]
        [TestCase("?value=", null)]
        [TestCase("?values=wrong", null)]
        [TestCase("?Value=wrong", null)]
        [TestCase("#?value=wrong", null)]
        [TestCase("?other=1#?value=wrong", null)]
        [TestCase("?value=hello+world", "hello world")]
        [TestCase("?value=ni%C3%B1a%20%2B%20luz", "niña + luz")]
        [TestCase("?other=1&value=a%26b%3Dc&last=2", "a&b=c")]
        [TestCase("?value=a=b#ignored", "a=b")]
        [TestCase("?value=first&value=second", "first")]
        public void GetDecodesOnlyTheValueBeforeTheFragment(string url, string expected) =>
            Assert.AreEqual(expected, WebDebugQuery.Get(url, "value"));

        [TestCase(null)]
        [TestCase("")]
        public void EmptyKeysDoNotMatch(string key)
        {
            Assert.IsFalse(WebDebugQuery.Has("?=value", key));
            Assert.IsNull(WebDebugQuery.Get("?=value", key));
        }
    }
}
