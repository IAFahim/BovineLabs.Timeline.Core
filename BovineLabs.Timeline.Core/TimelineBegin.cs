using Unity.IntegerTime;

namespace BovineLabs.Timeline.Schedular
{
    public static class TimelineBegin
    {
        public static bool TryAdvance(DiscreteTime remaining, DiscreteTime elapsed, out DiscreteTime next)
        {
            if (remaining <= DiscreteTime.Zero)
            {
                next = DiscreteTime.Zero;
                return true;
            }

            next = remaining - elapsed;
            return next <= DiscreteTime.Zero;
        }
    }
}
