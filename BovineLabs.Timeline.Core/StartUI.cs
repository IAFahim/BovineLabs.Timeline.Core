using System;
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
            if (!TryGetTimelineQuery(out var query) || query.IsEmpty) return;
            var em = timelineQueryWorld.EntityManager;

            try
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                var count = entities.Length;
                for (var i = 0; i < count; i++)
                    em.SetComponentEnabled<TimelineActive>(entities[i], true);
                entities.Dispose();

                if (count > 0) enabled = false;
            }
            catch (InvalidOperationException)
            {
                // Wait for AsyncLoadSceneJob to finish
            }
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