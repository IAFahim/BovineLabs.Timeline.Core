using BovineLabs.Core.EntityCommands;
using BovineLabs.Testing;
using BovineLabs.Timeline.Core.Data.Builders;
using NUnit.Framework;
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

            var commands = new EntityManagerCommands(Manager, entity);
            builder.ApplyTo(ref commands);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(entity));
        }

        [Test]
        public void ApplyTo_EntityAlreadyHasComponent_DoesNotThrow()
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponent<TimelineReference>(entity);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(entity));

            var builder = new TimelineReferenceBuilder();
            Assert.DoesNotThrow(() =>
            {
                var commands = new EntityManagerCommands(Manager, entity);
                builder.ApplyTo(ref commands);
            });
        }

        [Test]
        public void ApplyTo_MultipleEntities_AllGetComponent()
        {
            var e1 = Manager.CreateEntity();
            var e2 = Manager.CreateEntity();
            var e3 = Manager.CreateEntity();

            var builder = new TimelineReferenceBuilder();

            var c1 = new EntityManagerCommands(Manager, e1);
            var c2 = new EntityManagerCommands(Manager, e2);
            var c3 = new EntityManagerCommands(Manager, e3);
            builder.ApplyTo(ref c1);
            builder.ApplyTo(ref c2);
            builder.ApplyTo(ref c3);

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
            var builder = new TimelineReferenceBuilder();

            var commands = new EntityManagerCommands(Manager, entity);
            builder.ApplyTo(ref commands);

            Assert.IsTrue(Manager.HasComponent<TimelineReference>(entity));
        }
    }
}