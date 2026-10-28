using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class EchoDataTests
    {
        public static EchoData Load(int id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/Data/Echoes/echo-{id}.json");
            Assert.IsNotNull(asset);
            Assert.IsTrue(EchoData.TryLoad(asset.text, out var data, out string error), error);
            return data;
        }

        [TestCase(1, "¡Tyler!")]
        [TestCase(2, null)]
        [TestCase(3, "Cuídame el camino")]
        [TestCase(4, null)]
        [TestCase(5, "¿Tyler?")]
        public void RealEchoesValidateAndContainOnlyScriptedSubtitles(int id, string phrase)
        {
            var data = Load(id);
            Assert.AreEqual(id, data.id);
            Assert.That(data.duration, Is.InRange(10f, 15f));
            Assert.AreEqual(phrase == null ? 0 : 1, data.subtitles.Length);
            if (phrase != null) Assert.AreEqual(phrase, data.subtitles[0].text);
            foreach (var sound in data.sounds)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Sfx/{sound.clip}.wav"), sound.clip);
        }

        [TestCase(9.99f)]
        [TestCase(15.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsDurationOutsideEchoRange(float duration)
        {
            var data = Load(1);
            data.duration = duration;
            Assert.IsFalse(EchoValidation.Check(data, out string error));
            StringAssert.Contains("10 a 15", error);
        }

        [Test] public void RejectsUnorderedAndDuplicateMovementTimes()
        {
            var data = Load(1);
            data.girl.points[2].time = data.girl.points[1].time - 0.1f;
            Assert.IsFalse(EchoValidation.Check(data, out string error));
            StringAssert.Contains("tiempos desordenados", error);
            data.girl.points[2].time = data.girl.points[1].time;
            Assert.IsFalse(EchoValidation.Check(data, out error));
        }

        [Test] public void RejectsUnorderedSoundsAndCopies()
        {
            var data = Load(5);
            data.sounds[1].time = data.sounds[0].time - 0.1f;
            Assert.IsFalse(EchoValidation.Check(data, out string error));
            StringAssert.Contains("tiempos desordenados", error);
            data = Load(2);
            data.sounds[0].copies[1].delay = 0f;
            Assert.IsFalse(EchoValidation.Check(data, out error));
            StringAssert.Contains("retrasos desordenados", error);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("bark_1.wav")]
        [TestCase("../bark_1")]
        public void RejectsMissingOrInvalidClipNames(string clip)
        {
            var data = Load(1);
            data.sounds[0].clip = clip;
            Assert.IsFalse(EchoValidation.Check(data, out string error));
            StringAssert.Contains("nombre del clip", error);
        }

        [Test] public void RejectsInvalidTransformsWindowsAndBlackout()
        {
            var data = Load(1);
            data.girl.points[0].position.x = float.NaN;
            Assert.IsFalse(EchoValidation.Check(data, out _));
            data = Load(3);
            data.subtitles[0].duration = 20f;
            Assert.IsFalse(EchoValidation.Check(data, out _));
            data = Load(5);
            data.blackout.time = 14f;
            Assert.IsFalse(EchoValidation.Check(data, out _));
            data = Load(2);
            data.props[0].appear = data.props[0].disappear + 1f;
            Assert.IsFalse(EchoValidation.Check(data, out _));
        }

        [Test] public void RejectsMalformedJsonWithAnExplanation()
        {
            Assert.IsFalse(EchoData.TryLoad("{invalid", out _, out string error));
            StringAssert.Contains("JSON inválido", error);
        }
    }
}
