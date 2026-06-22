using BovineLabs.Timeline.Core.Data.Builders;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Authoring
{
    public class TimelineBeginBaker : Baker<PlayableDirector>
    {
        public override void Bake(PlayableDirector director)
        {
            if (director.playableAsset is not TimelineAsset) return;

            var authoring = GetComponent<TimelineBeginAuthoring>();

            var resolve = new TimelineBeginResolve
            {
                HasAuthoring = authoring != null,
                AuthoringOnLoad = authoring != null && authoring.Mode == TimelineBeginMode.OnLoad,
                AuthoringDelaySeconds = authoring != null ? authoring.DelaySeconds : 0f,
                DirectorPlayOnAwake = director.playOnAwake,
            };

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TimelinePlayRequest { Remaining = resolve.Remaining });
            SetComponentEnabled<TimelinePlayRequest>(entity, resolve.Enabled);
        }
    }
}