using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using Unity.Burst;
using Unity.Mathematics;

#if UNITY_EDITOR || BL_DEBUG
namespace BovineLabs.Timeline.Core.Debug
{
    public enum DebugTier : byte
    {
        Far = 0,

        Mid = 1,

        Close = 2
    }

    [Configurable]
    public static class TimelineDebugTierConfig
    {
        [ConfigVar("debug.tier.mode", 0,
            "Debug detail tier: 0=auto by distance, 1=force far (minimal), 2=force mid, 3=force close (full detail).")]
        public static readonly SharedStatic<int> Mode = SharedStatic<int>.GetOrCreate<ModeTag>();

        [ConfigVar("debug.tier.close-distance", 8f, "Camera distance (m) at/below which ALL debug data is shown.")]
        public static readonly SharedStatic<float> CloseDistance = SharedStatic<float>.GetOrCreate<CloseTag>();

        [ConfigVar("debug.tier.mid-distance", 25f,
            "Camera distance (m) at/below which key interaction data is shown; beyond is far/minimal.")]
        public static readonly SharedStatic<float> MidDistance = SharedStatic<float>.GetOrCreate<MidTag>();

        private struct ModeTag
        {
        }

        private struct CloseTag
        {
        }

        private struct MidTag
        {
        }
    }

    public static class TimelineDebugTier
    {
        public static DebugTier Resolve(float3 worldPos, float3 viewer, bool hasViewer)
        {
            switch (TimelineDebugTierConfig.Mode.Data)
            {
                case 1: return DebugTier.Far;
                case 2: return DebugTier.Mid;
                case 3: return DebugTier.Close;
            }

            if (!hasViewer) return DebugTier.Mid;

            var d = math.distance(worldPos, viewer);
            if (d <= TimelineDebugTierConfig.CloseDistance.Data) return DebugTier.Close;

            return d <= TimelineDebugTierConfig.MidDistance.Data ? DebugTier.Mid : DebugTier.Far;
        }

        public static bool TryGetViewer(in CameraCulling culling, out float3 viewer)
        {
            viewer = default;
            return !culling.IsDefault && PlaneIntersect.TryThreePlaneIntersect(culling.Left, culling.Right, culling.Top, out viewer);
        }
    }
}
#endif