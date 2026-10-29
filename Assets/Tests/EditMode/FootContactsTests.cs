using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class FootContactsTests
    {
        static readonly Vector2 Phases = new Vector2(0f, 0.5f);

        [TestCase(0f, 0.5f, 0f, 0.5f)]
        [TestCase(0.2f, 0.7f, 0.8f, 0.3f)]
        public void ReversedPlaybackReflectsContactsAndCountsDecreasingAnimationTime(float left, float right, float reversedLeft, float reversedRight)
        {
            var phases = FootContacts.Reversed(new Vector2(left, right));
            Assert.AreEqual(new Vector2(reversedLeft, reversedRight), phases);
            var contacts = new FootContacts();
            float first = Mathf.Min(reversedLeft > 0f ? reversedLeft : 1f, reversedRight);
            float normalizedTime = -first + 0.01f;
            Assert.AreEqual(0, contacts.Advance(1, -0f, phases));
            Assert.AreEqual(0, contacts.Advance(1, -normalizedTime, phases));
            normalizedTime -= 0.02f;
            Assert.AreEqual(1, contacts.Advance(1, -normalizedTime, phases));
            Assert.AreEqual(0, contacts.Advance(1, -normalizedTime, phases));
            normalizedTime = -2f;
            Assert.AreEqual(3, contacts.Advance(1, -normalizedTime, phases));
        }

        [Test]
        public void FirstStepWaitsForAContactAndNeverRepeatsTheSameSample()
        {
            var contacts = new FootContacts();
            Assert.AreEqual(0, contacts.Advance(1, 0f, Phases));
            Assert.AreEqual(0, contacts.Advance(1, 0.49f, Phases));
            Assert.AreEqual(1, contacts.Advance(1, 0.5f, Phases));
            Assert.AreEqual(0, contacts.Advance(1, 0.5f, Phases));
            Assert.AreEqual(0, contacts.Advance(1, 0.99f, Phases));
            Assert.AreEqual(1, contacts.Advance(1, 1f, Phases));
        }

        [Test]
        public void WrapAndLongFramesPublishEveryCrossedContactWithoutBacklog()
        {
            var contacts = new FootContacts();
            contacts.Advance(1, 0.9f, Phases);
            Assert.AreEqual(1, contacts.Advance(1, 1.01f, Phases));
            Assert.AreEqual(5, contacts.Advance(1, 3.6f, Phases));
            Assert.AreEqual(0, contacts.Advance(1, 3.61f, Phases));
        }

        [Test]
        public void ContactPhasesComeFromTheClipAndNotAHardcodedHalfCycle()
        {
            var contacts = new FootContacts();
            var phases = new Vector2(0.2f, 0.7f);
            Assert.AreEqual(0, contacts.Advance(1, 0.19f, phases));
            Assert.AreEqual(1, contacts.Advance(1, 0.21f, phases));
            Assert.AreEqual(0, contacts.Advance(1, 0.5f, phases));
            Assert.AreEqual(1, contacts.Advance(1, 0.71f, phases));
        }

        [Test]
        public void CrossfadeDestinationBecomingCurrentDoesNotDuplicateContacts()
        {
            var contacts = new FootContacts();
            Assert.AreEqual(1, contacts.Advance(1, 0.51f, Phases));
            Assert.AreEqual(0, contacts.Advance(1, 0.6f, Phases));
            Assert.AreEqual(0, contacts.Advance(2, 0.1f, Phases));
            Assert.AreEqual(1, contacts.Advance(2, 0.51f, Phases));
        }

        [Test]
        public void RestartAndRewindDoNotReplayPastContacts()
        {
            var contacts = new FootContacts();
            contacts.Advance(1, 4.4f, Phases);
            Assert.AreEqual(0, contacts.Advance(1, 0.1f, Phases));
            Assert.AreEqual(1, contacts.Advance(1, 0.5f, Phases));
            contacts = default;
            Assert.AreEqual(0, contacts.Advance(1, 0.2f, Phases));
        }
    }
}
