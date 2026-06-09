using Unity.Entities;
using Unity.IntegerTime;

namespace BovineLabs.Timeline.Data
{
    public struct TimelinePlayRequest : IComponentData, IEnableableComponent
    {
        public DiscreteTime Remaining;
    }
}