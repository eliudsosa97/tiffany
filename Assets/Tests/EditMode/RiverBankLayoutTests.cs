using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class RiverBankLayoutTests
    {
        const float Gap = RiverPieces.BankLength;

        [Test]
        public void PiecesCoverBothEdgesExceptTheCrossingGap()
        {
            var terrain = new RouteTerrain();
            var pieces = RiverBankLayout.Pieces(terrain, RiverPieces.BankLength, Gap);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float edge = 0f, covered = 0f;
                for (int i = 1; i < terrain.RiverPointCount; i++)
                    edge += Vector2.Distance(terrain.RiverBankPoint(i - 1, sign), terrain.RiverBankPoint(i, sign));
                foreach (var piece in pieces)
                    if (piece.Mirror == sign > 0) covered += piece.Length;
                Assert.AreEqual(edge - Gap, covered, 0.01f, $"orilla {sign}");
            }
            foreach (var piece in pieces)
                Assert.That(piece.Length, Is.InRange(RiverPieces.BankLength * 0.7f, RiverPieces.BankLength * 1.3f));
        }

        [Test]
        public void NoPieceEntersTheCrossingAndEachFacesTheWater()
        {
            var terrain = new RouteTerrain();
            var crossing = new Vector2(terrain.Crossing.x, terrain.Crossing.z);
            foreach (var piece in RiverBankLayout.Pieces(terrain, RiverPieces.BankLength, Gap))
            {
                var end = piece.Start + piece.Direction * piece.Length;
                Assert.GreaterOrEqual(RouteTerrain.SegmentDistance(crossing, piece.Start, end), Gap * 0.5f - 0.01f);
                Assert.AreEqual(RouteTerrain.RiverWidth * 0.5f, terrain.RiverDistance(piece.Middle.x, piece.Middle.y), 0.05f);
                var water = new Vector2(piece.Direction.y, -piece.Direction.x) * (piece.Mirror ? -1f : 1f);
                var wet = piece.Middle + water;
                var dry = piece.Middle - water;
                Assert.Less(terrain.RiverDistance(wet.x, wet.y), terrain.RiverDistance(dry.x, dry.y));
            }
        }

        [Test]
        public void LayBanksPlacesStraightBanksAndPlantsWithoutColliders()
        {
            var terrain = new RouteTerrain();
            var parent = new GameObject("Banks").transform;
            try
            {
                RiverPieces.LayBanks(parent, terrain);
                var pieces = RiverBankLayout.Pieces(terrain, RiverPieces.BankLength, Gap);
                int banks = 0, plants = 0;
                foreach (Transform child in parent)
                {
                    if (child.name == "river-bed-straight")
                    {
                        var piece = pieces[banks++];
                        Assert.AreEqual(piece.Mirror ? -1f : 1f, child.localScale.x);
                        Assert.AreEqual(piece.Length / RiverPieces.BankLength, child.localScale.z, 0.001f);
                        Assert.AreEqual(terrain.Crossing.y, child.localPosition.y, 0.001f);
                    }
                    if (child.name == "plant-tussock") plants++;
                }
                Assert.AreEqual(pieces.Count, banks);
                Assert.AreEqual((pieces.Count + 1) / 3, plants);
                Assert.IsEmpty(parent.GetComponentsInChildren<Collider>(true));
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }
    }
}
