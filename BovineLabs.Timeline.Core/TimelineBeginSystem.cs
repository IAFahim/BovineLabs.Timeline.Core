namespace BovineLabs.Timeline.Schedular
{
    using BovineLabs.Core;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.Data.Schedular;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.IntegerTime;
    using UnityEngine;

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                       WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ScheduleSystemGroup))]
    [UpdateBefore(typeof(ClockUpdateSystem))]
    public partial struct TimelineBeginSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BeginJob
            {
                GameDeltaTime = new DiscreteTime(SystemAPI.Time.DeltaTime),
                UnscaledDeltaTime = new DiscreteTime(Time.unscaledDeltaTime),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(TimelineActive))]
        [WithPresent(typeof(TimerPaused))]
        private partial struct BeginJob : IJobEntity
        {
            public DiscreteTime GameDeltaTime;
            public DiscreteTime UnscaledDeltaTime;

            private void Execute(
                ref TimelinePlayRequest request,
                EnabledRefRW<TimelinePlayRequest> requested,
                in ClockSettings clock,
                EnabledRefRW<TimelineActive> active,
                EnabledRefRW<TimerPaused> paused)
            {
                if (request.Remaining > DiscreteTime.Zero)
                {
                    request.Remaining -= Elapsed(clock);

                    if (request.Remaining > DiscreteTime.Zero)
                    {
                        return;
                    }
                }

                active.ValueRW = true;
                paused.ValueRW = false;
                requested.ValueRW = false;
            }

            private DiscreteTime Elapsed(in ClockSettings clock)
            {
                return clock.UpdateMode switch
                {
                    ClockUpdateMode.UnscaledGameTime => this.UnscaledDeltaTime,
                    ClockUpdateMode.Constant => clock.DeltaTime,
                    _ => this.GameDeltaTime,
                };
            }
        }
    }
}
