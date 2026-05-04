using BovineLabs.Timeline.Core;
using NUnit.Framework;
using Unity.Entities;

namespace BovineLabs.Timeline.Core.Tests
{
    [TestFixture]
    public class TimelineReferenceTests
    {
        [Test]
        public void IsIComponentData()
        {
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(typeof(TimelineReference)));
        }

        [Test]
        public void IsValueType()
        {
            Assert.IsTrue(typeof(TimelineReference).IsValueType);
        }

        [Test]
        public void DefaultConstructs()
        {
            Assert.DoesNotThrow(() =>
            {
                var _ = new TimelineReference();
            });
        }
    }
}
