using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class EchoEvaluatorTests
    {
        static EchoData Data() => new EchoData
        {
            id = 1,
            duration = 12f,
            girl = new EchoTrack
            {
                disappear = 10f,
                points = new[]
                {
                    new EchoPoint { time = 1f, rotation = new Vector3(0f, 350f, 0f) },
                    new EchoPoint { time = 5f, position = new Vector3(4f, 2f, -2f), rotation = new Vector3(0f, 10f, 0f), scale = Vector3.one * 0.5f }
                }
            },
            tyler = new EchoTrack { appear = 2f, disappear = 8f, position = Vector3.right },
            subtitles = new[] { new EchoSubtitle { time = 2f, duration = 2f, text = "¡Tyler!" } },
            sounds = new[] { new EchoSound { time = 0f }, new EchoSound { time = 2f }, new EchoSound { time = 2f }, new EchoSound { time = 5f } },
            props = Array.Empty<EchoProp>(),
            blackout = new EchoBlackout { time = 9f, duration = 0.5f }
        };

        [Test] public void InterpolatesPositionScaleAndRotationAndHoldsEndpoints()
        {
            var data = Data();
            var evaluator = new EchoEvaluator(data);
            Assert.AreEqual(Vector3.zero, evaluator.Evaluate(0f).Girl.Position);
            var pose = evaluator.Evaluate(3f).Girl;
            Assert.AreEqual(new Vector3(2f, 1f, -1f), pose.Position);
            Assert.AreEqual(Vector3.one * 0.75f, pose.Scale);
            Assert.Less(Quaternion.Angle(Quaternion.identity, pose.Rotation), 0.01f);
            Assert.AreEqual(new Vector3(4f, 2f, -2f), evaluator.Evaluate(11f).Girl.Position);
            Assert.IsFalse(evaluator.Evaluate(10f).Girl.Visible);
            Assert.IsFalse(evaluator.Evaluate(1.9f).Tyler.Visible);
            Assert.IsTrue(evaluator.Evaluate(2f).Tyler.Visible);
            Assert.IsFalse(evaluator.Evaluate(8f).Tyler.Visible);
        }

        [TestCase(1.99f, -1)]
        [TestCase(2f, 0)]
        [TestCase(3.99f, 0)]
        [TestCase(4f, -1)]
        public void SelectsSubtitleOnlyInsideItsWindow(float time, int expected) =>
            Assert.AreEqual(expected, new EchoEvaluator(Data()).Evaluate(time).Subtitle);

        [Test] public void EmitsCrossedSoundsOnceIncludingSimultaneousEventsAndFrameSkips()
        {
            var evaluator = new EchoEvaluator(Data());
            var first = evaluator.Evaluate(0f);
            Assert.AreEqual((0, 1), (first.FirstSound, first.SoundCount));
            Assert.AreEqual(0, evaluator.Evaluate(0f).SoundCount);
            Assert.AreEqual(0, evaluator.Evaluate(1.99f).SoundCount);
            var crossed = evaluator.Evaluate(2f);
            Assert.AreEqual((1, 2), (crossed.FirstSound, crossed.SoundCount));
            Assert.AreEqual(0, evaluator.Evaluate(2f).SoundCount);
            Assert.AreEqual(0, evaluator.Evaluate(1f).SoundCount);
            var skipped = evaluator.Evaluate(8f);
            Assert.AreEqual((3, 1), (skipped.FirstSound, skipped.SoundCount));
            Assert.AreEqual(0, evaluator.Evaluate(12f).SoundCount);
            evaluator.Reset();
            Assert.AreEqual(1, evaluator.Evaluate(0f).SoundCount);
        }

        [TestCase(8.9f, 0f)]
        [TestCase(9f, 0f)]
        [TestCase(9.25f, 0.5f)]
        [TestCase(9.5f, 1f)]
        [TestCase(11f, 1f)]
        public void FadesToBlackAndHoldsUntilPlaybackEnds(float time, float expected) =>
            Assert.AreEqual(expected, new EchoEvaluator(Data()).Evaluate(time).BlackOpacity, 0.0001f);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RealEchoesRemainTransparentAtMidpoint(int id)
        {
            var data = EchoDataTests.Load(id);
            var evaluator = new EchoEvaluator(data);
            Assert.AreEqual(0f, evaluator.Evaluate(0f).BlackOpacity);
            Assert.AreEqual(0f, evaluator.Evaluate(data.duration * 0.5f).BlackOpacity);
        }

        [Test] public void FifthRealEchoReachesBlackAfterItsCut()
        {
            var data = EchoDataTests.Load(5);
            float afterCut = data.blackout.time + data.blackout.duration + 0.1f;
            Assert.Less(afterCut, data.duration);
            Assert.AreEqual(1f, new EchoEvaluator(data).Evaluate(afterCut).BlackOpacity);
        }

        [TestCase(0f)]
        [TestCase(5f)]
        [TestCase(10f)]
        public void NullAndZeroDurationBlackoutsStayTransparent(float time)
        {
            Assert.AreEqual(0f, EchoEvaluator.BlackOpacity(null, time));
            Assert.AreEqual(0f, EchoEvaluator.BlackOpacity(new EchoBlackout(), time));
            Assert.AreEqual(0f, EchoEvaluator.BlackOpacity(new EchoBlackout { time = 5f }, time));
        }

        [TestCase(-0.1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1.75f, 0.5f)]
        [TestCase(2f, 0f)]
        public void AudioEnvelopeFadesWithoutFilters(float time, float expected) =>
            Assert.AreEqual(expected, EchoEvaluator.AudioEnvelope(time, 2f, 0.5f, 0.5f), 0.0001f);

        [Test] public void FifthEchoOverlapsBarkReleaseAndLaughAttack()
        {
            var data = EchoDataTests.Load(5);
            var bark = data.sounds[0];
            var laugh = data.sounds[1];
            Assert.AreEqual("woman_laugh", laugh.clip);
            float barkDuration = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Sfx/{bark.clip}.wav").length / bark.pitch;
            float first = laugh.time + 0.02f;
            float later = first + 0.15f;
            float Bark(float time) => EchoEvaluator.AudioEnvelope(time - bark.time, barkDuration, bark.fadeIn, bark.fadeOut);
            float Laugh(float time) => EchoEvaluator.AudioEnvelope(time - laugh.time, laugh.duration, laugh.fadeIn, laugh.fadeOut);
            Assert.Greater(Bark(first), Bark(later));
            Assert.Greater(Bark(later), 0f);
            Assert.Less(Laugh(first), Laugh(later));
        }
    }
}
