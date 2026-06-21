#if UNITY_EDITOR || BL_DEBUG
namespace BovineLabs.Timeline.Core.Debug
{
    using BovineLabs.Core;
    using BovineLabs.Core.ConfigVars;
    using BovineLabs.Quill;
    using Unity.Burst;
    using Unity.Mathematics;

    /// <summary>
    ///     Level-of-detail tier for debug drawing, chosen by how far the camera is from what is being drawn.
    ///     Lets a designer read a system at a glance from far (Far) and see every number up close (Close)
    ///     without constantly zooming. Order matters: <c>tier &gt;= DebugTier.Mid</c> means "Mid or closer".
    /// </summary>
    public enum DebugTier : byte
    {
        /// <summary> Top / far view. Only the single shape that says what the system is doing. No text. </summary>
        Far = 0,

        /// <summary> Mid view. The key interaction (target line, velocity, one short label). </summary>
        Mid = 1,

        /// <summary> Close view. Everything, every number as text. Nothing left to imagination. </summary>
        Close = 2,
    }

    /// <summary> Config vars driving the debug tier selection. Surfaced in the ConfigVars window. </summary>
    [Configurable]
    public static class TimelineDebugTierConfig
    {
        /// <summary> 0 = auto by distance; 1 = force Far; 2 = force Mid; 3 = force Close (full detail from any distance). </summary>
        [ConfigVar("debug.tier.mode", 0, "Debug detail tier: 0=auto by distance, 1=force far (minimal), 2=force mid, 3=force close (full detail).")]
        public static readonly SharedStatic<int> Mode = SharedStatic<int>.GetOrCreate<ModeTag>();

        /// <summary> Camera distance (metres) at or below which the Close (full) tier is shown. </summary>
        [ConfigVar("debug.tier.close-distance", 8f, "Camera distance (m) at/below which ALL debug data is shown.")]
        public static readonly SharedStatic<float> CloseDistance = SharedStatic<float>.GetOrCreate<CloseTag>();

        /// <summary> Camera distance (metres) at or below which the Mid tier is shown. Beyond this is Far. </summary>
        [ConfigVar("debug.tier.mid-distance", 25f, "Camera distance (m) at/below which key interaction data is shown; beyond is far/minimal.")]
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

    /// <summary> Resolves the <see cref="DebugTier" /> for a world position. Burst friendly. </summary>
    public static class TimelineDebugTier
    {
        /// <summary> Picks the tier for a draw at <paramref name="worldPos" /> given the camera eye. </summary>
        /// <param name="worldPos"> The world position being drawn. </param>
        /// <param name="viewer"> Camera eye position, from <see cref="TryGetViewer" />. </param>
        /// <param name="hasViewer"> False when no camera info was available (falls back to Mid). </param>
        public static DebugTier Resolve(float3 worldPos, float3 viewer, bool hasViewer)
        {
            switch (TimelineDebugTierConfig.Mode.Data)
            {
                case 1: return DebugTier.Far;
                case 2: return DebugTier.Mid;
                case 3: return DebugTier.Close;
            }

            if (!hasViewer)
            {
                return DebugTier.Mid;
            }

            var d = math.distance(worldPos, viewer);
            if (d <= TimelineDebugTierConfig.CloseDistance.Data)
            {
                return DebugTier.Close;
            }

            return d <= TimelineDebugTierConfig.MidDistance.Data ? DebugTier.Mid : DebugTier.Far;
        }

        /// <summary> Derives the camera eye position from Quill's frustum planes (intersection of Left, Right, Top). </summary>
        public static bool TryGetViewer(in CameraCulling culling, out float3 viewer)
        {
            viewer = default;
            return !culling.IsDefault && TryIntersect3(culling.Left, culling.Right, culling.Top, out viewer);
        }

        // Intersection of 3 planes (plane = n.xyz, d.w; dot(n, x) + d = 0). Same math as CameraCulling.
        private static bool TryIntersect3(in float4 p1, in float4 p2, in float4 p3, out float3 result)
        {
            float3 n1 = p1.xyz, n2 = p2.xyz, n3 = p3.xyz;
            var n2xn3 = math.cross(n2, n3);
            var n3xn1 = math.cross(n3, n1);
            var n1xn2 = math.cross(n1, n2);

            var denom = math.dot(n1, n2xn3);
            if (math.abs(denom) < 1e-6f)
            {
                result = default;
                return false;
            }

            var x = (-p1.w * n2xn3) + (-p2.w * n3xn1) + (-p3.w * n1xn2);
            result = x / denom;
            return true;
        }
    }
}
#endif
