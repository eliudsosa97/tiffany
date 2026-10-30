using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class TrailPatchesTests
    {
        [Test]
        public void OnlyTheFiveRequestedPathsHaveOneOrTwoAlternatingPatches()
        {
            int total = 0;
            for (int from = 0; from < RouteLayout.Count - 1; from++)
            {
                int count = TrailPatches.CountForPath(from);
                if (from < 5) Assert.That(count, Is.InRange(1, 2), $"Sendero {from + 1}");
                else Assert.AreEqual(0, count);
                total += count;
            }
            Assert.AreEqual(TrailPatches.Count, total);
            for (int i = 0; i < total; i++)
                Assert.AreEqual(i % 2 == 0 ? GroundType.Leaves : GroundType.Branches, TrailPatches.Get(i).Type);
        }

        [Test]
        public void PatchesFitTheTrailAndReserveAnApproachToTheirSideHide()
        {
            for (int i = 0; i < TrailPatches.Count; i++)
            {
                var patch = TrailPatches.Get(i);
                Assert.AreEqual(RouteLayout.PathWidth, patch.Width);
                Assert.That(patch.Length, Is.InRange(4f, 8f));
                Assert.That(patch.HideSide, Is.EqualTo(-1).Or.EqualTo(1));
                Assert.That(Mathf.Abs(Vector3.Dot(patch.HidePosition - patch.Center, patch.Right)) - patch.Width * 0.5f,
                    Is.InRange(2f, 4f));
                var points = RouteLayout.Path(patch.From);
                float total = 0f;
                for (int p = 1; p < points.Length; p++)
                    total += Vector3.ProjectOnPlane(points[p] - points[p - 1], Vector3.up).magnitude;
                Assert.GreaterOrEqual(patch.Distance - patch.Length * 0.5f, 0f);
                Assert.LessOrEqual(patch.Distance + patch.Length * 0.5f, total);
                for (int step = 0; step <= 8; step++)
                {
                    Vector3 point = Vector3.Lerp(patch.Start, patch.End, step / 8f);
                    float nearest = float.PositiveInfinity;
                    for (int p = 1; p < points.Length; p++)
                        nearest = Mathf.Min(nearest, RouteTerrain.SegmentDistance(Xz(point), Xz(points[p - 1]), Xz(points[p])));
                    Assert.Less(nearest, 0.001f, $"Parche {i}, muestra {step}");
                    point = Vector3.Lerp(patch.Center, patch.HidePosition, step / 8f);
                    Assert.IsTrue(TrailPatches.Reserves(Xz(point), 0f));
                }
            }
            Assert.IsFalse(TrailPatches.Reserves(new Vector2(1000f, 1000f), 1f));
        }

        [Test]
        public void ReturnPatchUsesTheUpperLandingSixMetersBeforeTheDescent()
        {
            var path = RouteLayout.Path(4);
            Vector3 slopeStart = path[1] + Vector3.ProjectOnPlane(path[2] - path[1], Vector3.up).normalized * RouteTerrain.RampLanding;
            for (int i = 0; i < TrailPatches.Count; i++)
            {
                var patch = TrailPatches.Get(i);
                if (patch.From != 4) continue;
                Assert.LessOrEqual(patch.Distance + patch.Length * 0.5f, Vector3.Distance(path[0], path[1]));
                Assert.GreaterOrEqual(RouteTerrain.SegmentDistance(Xz(slopeStart), Xz(patch.Start), Xz(patch.End)),
                    TrailPatches.Clearance - 0.001f);
            }
        }

        static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
    }
}
