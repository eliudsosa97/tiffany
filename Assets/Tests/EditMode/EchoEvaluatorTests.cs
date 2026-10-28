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

        static EchoSound Bark() => new EchoSound
        {
            clip = "bark", time = 1f, duration = 3f, pitch = 1f, volume = 0.8f,
            panStereo = 0.1f, fadeIn = 0.5f, fadeOut = 0.5f,
            copies = new[] { new EchoSoundCopy { delay = 1f, pitch = 1.5f, volume = 0.6f, panStereo = -0.7f } }
        };

        [Test] public void DelayedCopyIsScheduledOnceAndStopsAtItsPitchedDuration()
        {
            var bark = Bark();
            var evaluator = new EchoAudioEvaluator(new[] { bark }, new[] { 3f }, 2);
            evaluator.Evaluate(0f, 0.9f);
            Assert.IsFalse(evaluator[0].Play);
            Assert.IsFalse(evaluator[1].Play);
            evaluator.Evaluate(0.9f, 1.25f);
            Assert.AreEqual(1f, evaluator[0].Pitch);
            Assert.AreEqual(0.1f, evaluator[0].PanStereo);
            Assert.AreEqual(0.4f, evaluator[0].Volume, 0.0001f);
            var copy = evaluator[1];
            Assert.IsTrue(copy.Play);
            Assert.IsTrue(copy.Active);
            Assert.AreEqual(0, copy.SoundIndex);
            Assert.AreEqual(0.75f, copy.Delay);
            Assert.AreEqual(0f, copy.ClipTime);
            Assert.AreEqual(2f, copy.Duration);
            Assert.AreEqual(1.5f, copy.Pitch);
            Assert.AreEqual(-0.7f, copy.PanStereo);
            Assert.AreEqual(0f, copy.Volume);
            evaluator.Evaluate(1.25f, 2.25f);
            Assert.IsFalse(evaluator[1].Play);
            Assert.IsTrue(evaluator[1].Active);
            Assert.AreEqual(0.3f, evaluator[1].Volume, 0.0001f);
            evaluator.Evaluate(2.25f, 2.25f);
            Assert.IsFalse(evaluator[1].Play);
            evaluator.Evaluate(2.25f, 3.75f);
            Assert.AreEqual(0.3f, evaluator[1].Volume, 0.0001f);
            evaluator.Evaluate(3.75f, 4f);
            Assert.IsTrue(evaluator[1].Stop);
            Assert.IsFalse(evaluator[1].Active);
            evaluator.Evaluate(4f, 4.1f);
            Assert.IsFalse(evaluator[1].Stop);
            evaluator.Reset();
            evaluator.Evaluate(float.NegativeInfinity, 1f);
            Assert.IsTrue(evaluator[1].Play);
            Assert.AreEqual(1f, evaluator[1].Delay);
        }

        [TestCase(3f, 1.5f, 1, 0.75f)]
        [TestCase(1f, 0.5f, 0, 0.25f)]
        public void LongFrameSkipStartsDelayedCopyAtItsPitchedClipOffset(float clipLength, float pitch, int slot, float offset)
        {
            var bark = Bark();
            bark.copies[0].pitch = pitch;
            var evaluator = new EchoAudioEvaluator(new[] { bark }, new[] { clipLength }, 2);
            evaluator.Evaluate(0f, 2.5f);
            var copy = evaluator[slot];
            Assert.IsTrue(copy.Play);
            Assert.AreEqual(0f, copy.Delay);
            Assert.AreEqual(offset, copy.ClipTime);
            Assert.AreEqual(2f, copy.Duration);
            Assert.AreEqual(pitch, copy.Pitch);
            Assert.AreEqual(0.6f, copy.Volume);
            Assert.AreEqual(-0.7f, copy.PanStereo);
        }

        [TestCase(4f)]
        [TestCase(5f)]
        public void FrameSkipPastDelayedCopyEndDoesNotStartIt(float time)
        {
            var evaluator = new EchoAudioEvaluator(new[] { Bark() }, new[] { 3f }, 2);
            evaluator.Evaluate(0f, time);
            for (int i = 0; i < 2; i++)
            {
                Assert.IsFalse(evaluator[i].Play);
                Assert.IsFalse(evaluator[i].Active);
            }
        }

        [Test] public void FullPoolReplacesOldestVoiceRatherThanAlwaysReplacingFirstSlot()
        {
            var sounds = new EchoSound[4];
            for (int i = 0; i < sounds.Length; i++)
                sounds[i] = new EchoSound { time = i * 0.5f, duration = 4f, pitch = 1f, volume = 1f };
            var evaluator = new EchoAudioEvaluator(sounds, new[] { 4f, 4f, 4f, 4f }, 2);
            evaluator.Evaluate(float.NegativeInfinity, 0.5f);
            evaluator.Evaluate(0.5f, 1f);
            Assert.IsTrue(evaluator[0].Stop);
            Assert.IsTrue(evaluator[0].Play);
            Assert.AreEqual(2, evaluator[0].SoundIndex);
            Assert.AreEqual(1, evaluator[1].SoundIndex);
            Assert.IsFalse(evaluator[1].Play);
            Assert.IsFalse(evaluator[1].Stop);
            evaluator.Evaluate(1f, 1.5f);
            Assert.IsTrue(evaluator[1].Stop);
            Assert.IsTrue(evaluator[1].Play);
            Assert.AreEqual(3, evaluator[1].SoundIndex);
            Assert.AreEqual(2, evaluator[0].SoundIndex);
            Assert.IsFalse(evaluator[0].Play);
        }

        [Test] public void ReusesExpiredSlotBeforeReplacingAnActiveVoice()
        {
            var sounds = new[]
            {
                new EchoSound { time = 0f, duration = 4f, pitch = 1f, volume = 1f },
                new EchoSound { time = 0.5f, duration = 0.5f, pitch = 1f, volume = 1f },
                new EchoSound { time = 1f, duration = 1f, pitch = 1f, volume = 1f }
            };
            var evaluator = new EchoAudioEvaluator(sounds, new[] { 4f, 4f, 4f }, 2);
            evaluator.Evaluate(float.NegativeInfinity, 0.5f);
            evaluator.Evaluate(0.5f, 1f);
            Assert.AreEqual(0, evaluator[0].SoundIndex);
            Assert.IsFalse(evaluator[0].Stop);
            Assert.IsFalse(evaluator[0].Play);
            Assert.AreEqual(2, evaluator[1].SoundIndex);
            Assert.IsTrue(evaluator[1].Stop);
            Assert.IsTrue(evaluator[1].Play);
        }

        [TestCase(2)]
        [TestCase(5)]
        public void SoundAndCopyOrderDoesNotChangeAudioPlan(int voiceCount)
        {
            var bark = Bark();
            bark.copies = new[]
            {
                new EchoSoundCopy { delay = 0.5f, pitch = 1.5f, volume = 0.6f, panStereo = -0.7f },
                new EchoSoundCopy { delay = 1.5f, pitch = 0.5f, volume = 0.3f, panStereo = 0.7f }
            };
            var reversedBark = Bark();
            reversedBark.copies = new[] { bark.copies[1], bark.copies[0] };
            var laugh = new EchoSound { clip = "laugh", time = 0f, duration = 2f, pitch = 1f, volume = 1f };
            var whisper = new EchoSound { clip = "whisper", time = 1f, duration = 4f, pitch = 1f, volume = 0.5f };
            var ordered = new[] { laugh, bark, whisper };
            var unordered = new[] { whisper, reversedBark, laugh };
            var first = new EchoAudioEvaluator(ordered, new[] { 2f, 3f, 4f }, voiceCount);
            var second = new EchoAudioEvaluator(unordered, new[] { 4f, 3f, 2f }, voiceCount);
            float previous = float.NegativeInfinity;
            foreach (float time in new[] { 0f, 0.5f, 1.25f, 2.75f, 4f, 6f })
            {
                first.Evaluate(previous, time);
                second.Evaluate(previous, time);
                for (int i = 0; i < voiceCount; i++)
                {
                    var left = first[i];
                    var right = second[i];
                    Assert.AreEqual((left.Active, left.Play, left.Stop), (right.Active, right.Play, right.Stop));
                    if (!left.Active) continue;
                    Assert.AreEqual(ordered[left.SoundIndex].clip, unordered[right.SoundIndex].clip);
                    Assert.AreEqual((left.Delay, left.ClipTime, left.Duration), (right.Delay, right.ClipTime, right.Duration));
                    Assert.AreEqual((left.Pitch, left.Volume, left.PanStereo), (right.Pitch, right.Volume, right.PanStereo));
                }
                previous = time;
            }
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
