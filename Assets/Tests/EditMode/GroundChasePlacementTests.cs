using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame.Tests
{
    public sealed class GroundChasePlacementTests
    {
        [Test]
        public void ChoosesTheShortestCompletePath()
        {
            var candidates = new[]
            {
                Candidate(Vector3.right * 12f, NavMeshPathStatus.PathComplete, 30f),
                Candidate(Vector3.left * 12f, NavMeshPathStatus.PathComplete, 14f),
                Candidate(Vector3.forward * 12f, NavMeshPathStatus.PathComplete, 22f)
            };
            Assert.AreEqual(1, GroundChasePlacement.Choose(candidates, 3, Vector3.zero, 10f));
        }

        [Test]
        public void SkipsIncompletePathsAndCandidatesInsideTheMinimumDistance()
        {
            var candidates = new[]
            {
                Candidate(Vector3.right * 12f, NavMeshPathStatus.PathPartial, 5f),
                Candidate(Vector3.left * 12f, NavMeshPathStatus.PathInvalid, 6f),
                Candidate(new Vector3(9f, 8f, 0f), NavMeshPathStatus.PathComplete, 7f),
                Candidate(Vector3.back * 12f, NavMeshPathStatus.PathComplete, 40f)
            };
            Assert.AreEqual(3, GroundChasePlacement.Choose(candidates, 4, Vector3.zero, 10f));
        }

        [Test]
        public void ReturnsMinusOneWhenNothingQualifies()
        {
            var candidates = new[]
            {
                Candidate(Vector3.right * 12f, NavMeshPathStatus.PathPartial, 5f),
                Candidate(Vector3.left * 4f, NavMeshPathStatus.PathComplete, 6f),
                Candidate(Vector3.back * 12f, NavMeshPathStatus.PathComplete, 3f)
            };
            Assert.AreEqual(-1, GroundChasePlacement.Choose(candidates, 2, Vector3.zero, 10f));
            Assert.AreEqual(-1, GroundChasePlacement.Choose(candidates, 0, Vector3.zero, 10f));
        }

        static GroundChasePlacement.Candidate Candidate(Vector3 position, NavMeshPathStatus status, float length) =>
            new GroundChasePlacement.Candidate { Position = position, Status = status, PathLength = length };
    }
}
