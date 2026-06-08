namespace BovineLabs.Timeline.Data
{
    using Unity.Entities;
    using Unity.IntegerTime;

    public struct TimelinePlayRequest : IComponentData, IEnableableComponent
    {
        public DiscreteTime Remaining;
    }
}
