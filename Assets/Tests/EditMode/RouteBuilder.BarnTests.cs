using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed partial class RouteBuilderTests
    {
        [Test]
        public void BarnKeepsFullWallCollisionsAnOpenDoorAndTheMemoryOnNonSolidMud()
        {
            var clearing = route.GetChild(3);
            var barn = clearing.Find("Barn/barn");
            Assert.AreEqual(new Vector3(0f, 0f, 5f), barn.parent.localPosition);
            Assert.IsEmpty(barn.GetComponentsInChildren<MeshCollider>(true));
            Assert.IsFalse(barn.Find("BarnRoof").GetComponent<Renderer>().enabled);
            Assert.IsTrue(barn.Find("BarnRoof/Roof").GetComponent<BoxCollider>().enabled);
            var walls = barn.Find("BarnWalls").GetComponentsInChildren<BoxCollider>();
            Assert.AreEqual(5, walls.Length);
            foreach (var wall in walls)
            {
                Assert.IsFalse(wall.isTrigger);
                Assert.AreEqual(5f, wall.bounds.size.y, 0.001f);
                var normal = wall.size.x < wall.size.z ? barn.right : barn.forward;
                foreach (float height in new[] { 0.6f, 4.75f })
                    Assert.IsTrue(wall.Raycast(new Ray(wall.bounds.center + normal * 2f
                        + Vector3.up * (height - 2.5f), -normal), out _, 4f), wall.name);
            }
            Assert.AreEqual(8, barn.Find("BarnInterior").GetComponentsInChildren<BoxCollider>().Length);
            Assert.AreEqual(3, barn.Find("BarnFence").GetComponentsInChildren<BoxCollider>().Length);
            foreach (float x in new[] { -1.7f, 0f, 1.7f })
                AssertHutWalkable(clearing.TransformPoint(new Vector3(x, 0f, -2f)), clearing.TransformPoint(new Vector3(x, 0f, 7f)));
            var mud = barn.Find("BarnMud");
            Assert.IsEmpty(mud.GetComponentsInChildren<Collider>(true));
            Assert.AreEqual(GroundType.Dirt, mud.GetComponent<GroundSurface>().type);
            Assert.Greater(mud.GetComponent<Renderer>().sharedMaterial.GetFloat("_Smoothness"), 0.8f);
            var memory = clearing.GetComponentInChildren<MemorySpot>();
            Assert.AreEqual(4, memory.memoryId);
            var bounds = mud.GetComponent<Renderer>().bounds;
            Assert.IsTrue(bounds.Contains(new Vector3(memory.transform.position.x, bounds.center.y, memory.transform.position.z)));
            Assert.AreEqual(new Vector3(-4.5f, 0f, 7f), clearing.Find("Hide Stable Crate").localPosition);
            Assert.LessOrEqual(clearing.GetComponentsInChildren<Renderer>(true).Length, 200);
        }

        [Test]
        public void BarnEntranceFacesTheGameCameraLikeThePreviousShelter()
        {
            var clearing = route.GetChild(3);
            var barn = clearing.Find("Barn/barn");
            Assert.AreEqual(Quaternion.identity, barn.parent.localRotation);
            Assert.AreEqual(Quaternion.identity, barn.localRotation);
            var cameraDirection = -(Quaternion.Euler(FollowCamera.DefaultPitch, FollowCamera.DefaultYaw, 0f) * Vector3.forward);
            Assert.Greater(Vector3.Dot(-barn.forward, cameraDirection), 0f);
            var entrance = barn.TransformPoint(new Vector3(0f, 0.6f, -5f));
            foreach (var wall in barn.Find("BarnWalls").GetComponentsInChildren<BoxCollider>())
                Assert.IsFalse(wall.bounds.Contains(entrance));
            foreach (var renderer in barn.Find("BarnRoof").GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(renderer.enabled);
        }
    }
}
