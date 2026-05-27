#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core.Extensions;
using BovineLabs.Quill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Debug
{
    /// <summary>
    /// Shared utility to eliminate the repeated DrawSystem.Singleton boilerplate
    /// found in every debug system's OnUpdate method.
    /// </summary>
    public static class TimelineDebugUtility
    {
        /// <summary>
        /// Attempts to acquire a <see cref="Drawer"/> from the DrawSystem singleton.
        /// - If <paramref name="forceEnabled"/> is false: uses typed CreateDrawer (per-system toggle).
        /// - If <paramref name="forceEnabled"/> is true: uses untyped CreateDrawer (global draw).
        /// Returns false if DrawSystem singleton is missing or the drawer is not enabled.
        /// </summary>
        public static bool TryGetDrawer<TSystem>(
            bool forceEnabled,
            out Drawer drawer)
            where TSystem : unmanaged, ISystem
        {
            drawer = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
                return false; // Do not access default world blindly during play mode!
#endif

            var em = world.EntityManager;
            try
            {
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
            catch (System.InvalidOperationException)
            {
                // Happens when AsyncLoadSceneJob holds an exclusive lock on the Default World's EntityManager.
                // We safely ignore debug drawing for this frame.
                return false;
            }
        }

        /// <summary>
        /// SystemState-based overload for cases where the calling system already has access to it.
        /// </summary>
        public static bool TryGetDrawer<TSystem>(
            ref SystemState state,
            bool forceEnabled,
            out Drawer drawer)
            where TSystem : unmanaged, ISystem
        {
            drawer = default;
            var em = state.EntityManager;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying && (state.World.Flags & WorldFlags.Editor) != 0)
                return false; // Do not cross-access Default World from Editor World during play mode
#endif

            try
            {
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
            catch (System.InvalidOperationException)
            {
                // Ignore structural lock exceptions during scene loads
                return false;
            }
        }
    }
}
#endif
