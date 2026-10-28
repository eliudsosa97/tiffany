using NUnit.Framework;
using UnityEngine;

namespace ElVelador.Tests
{
    public class GroundSurfaceTests
    {
        GameObject ground;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ground);
        }

        [Test]
        public void TypeOfReturnsSurfaceOnColliderObject()
        {
            ground = new GameObject("Ground");
            var collider = ground.AddComponent<BoxCollider>();
            ground.AddComponent<GroundSurface>().type = GroundType.Leaves;

            Assert.AreEqual(GroundType.Leaves, GroundSurface.TypeOf(collider));
        }

        [Test]
        public void TypeOfDefaultsToDirtWithoutSurface()
        {
            ground = new GameObject("Ground");

            Assert.AreEqual(GroundType.Dirt, GroundSurface.TypeOf(ground.AddComponent<BoxCollider>()));
        }

        [Test]
        public void TypeOfDefaultsToDirtWithoutCollider()
        {
            Assert.AreEqual(GroundType.Dirt, GroundSurface.TypeOf(null));
        }
    }
}
