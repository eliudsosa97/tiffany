using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class MemoryProgressTests
    {
        [Test]
        public void PreservesOrderSkipsInvalidIdsAndCountsEachMemoryOnce()
        {
            var ids = new[] { 3, 0, -1, 1, 3, 2, 1 };
            var progress = new MemoryProgress(ids);
            ids[0] = 9;
            Assert.AreEqual(0, progress.FoundCount);
            Assert.IsFalse(progress.AllFound);
            Assert.IsTrue(progress.TryGetNext(out int next));
            Assert.AreEqual(3, next);
            Assert.IsTrue(progress.MarkFound(2));
            Assert.IsTrue(progress.IsFound(2));
            Assert.IsFalse(progress.MarkFound(2));
            Assert.IsFalse(progress.IsFound(3));
            Assert.AreEqual(1, progress.FoundCount);
            Assert.IsTrue(progress.TryGetNext(out next));
            Assert.AreEqual(3, next);
            Assert.IsTrue(progress.MarkFound(3));
            Assert.IsTrue(progress.TryGetNext(out next));
            Assert.AreEqual(1, next);
            Assert.IsTrue(progress.MarkFound(1));
            Assert.AreEqual(3, progress.FoundCount);
            Assert.IsTrue(progress.AllFound);
            Assert.IsFalse(progress.TryGetNext(out next));
            Assert.AreEqual(0, next);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(5)]
        public void RejectsIdsOutsideOrder(int id)
        {
            var progress = new MemoryProgress(new[] { 1, 2 });
            Assert.IsFalse(progress.MarkFound(id));
            Assert.IsFalse(progress.IsFound(id));
            Assert.AreEqual(0, progress.FoundCount);
        }

        [Test]
        public void EmptyOrderIsCompleteAndNullOrderIsRejected()
        {
            var progress = new MemoryProgress(new[] { 0, -2 });
            Assert.IsTrue(progress.AllFound);
            Assert.IsFalse(progress.TryGetNext(out _));
            Assert.IsTrue(new MemoryProgress(Array.Empty<int>()).AllFound);
            Assert.Throws<ArgumentNullException>(() => new MemoryProgress(null));
        }

        [TestCase(0f, 0f, true)]
        [TestCase(0.5f, 0.5f, true)]
        [TestCase(1f, 1f, true)]
        [TestCase(5f, 1f, true)]
        [TestCase(9f, 1f, true)]
        [TestCase(9.5f, 0.5f, true)]
        [TestCase(10f, 0f, false)]
        [TestCase(1000f, 0f, false)]
        public void EchoFadesInHoldsAndFadesOut(float dt, float weight, bool active)
        {
            var echo = new MemoryEcho();
            echo.Tick(1f);
            Assert.IsFalse(echo.Active);
            Assert.AreEqual(0f, echo.Weight);
            echo.Start();
            echo.Tick(dt);
            Assert.AreEqual(active, echo.Active);
            Assert.AreEqual(weight, echo.Weight, 0.0001f);
        }

        [TestCase(0.5f)]
        [TestCase(5f)]
        [TestCase(9.5f)]
        [TestCase(10f)]
        public void RestartResetsEchoAndTicksAccumulate(float elapsed)
        {
            var echo = new MemoryEcho();
            echo.Start();
            echo.Tick(elapsed);
            echo.Start();
            Assert.IsTrue(echo.Active);
            Assert.AreEqual(0f, echo.Weight);
            echo.Tick(0.25f);
            echo.Tick(0.25f);
            Assert.AreEqual(0.5f, echo.Weight);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void InvalidTimeDoesNotAdvanceEcho(float dt)
        {
            var echo = new MemoryEcho();
            echo.Start();
            echo.Tick(0.5f);
            echo.Tick(dt);
            Assert.AreEqual(0.5f, echo.Weight);
        }
    }
}
