using UnityEngine;
using UnityEngine.Playables;

namespace BovineLabs.Timeline.Core.Authoring
{
    public enum TimelineBeginMode : byte
    {
        Manual = 0,
        OnLoad = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayableDirector))]
    public class TimelineBeginAuthoring : MonoBehaviour
    {
        public TimelineBeginMode Mode = TimelineBeginMode.OnLoad;

        [Min(0f)] public float DelaySeconds;
    }
}