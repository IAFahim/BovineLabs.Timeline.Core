#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Debug
{
    [Configurable]
    public static class TelemetryVisualConfig
    {
        [ConfigVar("bovinelabs.telemetry.visual-overlay", true, "Draw visual overlays alongside telemetry text.")]
        public static readonly SharedStatic<bool> VisualOverlay = SharedStatic<bool>.GetOrCreate<Tags.VisualOverlay>();

        [ConfigVar("bovinelabs.telemetry.visual-dots", true,
            "Draw per-line fill indicator dots (requires FullDetails range).")]
        public static readonly SharedStatic<bool> ShowDots = SharedStatic<bool>.GetOrCreate<Tags.ShowDots>();

        [ConfigVar("bovinelabs.telemetry.visual-lod", 80f, "Max distance at which the health beacon is drawn.")]
        public static readonly SharedStatic<float> BeaconLod = SharedStatic<float>.GetOrCreate<Tags.BeaconLod>();

        [ConfigVar("bovinelabs.telemetry.cond-bits", 16, "Number of condition bits shown in the ring.")]
        public static readonly SharedStatic<int> CondBits = SharedStatic<int>.GetOrCreate<Tags.CondBits>();

        [ConfigVar("bovinelabs.telemetry.arc-gap", 0.07f, "Gap between condition ring arc segments (radians).")]
        public static readonly SharedStatic<float> ArcGap = SharedStatic<float>.GetOrCreate<Tags.ArcGap>();

        [ConfigVar("bovinelabs.telemetry.ripple-max-r", 2.0f, "Event ripple maximum expansion radius (world units).")]
        public static readonly SharedStatic<float> RippleMaxR = SharedStatic<float>.GetOrCreate<Tags.RippleMaxR>();

        [ConfigVar("bovinelabs.telemetry.ripple-life", 1.4f, "Event ripple lifetime in seconds.")]
        public static readonly SharedStatic<float> RippleLife = SharedStatic<float>.GetOrCreate<Tags.RippleLife>();

        [ConfigVar("bovinelabs.telemetry.ripple-offset-x", 0f,
            "Horizontal glyph-space offset for ripple anchors (0 = centre-aligned with text). Negative = left.")]
        public static readonly SharedStatic<float>
            RippleOffsetX = SharedStatic<float>.GetOrCreate<Tags.RippleOffsetX>();

        private struct Tags
        {
            public struct VisualOverlay
            {
            }

            public struct ShowDots
            {
            }

            public struct BeaconLod
            {
            }

            public struct CondBits
            {
            }

            public struct ArcGap
            {
            }

            public struct RippleMaxR
            {
            }

            public struct RippleLife
            {
            }

            public struct RippleOffsetX
            {
            }
        }
    }

    public static class VisualGlyph
    {
        public static Color HealthGradient(float pct)
        {
            pct = math.saturate(pct);
            float r, g;
            if (pct <= 0.5f)
            {
                r = 0.92f;
                g = pct * 1.60f;
            }
            else
            {
                r = (1f - pct) * 1.72f;
                g = 0.80f;
            }

            return new Color(r, g, 0.07f, 1f);
        }

        public static void BeaconPulse(Drawer d, in View v, float glyphX, float glyphY,
            float glyphRadius, float time, Color color)
        {
            var p = math.sin(time * 3.8f) * 0.5f + 0.5f;
            var center = v.At(glyphX, glyphY);
            var worldR = glyphRadius * v.Unit * (1.16f + p * 0.30f);
            var a = (0.25f + p * 0.60f) * color.a;
            d.Circle(center, v.Normal * worldR, new Color(color.r, color.g, color.b, a));
        }
    }
}
#endif