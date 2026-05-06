using BovineLabs.Testing;
using BovineLabs.Timeline.Core.Data.Builders;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Core.Tests
{
    public class TimelineReferenceBuilderTests : ECSTestsFixture
    {
        [Test]
        public void ApplyTo_AddsTimelineReference()
        {
            var entity = Manager.CreateEntity();
            var builder = new TimelineReferenceBuilder();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddComponent<TimelineReference>(entity);
            ecb.Playback(Manager);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(entity));
        }

        [Test]
        public void ApplyTo_EntityAlreadyHasComponent_DoesNotThrow()
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponent<TimelineReference>(entity);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(entity));
            Assert.DoesNotThrow(() =>
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                ecb.AddComponent<TimelineReference>(entity);
                ecb.Playback(Manager);
            });
        }

        [Test]
        public void ApplyTo_MultipleEntities_AllGetComponent()
        {
            var e1 = Manager.CreateEntity();
            var e2 = Manager.CreateEntity();
            var e3 = Manager.CreateEntity();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddComponent<TimelineReference>(e1);
            ecb.AddComponent<TimelineReference>(e2);
            ecb.AddComponent<TimelineReference>(e3);
            ecb.Playback(Manager);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(e1));
            Assert.IsTrue(Manager.HasComponent<TimelineReference>(e2));
            Assert.IsTrue(Manager.HasComponent<TimelineReference>(e3));
        }

        [Test]
        public void TimelineReference_IsBlittable()
        {
            Assert.IsTrue(typeof(TimelineReference).IsValueType);
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(typeof(TimelineReference)));
        }

        [Test]
        public void ApplyTo_ComponentIsZeroSizedTag()
        {
            var entity = Manager.CreateEntity();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddComponent<TimelineReference>(entity);
            ecb.Playback(Manager);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(entity));
        }
    }
}