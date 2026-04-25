using BovineLabs.Timeline.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Core
{
    public class StartUI : MonoBehaviour
    {
        private EntityQuery timelineQuery;

        private void Update()
        {
            TriggerTimeline();
        }

        private void TriggerTimeline()
        {
            if (World.DefaultGameObjectInjectionWorld == null)
                return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (timelineQuery == default)
            {
                timelineQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<TimelineReference>()
                    .WithDisabled<TimelineActive>()
                    .Build(em);
            }

            foreach (var e in timelineQuery.ToEntityArray(Allocator.Temp))
                em.SetComponentEnabled<TimelineActive>(e, true);

            if (!timelineQuery.IsEmpty) enabled = false;
        }
    }
}
