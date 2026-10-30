using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class SaveRulesTests
    {
        static readonly int[] MemoryIds = { 1, MemoryProgress.BallId, 2, 3, 4, 5 };
        static readonly string[] CandleIds = { "cemetery", "tracks" };

        static ChallengeRules[] Challenges() => new[]
        {
            new ChallengeRules(ChallengeKind.Hut), new ChallengeRules(ChallengeKind.Candles),
            new ChallengeRules(ChallengeKind.Vanish), new ChallengeRules(ChallengeKind.Distract)
        };

        static SaveData Valid() => new SaveData
        {
            version = SaveRules.Version, memories = new[] { 1, MemoryProgress.BallId, 2 },
            candleId = "tracks", chapter = Chapter.TrainTracks, energy = 37f
        };

        [Test]
        public void CaptureRoundTripsMemoriesChallengesCheckpointChapterAndEnergyThroughJson()
        {
            var progress = new MemoryProgress(MemoryIds);
            progress.MarkFound(1);
            progress.MarkFound(MemoryProgress.BallId);
            progress.MarkFound(2);
            var challenges = Challenges();
            challenges[0].MarkCompleted();
            challenges[1].MarkCompleted();
            var data = SaveRules.Capture(progress, MemoryIds, "tracks", Chapter.TrainTracks, new SoulEnergy(37f), challenges);
            progress.MarkFound(3);
            challenges[3].MarkCompleted();
            Assert.IsTrue(SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out var restored));
            Assert.AreEqual(SaveRules.Version, restored.version);
            CollectionAssert.AreEqual(new[] { 1, MemoryProgress.BallId, 2 }, restored.memories);
            Assert.AreEqual("tracks", restored.candleId);
            Assert.AreEqual(Chapter.TrainTracks, restored.chapter);
            Assert.AreEqual(37f, restored.energy);
            Assert.AreEqual((1 << (int)ChallengeKind.Candles) | (1 << (int)ChallengeKind.Hut), restored.completedChallenges);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{")]
        [TestCase("broken json")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("{\"version\":1,\"memories\":\"invalid\"}")]
        public void BrokenOrIncompleteJsonIsIgnored(string json)
        {
            Assert.IsFalse(SaveRules.TryRead(json, MemoryIds, CandleIds, out var data));
            Assert.IsNull(data);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void DifferentVersionIsIgnored(int version)
        {
            var data = Valid();
            data.version = version;
            Assert.IsFalse(SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out _));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(6)]
        [TestCase(99)]
        [TestCase(101)]
        public void UnknownMemoryIsIgnored(int id)
        {
            var data = Valid();
            data.memories = new[] { id };
            Assert.IsFalse(SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out _));
        }

        [Test]
        public void DuplicateOrMissingSceneMemoriesAreIgnored()
        {
            var data = Valid();
            data.memories = new[] { 1, 1 };
            Assert.IsFalse(SaveRules.IsValid(data, MemoryIds, CandleIds));
            data = Valid();
            Assert.IsFalse(SaveRules.IsValid(data, new[] { 1, 2 }, CandleIds));
            data.memories = null;
            Assert.IsFalse(SaveRules.IsValid(data, MemoryIds, CandleIds));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("removed candle")]
        public void UnknownCandleIsIgnored(string id)
        {
            var data = Valid();
            data.candleId = id;
            Assert.IsFalse(SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out _));
        }

        [TestCase(-1f)]
        [TestCase(101f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidEnergyIsIgnored(float energy)
        {
            var data = Valid();
            data.energy = energy;
            Assert.IsFalse(SaveRules.IsValid(data, MemoryIds, CandleIds));
        }

        [TestCase(-1)]
        [TestCase(6)]
        public void UnknownChapterIsIgnored(int chapter)
        {
            var data = Valid();
            data.chapter = (Chapter)chapter;
            Assert.IsFalse(SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out _));
        }

        [TestCase(-1)]
        [TestCase(16)]
        [TestCase(31)]
        [TestCase(int.MinValue)]
        public void UnknownChallengeBitsAreIgnored(int mask)
        {
            var data = Valid();
            data.completedChallenges = mask;
            Assert.IsFalse(SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out var restored));
            Assert.IsNull(restored);
        }

        [TestCase(3, 0, true)]
        [TestCase(3, 1, true)]
        [TestCase(3, 2, true)]
        [TestCase(4, 1, true)]
        [TestCase(4, 2, true)]
        [TestCase(4, 3, true)]
        [TestCase(5, 7, true)]
        [TestCase(5, 11, true)]
        [TestCase(5, 13, true)]
        [TestCase(5, 14, true)]
        [TestCase(5, 15, true)]
        public void MemoriesLoadWhateverChallengesAreDone(int memoryId, int mask, bool valid)
        {
            var data = Valid();
            data.memories = new[] { memoryId };
            data.completedChallenges = mask;
            if (memoryId == 5) data.chapter = Chapter.Return;
            Assert.AreEqual(valid, SaveRules.TryRead(JsonUtility.ToJson(data), MemoryIds, CandleIds, out _));
        }

        [Test]
        public void ReturnRequiresLastMemoryAndLastMemoryRequiresReturn()
        {
            var data = Valid();
            data.chapter = Chapter.Return;
            Assert.IsFalse(SaveRules.IsValid(data, MemoryIds, CandleIds));
            data.memories = new[] { 5 };
            data.completedChallenges = 15;
            Assert.IsTrue(SaveRules.IsValid(data, MemoryIds, CandleIds));
            data.chapter = Chapter.WitchHut;
            Assert.IsFalse(SaveRules.IsValid(data, MemoryIds, CandleIds));
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(100f)]
        public void ApplyRestoresProgressPowersChallengesCheckpointAndChapterWithHalfEnergy(float savedEnergy)
        {
            var data = Valid();
            data.energy = savedEnergy;
            data.completedChallenges = (1 << (int)ChallengeKind.Candles) | (1 << (int)ChallengeKind.Hut);
            var progress = new MemoryProgress(MemoryIds);
            progress.MarkFound(4);
            var powers = new PowerSet();
            powers.Unlock(Power.Manifest);
            var respawn = new RespawnPoint(Vector3.zero);
            var candle = new Candle(CandleKind.Family, new Vector3(10f, 0f, 20f));
            var energy = new SoulEnergy();
            var challenges = Challenges();
            var chapter = SaveRules.Apply(data, progress, powers, new[] { Power.Poltergeist }, respawn, candle, energy, challenges);
            Assert.AreEqual(3, progress.FoundCount);
            Assert.IsTrue(progress.IsFound(MemoryProgress.BallId));
            Assert.IsTrue(progress.IsFound(1) && progress.IsFound(2));
            Assert.IsFalse(progress.IsFound(4));
            Assert.IsTrue(powers.Has(Power.Light) && powers.Has(Power.Candles) && powers.Has(Power.Poltergeist));
            Assert.IsFalse(powers.Has(Power.Vanish) || powers.Has(Power.Manifest));
            Assert.AreEqual(candle.Position, respawn.Position);
            Assert.AreEqual(50f, energy.Current);
            Assert.AreEqual(Chapter.TrainTracks, chapter);
            foreach (var challenge in challenges)
                Assert.AreEqual(challenge.Kind == ChallengeKind.Candles || challenge.Kind == ChallengeKind.Hut, challenge.Completed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LightRestoresOnlyWithItsMemoryWhenStartingWithoutPowers(bool remembered)
        {
            var data = Valid();
            data.memories = remembered ? new[] { 1 } : System.Array.Empty<int>();
            var powers = new PowerSet();
            SaveRules.Apply(data, new MemoryProgress(MemoryIds), powers, System.Array.Empty<Power>(),
                new RespawnPoint(Vector3.zero), new Candle(CandleKind.Family, Vector3.zero), new SoulEnergy(), Challenges());
            Assert.AreEqual(remembered, powers.Has(Power.Light));
        }

        [TestCase("-playthrough", true)]
        [TestCase("-autocapture", true)]
        [TestCase("-echodemo", true)]
        [TestCase("-screen-width", false)]
        public void AutomaticRunsDisablePlayerPersistence(string argument, bool disabled) =>
            Assert.AreEqual(disabled, SaveRules.IsAutomaticRun(new[] { "Tiffany", argument }));
    }
}
