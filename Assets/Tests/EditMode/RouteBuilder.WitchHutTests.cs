using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed partial class RouteBuilderTests
    {
        [Test]
        public void WitchHutKeepsTheClearingMemoryAndChallengePositionsFromMain()
        {
            var clearing = route.GetChild(4);
            Assert.AreEqual("Route 5 Witch Hut", clearing.name);
            Assert.AreEqual(new Vector3(104f, 6f, 60f), clearing.localPosition);
            var hut = clearing.Find("Hut/witch-hut");
            Assert.IsNotNull(hut);
            Assert.AreEqual(new Vector3(0f, 0f, 5f), hut.parent.localPosition);
            Assert.AreEqual(Vector3.zero, hut.localPosition);
            Assert.Greater(Vector3.Dot(hut.forward, -clearing.forward), 0.999f);
            Assert.AreEqual(new Vector3(0f, 0f, 3f), clearing.Find("Memory Point").localPosition);
            var memory = clearing.GetComponentInChildren<MemorySpot>();
            Assert.AreEqual(5, memory.memoryId);
            Assert.AreEqual(clearing.TransformPoint(new Vector3(0f, 0f, 3f)), memory.transform.position);
            Assert.IsFalse(Physics.CheckCapsule(memory.transform.position + Vector3.up * 0.35f,
                memory.transform.position + Vector3.up * 1.2f, 0.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            var challenge = System.Array.Find(route.GetComponentsInChildren<PowerChallenge>(), item => item.kind == ChallengeKind.Hut);
            Assert.AreSame(route.GetChild(3), challenge.transform.parent);
            var terrain = new RouteTerrain();
            var expected = new Vector3(96f, 0f, 41.5f);
            expected.y = terrain.Height(expected.x, expected.z) + TerrainBuilder.FloorOffset;
            Assert.Less(Vector3.Distance(route.TransformPoint(expected), challenge.transform.position), 0.001f);
            Assert.AreEqual(-6f, challenge.checkpoint.transform.localPosition.z);
        }

        [Test]
        public void WitchHutDoorAndFenceGapAreWalkableFromTheTrailAndWallsBlockAtChestHeight()
        {
            var clearing = route.GetChild(4);
            foreach (float x in new[] { -1.5f, 0f, 1.5f })
            {
                Vector3 start = clearing.TransformPoint(new Vector3(x, 0.9f, -3f));
                Assert.IsFalse(Physics.CapsuleCast(start, start + Vector3.up * 0.35f, 0.25f,
                    clearing.forward, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            }
            var hut = clearing.Find("Hut/witch-hut");
            foreach (string part in new[] { "HutWalls", "HutFence" })
            {
                var colliders = hut.Find(part).GetComponentsInChildren<BoxCollider>();
                Assert.AreEqual(5, colliders.Length, part);
                foreach (var collider in colliders)
                {
                    Assert.IsFalse(collider.isTrigger);
                    Assert.IsNull(collider.GetComponent<GroundSurface>());
                    Vector3 normal = collider.size.x < collider.size.z ? hut.right : hut.forward;
                    foreach (float height in part == "HutWalls" ? new[] { 0.8f, 2.1f } : new[] { 0.8f })
                    {
                        Vector3 origin = collider.bounds.center + normal * 2f;
                        origin.y = clearing.position.y + height;
                        Assert.IsTrue(collider.Raycast(new Ray(origin, -normal), out _, 4f), collider.name);
                    }
                }
            }
            var ground = clearing.Find("Ground").GetComponent<Collider>();
            Assert.AreEqual(GroundType.Leaves, ground.GetComponent<GroundSurface>().type);
            Assert.IsTrue(ground.Raycast(new Ray(hut.position + Vector3.up, Vector3.down), out _, 2f));
            Assert.IsEmpty(hut.GetComponentsInChildren<GroundSurface>(true));
        }

        [Test]
        public void WitchHutTableHasSolidTopAndLegsWithATriggerUnderneath()
        {
            var hut = route.GetChild(4).Find("Hut/witch-hut");
            var hide = hut.Find("Hide Hut Table").GetComponent<HideVolume>();
            Assert.IsNotNull(hide);
            var trigger = hide.GetComponent<BoxCollider>();
            Assert.IsTrue(trigger.isTrigger);
            var colliders = hut.Find("HutInterior").GetComponentsInChildren<BoxCollider>();
            Assert.AreEqual(5, colliders.Length);
            var top = colliders[0];
            Assert.LessOrEqual(trigger.bounds.max.y, top.bounds.min.y);
            Assert.AreEqual(top.bounds.center.x, trigger.bounds.center.x, 0.001f);
            Assert.AreEqual(top.bounds.center.z, trigger.bounds.center.z, 0.001f);
            Assert.AreEqual(hut.position.y, hide.HidePoint.y, 0.001f);
            foreach (var collider in colliders) Assert.IsFalse(collider.isTrigger);
            Assert.IsTrue(top.Raycast(new Ray(trigger.bounds.center + Vector3.up * 2f, Vector3.down), out _, 3f));
        }

        [Test]
        public void WitchHutAddsOneSmallWarmShadowlessLightAndStaysWithinTheClearingBudget()
        {
            var clearing = route.GetChild(4);
            var hut = clearing.Find("Hut/witch-hut");
            var lights = hut.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(1, lights.Length);
            var light = lights[0];
            Assert.AreEqual(LightType.Point, light.type);
            Assert.AreEqual(LightShadows.None, light.shadows);
            Assert.That(light.range, Is.GreaterThan(0f).And.LessThanOrEqualTo(4f));
            Assert.That(light.intensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.Greater(light.color.r, light.color.g);
            Assert.Greater(light.color.g, light.color.b);
            Assert.AreEqual(ModelLibrary.LoadSpec("witch-hut").lightOffset, light.transform.localPosition);
            Assert.AreEqual(2, clearing.GetComponentsInChildren<Light>(true).Length);
            int renderers = clearing.GetComponentsInChildren<Renderer>(true).Length;
            TestContext.WriteLine($"Route 5 Witch Hut: {renderers} renderers");
            Assert.LessOrEqual(renderers, 200);
        }

        [Test]
        public void WitchHutRoofUsesExistingDitherAndCoverWithoutBlockingNavigation()
        {
            var roof = route.GetChild(4).Find("Hut/witch-hut/HutRoof");
            Assert.AreEqual(LayerMask.NameToLayer(ForestLayout.CoverLayer), roof.gameObject.layer);
            Assert.IsTrue(roof.GetComponent<Renderer>().enabled);
            Assert.IsTrue(roof.GetComponent<NavMeshModifier>().ignoreFromBuild);
            var collider = roof.GetComponent<MeshCollider>();
            Assert.AreSame(roof.GetComponent<MeshFilter>().sharedMesh, collider.sharedMesh);
            foreach (var material in roof.GetComponent<Renderer>().sharedMaterials)
                Assert.AreEqual(GeneratedMaterials.OcclusionShader, material.shader.name);
            var camera = new GameObject("Hut Dither Camera").AddComponent<FollowCamera>();
            camera.transform.SetParent(root.transform, false);
            camera.target = player.transform;
            player.transform.position = roof.parent.position;
            camera.Snap();
            Vector3 focus = player.transform.position + Vector3.up * FollowCamera.ChestHeight;
            var ray = new Ray(camera.transform.position, focus - camera.transform.position);
            Assert.IsTrue(collider.Raycast(ray, out var hit, 40f));
            Assert.Less(OcclusionVolume.KeepFraction(hit.point, camera.transform.position, focus,
                FollowCamera.OcclusionRadius, FollowCamera.OcclusionSoftness, FollowCamera.OcclusionFocusMargin,
                FollowCamera.OcclusionMinimumKeep, FollowCamera.OcclusionNearMargin), 1f);
        }
    }
}
