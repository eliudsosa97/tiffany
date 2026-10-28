using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;

namespace TiffanyGame.Tests
{
    public sealed class BuildToolsTests
    {
        int switches;
        BuildTargetGroup switchedGroup;
        BuildTarget switchedTarget;

        bool Switch(BuildTargetGroup group, BuildTarget target, bool result)
        {
            switches++;
            switchedGroup = group;
            switchedTarget = target;
            return result;
        }

        [SetUp]
        public void SetUp() => switches = 0;

        [TestCase(false, "Builds/Web")]
        [TestCase(true, "Builds/WebDebug")]
        public void WebBuildsKeepPublicAndDebugOutputsSeparate(bool debug, string path)
        {
            var scenes = new[] { "Assets/Scenes/Main.unity" };
            var options = BuildTools.WebOptions(scenes, debug);
            CollectionAssert.AreEqual(scenes, options.scenes);
            Assert.AreEqual(BuildTarget.WebGL, options.target);
            Assert.AreEqual(BuildOptions.None, options.options);
            Assert.AreEqual(path, options.locationPathName);
            CollectionAssert.AreEqual(debug ? new[] { WebDebugQuery.Define } : System.Array.Empty<string>(),
                options.extraScriptingDefines);
        }

        [TestCase("TIFFANY_WEB_DEBUG", false, true)]
        [TestCase("FOO; TIFFANY_WEB_DEBUG ;BAR", false, true)]
        [TestCase("TIFFANY_WEB_DEBUG", true, false)]
        [TestCase("TIFFANY_WEB_DEBUGGER;FOO", false, false)]
        [TestCase("", false, false)]
        [TestCase(null, false, false)]
        public void PublicBuildRefusesTheDebugDefineInPlayerSettings(string defines, bool debug, bool leaks)
        {
            Assert.AreEqual(leaks, BuildTools.LeaksWebDebug(defines, debug));
        }

        [Test]
        public void PublicBuildAfterDebugDoesNotInheritTheDefine()
        {
            BuildTools.WebOptions(System.Array.Empty<string>(), true);
            Assert.IsEmpty(BuildTools.WebOptions(System.Array.Empty<string>(), false).extraScriptingDefines);
        }

        [TestCase(BuildTarget.StandaloneOSX, BuildTarget.WebGL, BuildTargetGroup.WebGL)]
        [TestCase(BuildTarget.WebGL, BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone)]
        public void BuildSwitchesToItsTargetFirst(BuildTarget active, BuildTarget target, BuildTargetGroup group)
        {
            Assert.IsTrue(BuildTools.UseTarget(active, target, (g, t) => Switch(g, t, true)));

            Assert.AreEqual(1, switches);
            Assert.AreEqual(group, switchedGroup);
            Assert.AreEqual(target, switchedTarget);
        }

        [Test]
        public void MatchingTargetIsLeftAlone()
        {
            Assert.IsTrue(BuildTools.UseTarget(BuildTarget.WebGL, BuildTarget.WebGL, (g, t) => Switch(g, t, true)));

            Assert.AreEqual(0, switches);
        }

        [Test]
        public void FailedSwitchStopsTheBuild()
        {
            Assert.IsFalse(BuildTools.UseTarget(BuildTarget.StandaloneOSX, BuildTarget.WebGL, (g, t) => Switch(g, t, false)));
        }
    }
}
