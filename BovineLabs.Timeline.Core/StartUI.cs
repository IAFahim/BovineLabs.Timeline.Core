using BovineLabs.Timeline.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Core
{
    public class StartUI : MonoBehaviour
    {
        private EntityQuery timelineQuery;
        private World timelineQueryWorld;

        private void Update()
        {
            TriggerTimeline();
        }

        private void TriggerTimeline()
        {
            if (!TryGetTimelineQuery(out var query)) return;
            var em = timelineQueryWorld.EntityManager;

            var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
                em.SetComponentEnabled<TimelineActive>(entities[i], true);
            entities.Dispose();

            if (!query.IsEmpty) enabled = false;
        }

        private bool TryGetTimelineQuery(out EntityQuery query)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                query = default;
                return false;
            }

            if (timelineQuery != default && timelineQueryWorld == world)
            {
                query = timelineQuery;
                return true;
            }

            timelineQueryWorld = world;
            timelineQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TimelineReference>()
                .WithDisabled<TimelineActive>()
                .Build(world.EntityManager);

            query = timelineQuery;
            return true;
        }
    }
}