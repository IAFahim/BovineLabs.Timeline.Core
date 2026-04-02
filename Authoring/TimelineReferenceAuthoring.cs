using BovineLabs.Timeline.Core;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Authoring
{
    /// <summary>
    /// Add this MonoBehaviour to a GameObject to mark its baked entity
    /// as a TimelineReference — allowing StartUI and similar systems
    /// to locate and activate it at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimelineReferenceAuthoring : MonoBehaviour
    {
        private class TimelineReferenceBaker : Baker<TimelineReferenceAuthoring>
        {
            public override void Bake(TimelineReferenceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<TimelineReference>(entity);
            }
        }
    }
}
