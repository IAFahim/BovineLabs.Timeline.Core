using Unity.IntegerTime;

namespace BovineLabs.Timeline.Core.Data.Builders
{
    public struct TimelineBeginResolve
    {
        public bool HasAuthoring;
        public bool AuthoringOnLoad;
        public float AuthoringDelaySeconds;
        public bool DirectorPlayOnAwake;

        public bool Enabled => this.HasAuthoring ? this.AuthoringOnLoad : this.DirectorPlayOnAwake;

        public DiscreteTime Remaining => this.HasAuthoring ? new DiscreteTime(this.AuthoringDelaySeconds) : DiscreteTime.Zero;
    }
}