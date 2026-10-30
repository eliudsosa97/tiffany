using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class HutRoofCutawayTests
    {
        [Test]
        public void RotatedInteriorHidesRoofChildrenKeepsCoverAndRestoresOnDisable()
        {
            var hut = new GameObject("Hut");
            var player = new GameObject("Player");
            try
            {
                hut.transform.SetPositionAndRotation(new Vector3(104f, 6f, 60f), Quaternion.Euler(0f, 135f, 0f));
                var interior = hut.AddComponent<BoxCollider>();
                interior.isTrigger = true;
                interior.center = Vector3.up * 1.25f;
                interior.size = new Vector3(7.6f, 3f, 7.6f);
                var roof = new GameObject("HutRoof");
                roof.transform.SetParent(hut.transform, false);
                var renderer = roof.AddComponent<MeshRenderer>();
                var cover = roof.AddComponent<BoxCollider>();
                var child = new GameObject("Palm");
                child.transform.SetParent(roof.transform, false);
                var palm = child.AddComponent<MeshRenderer>();
                child.SetActive(false);
                var cutaway = hut.AddComponent<HutRoofCutaway>();
                cutaway.player = player.transform;
                cutaway.interior = interior;
                cutaway.roofRenderers = roof.GetComponentsInChildren<Renderer>(true);

                player.transform.position = hut.transform.TransformPoint(Vector3.zero);
                cutaway.Refresh();
                Assert.IsFalse(renderer.enabled);
                Assert.IsFalse(palm.enabled);
                Assert.IsTrue(roof.activeSelf && cover.enabled);
                player.transform.position = hut.transform.TransformPoint(Vector3.forward * 3.9f);
                cutaway.Refresh();
                Assert.IsFalse(renderer.enabled);
                player.transform.position = hut.transform.TransformPoint(Vector3.forward * 4.1f);
                cutaway.Refresh();
                Assert.IsTrue(renderer.enabled && palm.enabled);
                Assert.IsFalse(child.activeSelf);
                player.transform.position = hut.transform.TransformPoint(Vector3.zero);
                cutaway.Refresh();
                Assert.IsFalse(renderer.enabled);
                typeof(HutRoofCutaway).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(cutaway, null);
                Assert.IsTrue(renderer.enabled && palm.enabled);
                cutaway.Refresh();
                Assert.IsFalse(renderer.enabled);
            }
            finally
            {
                Object.DestroyImmediate(hut);
                Object.DestroyImmediate(player);
            }
        }
    }
}
