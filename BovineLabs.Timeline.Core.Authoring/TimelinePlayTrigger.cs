using BovineLabs.Timeline.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Authoring
{
    public class TimelinePlayTrigger : MonoBehaviour
    {
        public void Play()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TimelinePlayRequest>()
                .WithDisabled<TimelinePlayRequest>()
                .Build(em);

            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var e in entities) em.SetComponentEnabled<TimelinePlayRequest>(e, true);
        }
    }
}