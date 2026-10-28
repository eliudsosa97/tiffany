using System;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;

namespace TiffanyGame.Tests
{
    public sealed class BuildToolsTests
    {
        Action<BuildTargetGroup, BuildTarget> original;
        int switches;
        BuildTargetGroup switchedGroup;
        BuildTarget switchedTarget;

        [SetUp]
        public void SetUp()
        {
            original = BuildTools.switchTarget;
            switches = 0;
            BuildTools.switchTarget = (group, target) =>
            {
                switches++;
                switchedGroup = group;
                switchedTarget = target;
            };
        }

        [TearDown]
        public void TearDown() => BuildTools.switchTarget = original;

        [Test]
        public void WebBuildAfterMacBuildSwitchesToWebGLFirst()
        {
            BuildTools.UseTarget(BuildTarget.StandaloneOSX, BuildTarget.WebGL);

            Assert.AreEqual(1, switches);
            Assert.AreEqual(BuildTargetGroup.WebGL, switchedGroup);
            Assert.AreEqual(BuildTarget.WebGL, switchedTarget);
        }

        [Test]
        public void MatchingTargetIsLeftAlone()
        {
            BuildTools.UseTarget(BuildTarget.WebGL, BuildTarget.WebGL);

            Assert.AreEqual(0, switches);
        }
    }
}
