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


            var em = world.EntityManager;
            if (!em.HasSingleton<DrawSystem.Singleton>())
                return false;

            ref var drawSystem = ref em.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

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
            var em = state.EntityManager;

            if (!em.HasSingleton<DrawSystem.Singleton>())
                return false;

            ref var drawSystem = ref em.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

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