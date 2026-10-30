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
            Assert.Less(Quaternion.Angle(route.rotation, challenge.transform.rotation), 0.001f);
            Assert.AreEqual(-6f, challenge.checkpoint.transform.localPosition.z);
            foreach (var (point, x, z) in new[] { (challenge.Entry, 96f, 41.2f), (challenge.Exit, 96f, 46.2f),
                (challenge.post.position, 96f, 43.7f), (challenge.checkpoint.transform.position, 95.2f, 35.5f),
                (challenge.lure.transform.position, 102f, 43.7f),
                (challenge.candles[0].transform.position, 95f, 36.5f), (challenge.candles[1].transform.position, 95f, 39.5f) })
            {
                var local = route.InverseTransformPoint(point);
                Assert.AreEqual(x, local.x, 0.001f);
                Assert.AreEqual(z, local.z, 0.001f);
            }
        }

        [Test]
        public void WitchHutDoorAndFenceGapAreWalkableFromTheTrailAndWallsBlockAtChestHeight()
        {
            var clearing = route.GetChild(4);
            var trail = RouteLayout.Path(3);
            AssertHutWalkable(route.TransformPoint(trail[trail.Length - 1]), clearing.TransformPoint(Vector3.back * 3f));
            foreach (float x in new[] { -1.5f, 0f, 1.5f })
                AssertHutWalkable(clearing.TransformPoint(new Vector3(x, 0f, -3f)),
                    clearing.TransformPoint(new Vector3(x, 0f, 3f)));
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
            for (int i = 1; i < colliders.Length; i++) Assert.IsFalse(trigger.bounds.Intersects(colliders[i].bounds));
            var spec = ModelLibrary.LoadSpec("witch-hut");
            Vector3 entry = hut.TransformPoint(new Vector3(spec.hideVolume.center.x, 0f, spec.hideVolume.center.z + 1.05f));
            AssertHutWalkable(route.GetChild(4).TransformPoint(new Vector3(0f, 0f, 3f)), entry);
            AssertHutWalkable(entry, hide.HidePoint);
            CollectionAssert.Contains(Physics.OverlapSphere(entry, player.GetComponent<PlayerHiding>().searchRadius,
                Physics.AllLayers, QueryTriggerInteraction.Collide), trigger);
        }

        [Test]
        public void WitchHutHearthReachesTheDoorAndRoofHolesWithoutAddingLightsOrShadows()
        {
            var clearing = route.GetChild(4);
            var hut = clearing.Find("Hut/witch-hut");
            var lights = hut.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(1, lights.Length);
            var light = lights[0];
            Assert.AreEqual(LightType.Point, light.type);
            Assert.AreEqual(LightShadows.None, light.shadows);
            Assert.That(light.range, Is.GreaterThan(4f).And.LessThanOrEqualTo(8f));
            Assert.That(light.intensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(1.5f));
            Assert.Less(light.color.g, light.color.r * 0.3f);
            Assert.Greater(light.color.g, light.color.b);
            Assert.AreEqual(ModelLibrary.LoadSpec("witch-hut").lightOffset, light.transform.localPosition);
            foreach (var opening in new[] { new Vector3(0f, 0.8f, 4.3f), new Vector3(-2.2f, 3.65f, -1.25f),
                new Vector3(3.85f, 2.9f, 3.3f) })
                Assert.Less(Vector3.Distance(light.transform.localPosition, opening), light.range);
            Assert.AreEqual(2, clearing.GetComponentsInChildren<Light>(true).Length);
            int renderers = clearing.GetComponentsInChildren<Renderer>(true).Length;
            TestContext.WriteLine($"Route 5 Witch Hut: {renderers} renderers");
            Assert.LessOrEqual(renderers, 200);
        }

        [Test]
        public void HutInsideCapturePlacesTiffanyBesideTheTableWithTheGameCameraAndHiddenRoof()
        {
            var hut = route.GetChild(4).Find("Hut/witch-hut");
            var stop = hut.Find("Hut Inside").GetComponent<CaptureStop>();
            Assert.IsFalse(stop.overrideCamera);
            Assert.GreaterOrEqual(stop.wait, 1.5f);
            Assert.AreEqual(SoulEnergy.Max, stop.GetComponent<LightCaptureStage>().energy);
            Assert.IsTrue(stop.GetComponent<LightCaptureStage>().hideWitch);
            var point = hut.InverseTransformPoint(stop.transform.position);
            Assert.That(point.x, Is.InRange(-0.7f, -0.3f));
            Assert.That(point.z, Is.InRange(-0.5f, 0.5f));
            Assert.AreEqual(0f, point.y, 0.001f);
            AssertHutWalkable(hut.TransformPoint(new Vector3(0f, 0f, 1.5f)), stop.transform.position);
            var camera = new GameObject("Hut Inside Camera").AddComponent<FollowCamera>();
            camera.transform.SetParent(root.transform, false);
            camera.target = player.transform;
            stop.ApplyPose(player.transform, camera);
            Assert.IsTrue(camera.enabled);
            Assert.AreEqual(stop.transform.position, player.transform.position);
            Assert.Less(Quaternion.Angle(stop.transform.rotation, player.transform.rotation), 0.001f);
            Assert.AreEqual(FollowCamera.DefaultDistance, camera.distance);
            Assert.AreEqual(FollowCamera.DefaultPitch, camera.pitch);
            Assert.AreEqual(FollowCamera.DefaultYaw, camera.yaw);
            Assert.AreEqual(24f, camera.fieldOfView);
            var cutaway = hut.GetComponentInChildren<HutRoofCutaway>();
            Assert.AreSame(player.transform, cutaway.player);
            Assert.IsTrue(cutaway.interior.isTrigger);
            CollectionAssert.AreEquivalent(hut.Find("HutRoof").GetComponentsInChildren<Renderer>(true), cutaway.roofRenderers);
            cutaway.Refresh();
            foreach (var renderer in cutaway.roofRenderers) Assert.IsFalse(renderer.enabled);
            var cover = hut.Find("HutRoof").GetComponentInChildren<MeshCollider>();
            Assert.IsTrue(cover.enabled && cover.gameObject.activeInHierarchy);
            Assert.Greater(cover.bounds.min.y - hut.position.y, 2.5f);
            player.transform.position = hut.TransformPoint(Vector3.forward * 6f);
            cutaway.Refresh();
            foreach (var renderer in cutaway.roofRenderers) Assert.IsTrue(renderer.enabled);
        }

        [Test]
        public void WitchHutRoofUsesExistingDitherAndCoverWithoutBlockingNavigation()
        {
            var roof = route.GetChild(4).Find("Hut/witch-hut/HutRoof");
            Assert.AreEqual(LayerMask.NameToLayer(ForestLayout.CoverLayer), roof.gameObject.layer);
            Assert.IsTrue(roof.GetComponent<Renderer>().enabled);
            var collider = roof.GetComponentInChildren<MeshCollider>();
            Assert.IsTrue(collider.GetComponent<NavMeshModifier>().ignoreFromBuild);
            Assert.AreEqual(roof.gameObject.layer, collider.gameObject.layer);
            Assert.Greater(collider.bounds.min.y - roof.parent.position.y, 2.5f);
            Assert.AreSame(roof.GetComponent<MeshFilter>().sharedMesh, collider.sharedMesh);
            foreach (var material in roof.GetComponent<Renderer>().sharedMaterials)
                Assert.AreEqual(GeneratedMaterials.OcclusionShader, material.shader.name);
            var camera = new GameObject("Hut Dither Camera").AddComponent<FollowCamera>();
            camera.transform.SetParent(root.transform, false);
            camera.target = player.transform;
            Assert.AreEqual(36f, camera.distance);
            Assert.AreEqual(24f, camera.fieldOfView);
            Assert.AreEqual(42f, camera.pitch);
            foreach (var target in new[] { roof.parent.position,
                route.GetChild(4).Find("Memory Point").position, roof.parent.Find("Hide Hut Table").GetComponent<HideVolume>().HidePoint })
            {
                int roofHits = 0;
                for (int yaw = 0; yaw < 360; yaw += 90)
                {
                    player.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
                    camera.Snap();
                    Vector3 focus = target + Vector3.up * FollowCamera.ChestHeight;
                    var ray = new Ray(camera.transform.position, focus - camera.transform.position);
                    if (collider.Raycast(ray, out _, 40f)) roofHits++;
                    foreach (var renderer in roof.parent.GetComponentsInChildren<Renderer>(true))
                        AssertHutSightline(renderer, ray, Vector3.Distance(camera.transform.position, focus),
                            camera.transform.position, focus);
                }
                Assert.Greater(roofHits, 0, $"El techo no se interpone ante {target}.");
            }
        }

        static void AssertHutWalkable(Vector3 start, Vector3 end)
        {
            Vector3 bottom = start + Vector3.up * (TerrainBuilder.FloorOffset + 0.251f);
            Vector3 top = start + Vector3.up * (TerrainBuilder.FloorOffset + 0.951f);
            Assert.IsFalse(Physics.CheckCapsule(bottom, top, 0.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), $"Inicio bloqueado: {start}");
            Vector3 direction = end - start;
            Assert.IsFalse(Physics.CapsuleCast(bottom, top, 0.25f, direction.normalized, out var hit,
                direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                $"Paso bloqueado por {hit.collider}: {start} -> {end}");
        }

        static void AssertHutSightline(Renderer renderer, Ray ray, float distance, Vector3 camera, Vector3 focus)
        {
            var probe = new GameObject("Hut Sightline Probe");
            probe.transform.SetParent(renderer.transform, false);
            try
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                Physics.SyncTransforms();
                while (distance > 0f && collider.Raycast(ray, out var hit, distance))
                {
                    Assert.GreaterOrEqual(hit.triangleIndex, 0);
                    long triangles = 0;
                    Material material = null;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        triangles += mesh.GetIndexCount(i) / 3;
                        if (hit.triangleIndex >= triangles) continue;
                        material = renderer.sharedMaterials[i];
                        break;
                    }
                    Assert.IsNotNull(material, renderer.name);
                    Assert.AreEqual(GeneratedMaterials.OcclusionShader, material.shader.name, renderer.name + "/" + material.name);
                    Assert.Less(OcclusionVolume.KeepFraction(hit.point, camera, focus,
                        FollowCamera.OcclusionRadius, FollowCamera.OcclusionSoftness, FollowCamera.OcclusionFocusMargin,
                        FollowCamera.OcclusionMinimumKeep, FollowCamera.OcclusionNearMargin), 1f, renderer.name);
                    float advance = hit.distance + 0.001f;
                    ray.origin += ray.direction * advance;
                    distance -= advance;
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }
    }
}
