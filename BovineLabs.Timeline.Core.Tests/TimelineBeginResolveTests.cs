using BovineLabs.Timeline.Core.Data.Builders;
using NUnit.Framework;
using Unity.IntegerTime;

namespace BovineLabs.Timeline.Core.Tests
{
    public class TimelineBeginResolveTests
    {
        [Test]
        public void Enabled_NoAuthoring_FollowsPlayOnAwake()
        {
            var off = new TimelineBeginResolve { HasAuthoring = false, DirectorPlayOnAwake = false };
            var on = new TimelineBeginResolve { HasAuthoring = false, DirectorPlayOnAwake = true };

            Assert.IsFalse(off.Enabled);
            Assert.IsTrue(on.Enabled);
        }

        [Test]
        public void Enabled_WithAuthoring_FollowsAuthoringOnLoad()
        {
            var manual = new TimelineBeginResolve { HasAuthoring = true, AuthoringOnLoad = false, DirectorPlayOnAwake = true };
            var onLoad = new TimelineBeginResolve { HasAuthoring = true, AuthoringOnLoad = true, DirectorPlayOnAwake = false };

            Assert.IsFalse(manual.Enabled);
            Assert.IsTrue(onLoad.Enabled);
        }

        [Test]
        public void Remaining_NoAuthoring_IsZero()
        {
            var resolve = new TimelineBeginResolve { HasAuthoring = false, AuthoringDelaySeconds = 5f };

            Assert.AreEqual(DiscreteTime.Zero, resolve.Remaining);
        }

        [Test]
        public void Remaining_WithAuthoring_UsesDelaySeconds()
        {
            var resolve = new TimelineBeginResolve { HasAuthoring = true, AuthoringDelaySeconds = 2.5f };

            Assert.AreEqual(new DiscreteTime(2.5f), resolve.Remaining);
        }

        [Test]
        public void Remaining_WithAuthoringZeroDelay_IsZero()
        {
            var resolve = new TimelineBeginResolve { HasAuthoring = true, AuthoringDelaySeconds = 0f };

            Assert.AreEqual(DiscreteTime.Zero, resolve.Remaining);
        }
    }
}