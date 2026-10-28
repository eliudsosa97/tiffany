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
            var matrices = new Matrix4x4[4];
            for (int i = 0; i < matrices.Length; i++)
            {
                var position = new Vector3(i % 2 * 3f, 0f, i / 2 * 4f);
                matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                if (instanced) continue;
                var tree = new GameObject("Oak", typeof(CapsuleCollider));
                tree.transform.SetParent(world.transform, false);
                tree.transform.localPosition = position;
                var trunk = tree.GetComponent<CapsuleCollider>();
                trunk.center = Vector3.up * 3f;
                trunk.height = 6f;
                trunk.radius = 0.6f;
            }
            if (instanced)
            {
                treeMesh = new Mesh { name = "Oak", bounds = new Bounds(Vector3.up * 3f, new Vector3(1.2f, 6f, 1.2f)) };
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

            var stops = world.GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(3, stops.Length);
            CollectionAssert.AreEquivalent(new[] { "Cover 1 Tree", "Cover 2 Building", "Cover 3 Dense Forest" },
                System.Array.ConvertAll(stops, stop => stop.name));
            foreach (var stop in stops)
            {
                Assert.IsTrue(stop.overrideCamera);
                Assert.IsNotNull(stop.GetComponent<LightCaptureStage>());
                Assert.AreEqual(7f, stop.transform.position.y, 0.01f);
                var focus = stop.transform.position + Vector3.up * FollowCamera.ChestHeight;
                var ray = new Ray(stop.cameraPosition, focus - stop.cameraPosition);
                float distance = Vector3.Distance(focus, stop.cameraPosition) - FollowCamera.OcclusionFocusMargin;
                if (stop.name == "Cover 2 Building")
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
                    Assert.GreaterOrEqual(count, stop.name == "Cover 3 Dense Forest" ? 2 : 1);
                }
                Assert.Greater(Vector3.Dot(stop.cameraRotation * Vector3.forward, ray.direction), 0.999f);
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
