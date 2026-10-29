using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class OcclusionCaptureBuilderTests
    {
        GameObject world;
        Mesh treeMesh;
        ForestInstanceData forestData;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(world);
            if (treeMesh != null) Object.DestroyImmediate(treeMesh);
            if (forestData != null) Object.DestroyImmediate(forestData);
        }

        [TestCase(0f, 0f, false)]
        [TestCase(231f, 71f, false)]
        [TestCase(231f, 71f, true)]
        public void FindsCoverFromGeometryAfterWorldMovesAndRotates(float offset, float yaw, bool instanced)
        {
            world = new GameObject("World");
            var ground = Part("Ground", world.transform, Vector3.down * 0.25f, new Vector3(60f, 0.5f, 60f));
            ground.AddComponent<GroundSurface>();
            var matrices = new Matrix4x4[16];
            for (int i = 0; i < matrices.Length; i++)
            {
                var position = new Vector3(i % 4 * 2f, 0f, i / 4 * 2f);
                matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                if (instanced) continue;
                var tree = new GameObject("Oak", typeof(CapsuleCollider));
                tree.transform.SetParent(world.transform, false);
                tree.transform.localPosition = position;
                var trunk = tree.GetComponent<CapsuleCollider>();
                trunk.center = Vector3.up * 4f;
                trunk.height = 8f;
                trunk.radius = 0.6f;
            }
            if (instanced)
            {
                treeMesh = new Mesh { name = "Oak", bounds = new Bounds(Vector3.up * 4f, new Vector3(1.2f, 8f, 1.2f)) };
                forestData = ScriptableObject.CreateInstance<ForestInstanceData>();
                forestData.batches = new[] { new ForestInstanceData.Batch { mesh = treeMesh, matrices = matrices } };
                var forest = world.AddComponent<ForestInstances>();
                forest.enabled = false;
                forest.data = forestData;
            }
            var house = new GameObject("House").transform;
            house.SetParent(world.transform, false);
            house.localPosition = Vector3.right * 12f;
            var wall = Part("Back Wall", house, Vector3.up * 2.5f, new Vector3(10f, 5f, 0.5f));
            world.transform.SetPositionAndRotation(new Vector3(offset, 7f, -offset), Quaternion.Euler(0f, yaw, 0f));

            OcclusionCaptureBuilder.Build(world.transform);
            var player = new GameObject("Tiffany").transform;
            player.SetParent(world.transform, false);
            var follow = new GameObject("Follow Camera", typeof(Camera)).AddComponent<FollowCamera>();
            follow.transform.SetParent(world.transform, false);
            follow.target = player;
            follow.enabled = false;

            var stops = world.GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(6, stops.Length);
            System.Array.Sort(stops, AutoCapture.CompareStops);
            CollectionAssert.AreEqual(new[] { "Cover 1 Tree Before", "Cover 1 Tree After", "Cover 2 Building Before",
                    "Cover 2 Building After", "Cover 3 Dense Forest Before", "Cover 3 Dense Forest After" },
                System.Array.ConvertAll(stops, stop => stop.name));
            for (int i = 0; i < stops.Length; i += 2)
            {
                Assert.IsFalse(stops[i].GetComponent<OcclusionCaptureStage>().after);
                Assert.IsTrue(stops[i + 1].GetComponent<OcclusionCaptureStage>().after);
                Assert.AreEqual(stops[i].transform.position, stops[i + 1].transform.position);
                Assert.AreEqual(stops[i].transform.rotation, stops[i + 1].transform.rotation);
                Assert.AreEqual(stops[i].cameraPosition, stops[i + 1].cameraPosition);
                Assert.AreEqual(stops[i].cameraRotation, stops[i + 1].cameraRotation);
            }
            foreach (var stop in stops)
            {
                Assert.IsFalse(stop.overrideCamera);
                Assert.IsNotNull(stop.GetComponent<LightCaptureStage>());
                Assert.AreEqual(7f, stop.transform.position.y, 0.01f);
                stop.ApplyPose(player, follow);
                Assert.IsTrue(follow.enabled);
                Assert.Less(Vector3.Distance(stop.cameraPosition, follow.transform.position), 1e-4f);
                Assert.Less(Quaternion.Angle(stop.cameraRotation, follow.transform.rotation), 0.01f);
                var head = player.position + Vector3.up * 1.2f;
                var ray = new Ray(follow.transform.position, head - follow.transform.position);
                float distance = Vector3.Distance(head, follow.transform.position) - FollowCamera.OcclusionFocusMargin;
                var stage = stop.GetComponent<OcclusionCaptureStage>();
                Assert.IsNotEmpty(stage.covers);
                Assert.IsTrue(stage.covers[0].Intersects(follow.transform.position, head), stop.name);
                if (stop.name.StartsWith("Cover 2 Building"))
                    Assert.IsTrue(wall.GetComponent<Collider>().Raycast(ray, out _, distance));
                else
                {
                    int count = 0;
                    foreach (var tree in world.GetComponentsInChildren<CapsuleCollider>())
                        if (tree.Raycast(ray, out _, distance)) count++;
                    if (instanced)
                        foreach (var matrix in matrices)
                        {
                            var bounds = ForestInstances.TransformBounds(treeMesh.bounds, world.transform.localToWorldMatrix * matrix);
                            if (bounds.IntersectRay(ray, out float entry) && entry > 0f && entry < distance) count++;
                        }
                    Assert.GreaterOrEqual(count, stop.name.StartsWith("Cover 3 Dense Forest") ? 2 : 1);
                }
                Assert.Less(Quaternion.Angle(Quaternion.Euler(FollowCamera.DefaultPitch, FollowCamera.DefaultYaw, 0f),
                    follow.transform.rotation), 0.01f);
            }
        }

        static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            return part;
        }
    }
}
