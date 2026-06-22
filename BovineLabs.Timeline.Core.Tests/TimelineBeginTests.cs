using BovineLabs.Timeline.Schedular;
using NUnit.Framework;
using Unity.IntegerTime;

namespace BovineLabs.Timeline.Core.Tests
{
    [TestFixture]
    public class TimelineBeginTests
    {
        [Test]
        public void ZeroRemainingActivatesImmediately()
        {
            var advance = TimelineBegin.TryAdvance(DiscreteTime.Zero, new DiscreteTime(0.016f), out var next);

            Assert.IsTrue(advance);
            Assert.AreEqual(DiscreteTime.Zero, next);
        }

        [Test]
        public void NegativeRemainingActivatesImmediately()
        {
            var advance = TimelineBegin.TryAdvance(new DiscreteTime(-1f), new DiscreteTime(0.016f), out var next);

            Assert.IsTrue(advance);
            Assert.AreEqual(DiscreteTime.Zero, next);
        }

        [Test]
        public void RemainingGreaterThanElapsedKeepsCountingDown()
        {
            var remaining = new DiscreteTime(1f);
            var elapsed = new DiscreteTime(0.25f);

            var advance = TimelineBegin.TryAdvance(remaining, elapsed, out var next);

            Assert.IsFalse(advance);
            Assert.AreEqual(remaining - elapsed, next);
        }

        [Test]
        public void ElapsedExactlyConsumesRemainingActivates()
        {
            var remaining = new DiscreteTime(0.5f);

            var advance = TimelineBegin.TryAdvance(remaining, remaining, out var next);

            Assert.IsTrue(advance);
            Assert.AreEqual(DiscreteTime.Zero, next);
        }

        [Test]
        public void ElapsedOvershootsRemainingActivates()
        {
            var remaining = new DiscreteTime(0.5f);
            var elapsed = new DiscreteTime(2f);

            var advance = TimelineBegin.TryAdvance(remaining, elapsed, out var next);

            Assert.IsTrue(advance);
            Assert.AreEqual(remaining - elapsed, next);
        }
    }
}
