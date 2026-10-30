using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PosterLayoutTests
    {
        readonly RouteTerrain terrain = new RouteTerrain();
        List<PosterLayout.Anchor> candidates;

        [OneTimeSetUp]
        public void SetUp()
        {
            candidates = new List<PosterLayout.Anchor>();
            for (float z = terrain.Bounds.yMin + 1f; z < terrain.Bounds.yMax; z += 2f)
            for (float x = terrain.Bounds.xMin + 1f; x < terrain.Bounds.xMax; x += 2f)
            {
                var position = new Vector3(x, terrain.GroundHeight(x, z) + 1.55f, z);
                candidates.Add(new PosterLayout.Anchor(position, Vector3.right, (PosterLayout.AnchorKind)(candidates.Count % 3)));
            }
        }

        [Test]
        public void SameCandidatesProduceTheSameSelectionAndPoses()
        {
            var first = PosterLayout.Select(candidates, terrain);
            var second = PosterLayout.Select(candidates, new RouteTerrain());
            Assert.IsNotEmpty(first);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void EveryPairRespectsTheLargerOfItsTwoSpacingLimits()
        {
            var selected = PosterLayout.Select(candidates, terrain);
            Assert.Greater(selected.Count, 10);
            for (int i = 0; i < selected.Count; i++)
            for (int j = i + 1; j < selected.Count; j++)
            {
                Vector3 a = selected[i].Position, b = selected[j].Position;
                float minimum = Mathf.Max(PosterLayout.MinimumSpacing(a, terrain), PosterLayout.MinimumSpacing(b, terrain));
                Assert.GreaterOrEqual(new Vector2(a.x - b.x, a.z - b.z).magnitude, minimum - 0.0001f);
            }
        }

        [Test]
        public void RoadAndTownEachAllowMorePostersThanARuralStretchOfEqualLength()
        {
            var road = Pair(new Vector3(-100f, 0f, 163f), new Vector3(-94f, 0f, 163f));
            var town = Pair(new Vector3(114f, 0f, 190f), new Vector3(120f, 0f, 190f));
            var along = new Vector3(6f, 0f, 28f).normalized;
            var side = new Vector3(along.z, 0f, -along.x) * 6.5f;
            var rural = Pair(new Vector3(177f, 0f, 34f) + side, new Vector3(177f, 0f, 34f) + side + along * 6f);
            foreach (var anchor in town) Assert.Greater(terrain.RoadDistance(anchor.Position.x, anchor.Position.z), PosterLayout.DenseDistance);
            Assert.AreEqual(2, PosterLayout.Select(road, terrain).Count);
            Assert.AreEqual(2, PosterLayout.Select(town, terrain).Count);
            Assert.AreEqual(1, PosterLayout.Select(rural, terrain).Count);
        }

        [Test]
        public void OnlyDryAnchorsBetweenOneAndSixMetersFromWalkableGroundSurvive()
        {
            var selected = PosterLayout.Select(candidates, terrain);
            foreach (var anchor in selected)
            {
                Vector3 p = anchor.Position;
                Assert.That(terrain.DistanceToRoute(p.x, p.z), Is.InRange(1f, 6f));
                Assert.IsFalse(terrain.IsWater(p.x, p.z));
            }
            var wet = new List<PosterLayout.Anchor>();
            foreach (var anchor in candidates)
            {
                Vector3 p = anchor.Position;
                float distance = terrain.DistanceToRoute(p.x, p.z);
                if (distance >= 1f && distance <= 6f && terrain.IsWater(p.x, p.z)) wet.Add(anchor);
            }
            Assert.IsNotEmpty(wet, "El caso debe aislar el filtro de agua del filtro de distancia.");
            Assert.IsEmpty(PosterLayout.Select(wet, terrain));
            Assert.IsEmpty(PosterLayout.Select(Pair(new Vector3(-100f, 0f, 156f), new Vector3(-100f, 0f, 180f)), terrain));
        }

        [Test]
        public void SelectionHitsTheTotalCapAndNeverExceedsAnyClearingOrViewCap()
        {
            var selected = PosterLayout.Select(candidates, terrain);
            Assert.AreEqual(60, PosterLayout.MaxTotal);
            Assert.AreEqual(12, PosterLayout.MaxPerClearing);
            Assert.AreEqual(PosterLayout.MaxTotal, selected.Count);
            var counts = new int[RouteLayout.Count];
            foreach (var anchor in selected) counts[PosterLayout.ClearingIndex(anchor.Position)]++;
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Assert.LessOrEqual(counts[i], PosterLayout.MaxPerClearing);
                int visible = 0;
                foreach (var anchor in selected) if (PosterLayout.InView(anchor.Position, i)) visible++;
                Assert.LessOrEqual(visible, PosterLayout.MaxPerClearing);
            }
        }

        [Test]
        public void OneClearingCannotUseMoreThanTwelveSlotsEvenWithSpaceForMore()
        {
            var road = new List<PosterLayout.Anchor>();
            for (float x = -104f; x <= 10f; x += 6f) road.Add(At(new Vector3(x, 0f, 163f)));
            foreach (var anchor in road) Assert.AreEqual(6, PosterLayout.ClearingIndex(anchor.Position));
            Assert.AreEqual(PosterLayout.MaxPerClearing, PosterLayout.Select(road, terrain).Count);
        }

        [Test]
        public void VisibleSideFacesTheRouteIncludingTheMaximumRandomTilts()
        {
            foreach (var source in candidates)
            {
                Vector3 p = source.Position;
                if (!PosterLayout.Eligible(p, terrain)) continue;
                Vector3 direction = PosterLayout.RouteDirection(p, terrain);
                Assert.Greater(direction.sqrMagnitude, 0.99f);
                Assert.Less(terrain.DistanceToRoute(p.x + direction.x * 0.05f, p.z + direction.z * 0.05f), terrain.DistanceToRoute(p.x, p.z));
                var anchor = new PosterLayout.Anchor(p, direction, source.Kind, 10f, -6f);
                Assert.LessOrEqual(Vector3.Angle(anchor.Rotation * Vector3.back, direction), 10.001f);
                Assert.LessOrEqual(Vector3.Angle(anchor.Rotation * Vector3.up, Vector3.up), 6.001f);
            }
        }

        [Test]
        public void SymmetricRouteDistancesAreRejectedEvenWithASuppliedFacing()
        {
            var position = new Vector3(71f, 0f, -55f);
            Assert.That(terrain.DistanceToRoute(position.x, position.z),
                Is.InRange(PosterLayout.MinRouteDistance, PosterLayout.MaxRouteDistance));
            Assert.IsTrue(terrain.Bounds.Contains(new Vector2(position.x, position.z)));
            Assert.IsFalse(terrain.IsWater(position.x, position.z));
            Assert.AreEqual(Vector3.zero, PosterLayout.RouteDirection(position, terrain));
            Assert.IsFalse(PosterLayout.Eligible(position, terrain));

            var valid = At(new Vector3(-100f, 0f, 163f));
            var selected = PosterLayout.Select(new[]
            {
                At(position),
                new PosterLayout.Anchor(position, Vector3.right, PosterLayout.AnchorKind.Tree),
                new PosterLayout.Anchor(valid.Position, Vector3.zero, PosterLayout.AnchorKind.Tree),
                valid
            }, terrain);
            CollectionAssert.AreEqual(new[] { valid }, selected);
        }

        [Test]
        public void EmptyInputAndAnchorsWithoutAFacingProduceNoPosters()
        {
            Assert.IsEmpty(PosterLayout.Select(new PosterLayout.Anchor[0], terrain));
            Assert.IsEmpty(PosterLayout.Select(new[] { new PosterLayout.Anchor(new Vector3(-100f, 0f, 163f), Vector3.zero, PosterLayout.AnchorKind.Tree) }, terrain));
        }

        PosterLayout.Anchor At(Vector3 position) => new PosterLayout.Anchor(position, PosterLayout.RouteDirection(position, terrain), PosterLayout.AnchorKind.Tree);
        PosterLayout.Anchor[] Pair(Vector3 first, Vector3 second) => new[] { At(first), At(second) };
    }
}
