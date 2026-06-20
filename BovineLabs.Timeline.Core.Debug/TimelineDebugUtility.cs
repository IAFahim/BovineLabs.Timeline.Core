#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core.Extensions;
using BovineLabs.Quill;
using Unity.Entities;

namespace BovineLabs.Timeline.Core.Debug
{
    public static class TimelineDebugUtility
    {
        public static bool TryGetDrawer<TSystem>(
            bool forceEnabled,
            out Drawer drawer)
            where TSystem : unmanaged, ISystem
        {
            drawer = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return false;

            // Dispose the query: there is no system here to own/cache it, and em.GetSingletonRW would leak an
            // unowned query every call.
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<DrawSystem.Singleton>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            ref var drawSystem = ref query.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

            if (!forceEnabled)
            {
                drawer = drawSystem.CreateDrawer<TSystem>();
                return drawer.IsEnabled;
            }

            drawer = drawSystem.CreateDrawer();
            return true;
        }


        public static bool TryGetDrawer<TSystem>(
            ref SystemState state,
            bool forceEnabled,
            out Drawer drawer)
            where TSystem : unmanaged, ISystem
        {
            drawer = default;

            // Use the system's cached query (state.GetEntityQuery) instead of em.GetSingletonRW, which created
            // and leaked an unowned EntityQuery on every call.
            var query = state.GetEntityQuery(ComponentType.ReadWrite<DrawSystem.Singleton>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            ref var drawSystem = ref query.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

            if (!forceEnabled)
            {
                drawer = drawSystem.CreateDrawer<TSystem>();
                return drawer.IsEnabled;
            }

            drawer = drawSystem.CreateDrawer();
            return true;
        }
    }
}
#endif