using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class InteractionPromptTests
    {
        [Test]
        public void MemoryPromptNamesTheActualInteractBindingAndRememberAction()
        {
            var prompt = new InteractionPrompt();
            prompt.Consider(new object(), 0f, InteractionKind.Touch);
            Assert.That(prompt.Text, Is.EqualTo("Espacio / Equis: recordar"));
        }

        [Test]
        public void ChoosesNearestAvailableObjectAndItsTextRegardlessOfOrder()
        {
            var prompt = new InteractionPrompt();
            var memory = new object();
            var candle = new object();
            prompt.Consider(candle, 4f, InteractionKind.Kindle);
            prompt.Consider(memory, 1f, InteractionKind.Touch);
            prompt.Consider(new object(), 2f, InteractionKind.Hide);
            prompt.Consider(new object(), 0f, InteractionKind.Collect, false);
            Assert.That(prompt.Target, Is.SameAs(memory));
            Assert.That(prompt.Text, Is.EqualTo(InterfaceTexts.TouchPrompt));
            prompt.Clear();
            prompt.Consider(memory, 1f, InteractionKind.Touch);
            prompt.Consider(candle, 4f, InteractionKind.Kindle);
            Assert.That(prompt.Target, Is.SameAs(memory));
        }

        [TestCase(InteractionKind.Touch, InterfaceTexts.TouchPrompt)]
        [TestCase(InteractionKind.Collect, InterfaceTexts.CollectPrompt)]
        [TestCase(InteractionKind.Hide, InterfaceTexts.HidePrompt)]
        [TestCase(InteractionKind.Leave, InterfaceTexts.LeavePrompt)]
        [TestCase(InteractionKind.Kindle, InterfaceTexts.KindlePrompt)]
        [TestCase(InteractionKind.Recharge, InterfaceTexts.RechargePrompt)]
        public void ReturnsCachedTextForEachAction(InteractionKind kind, string expected)
        {
            var prompt = new InteractionPrompt();
            prompt.Consider(new object(), 0f, kind);
            Assert.That(prompt.Text, Is.EqualTo(expected));
            Assert.That(prompt.Text, Is.SameAs(prompt.Text));
        }

        [Test]
        public void ClearRemovesOldTargetAndTextWhenNothingIsUsable()
        {
            var prompt = new InteractionPrompt();
            prompt.Consider(new object(), 0f, InteractionKind.Hide);
            prompt.Clear();
            prompt.Consider(new object(), 0f, InteractionKind.Kindle, false);
            prompt.Consider(null, 0f, InteractionKind.Touch);
            prompt.Consider(new object(), -1f, InteractionKind.Touch);
            prompt.Consider(new object(), float.NaN, InteractionKind.Touch);
            Assert.That(prompt.Target, Is.Null);
            Assert.That(prompt.Text, Is.Empty);
        }

        [Test]
        public void EqualDistanceKeepsFirstTargetToMatchHideBeforeCollect()
        {
            var prompt = new InteractionPrompt();
            var hide = new object();
            prompt.Consider(hide, 0f, InteractionKind.Hide);
            prompt.Consider(new object(), 0f, InteractionKind.Touch);
            Assert.That(prompt.Target, Is.SameAs(hide));
        }

        [Test]
        public void FamilyRechargeDoesNotAdvertiseAnInputThatDoesNothing()
        {
            var prompt = new InteractionPrompt();
            prompt.Consider(new object(), 0f, InteractionKind.Recharge);
            Assert.That(prompt.Text, Does.Not.Contain(GameInput.InteractBinding));
            Assert.That(prompt.Text, Does.Contain("automático"));
        }

        [TestCase(false, 100f, CandleKind.Kindled, false, false, false)]
        [TestCase(true, 19f, CandleKind.Kindled, false, false, false)]
        [TestCase(true, 20f, CandleKind.Kindled, false, true, false)]
        [TestCase(true, 20f, CandleKind.Kindled, true, false, false)]
        [TestCase(true, 100f, CandleKind.Family, false, false, false)]
        [TestCase(true, 20f, CandleKind.Kindled, false, false, true)]
        public void KindlePromptUsesActualPowerEnergyWeatherAndCandleRules(bool unlocked, float startEnergy,
            CandleKind kind, bool lit, bool raining, bool expected)
        {
            var powers = new PowerSet();
            if (unlocked) powers.Unlock(Power.Candles);
            var energy = new SoulEnergy(startEnergy);
            var candle = new Candle(kind, Vector3.zero, lit);
            var prompt = new InteractionPrompt();
            prompt.Consider(candle, 0f, InteractionKind.Kindle,
                PowerRules.CanLightCandle(powers, energy, candle, raining));
            Assert.That(prompt.Target != null, Is.EqualTo(expected));
            Assert.That(energy.Current, Is.EqualTo(startEnergy));
            Assert.That(candle.IsLit, Is.EqualTo(lit || kind == CandleKind.Family));
            Assert.That(PowerRules.TryLightCandle(powers, energy, candle, raining), Is.EqualTo(expected));
        }
    }
}
