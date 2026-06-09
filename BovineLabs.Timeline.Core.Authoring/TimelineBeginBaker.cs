using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.IntegerTime;
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

            var onLoad = authoring != null
                ? authoring.Mode == TimelineBeginMode.OnLoad
                : director.playOnAwake;

            var remaining = authoring != null
                ? new DiscreteTime(authoring.DelaySeconds)
                : DiscreteTime.Zero;

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TimelinePlayRequest { Remaining = remaining });
            SetComponentEnabled<TimelinePlayRequest>(entity, onLoad);
        }
    }
}