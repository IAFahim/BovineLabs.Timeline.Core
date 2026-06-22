#if UNITY_EDITOR || BL_DEBUG
using UnityEngine;

namespace BovineLabs.Timeline.Core.Debug
{
    public static class TimelineDebugColors
    {
        public static readonly Color TargetLink = new(1.0f, 0.2f, 0.4f, 0.9f);
        public static readonly Color SourceLink = new(1.0f, 0.6f, 0.1f, 0.9f);
        public static readonly Color OwnerLink = new(0.2f, 0.8f, 1.0f, 0.9f);
        public static readonly Color CustomLink = new(0.4f, 1.0f, 0.4f, 0.9f);

        public static readonly Color LinearForce = new(1.0f, 0.0f, 1.0f, 0.8f);
        public static readonly Color AngularForce = new(0.6f, 0.0f, 1.0f, 0.8f);
        public static readonly Color LinearVelocity = new(0.0f, 1.0f, 1.0f, 0.8f);

        public static readonly Color PidTarget = Color.yellow;
        public static readonly Color PidGoal = new(1.0f, 0.8f, 0.0f, 1.0f);
        public static readonly Color PidPredicted = new(0.0f, 1.0f, 0.0f, 0.5f);

        public static readonly Color Radius = new(0.9f, 0.7f, 0.2f, 0.4f);
        public static readonly Color Clearance = new(0.95f, 0.4f, 0.3f, 0.6f);
        public static readonly Color LineOfSight = new(0.3f, 1.0f, 0.5f, 0.5f);

        public static readonly Color Anchor = new(0.1f, 0.95f, 0.85f, 0.9f);
        public static readonly Color Connection = new(0.1f, 0.85f, 0.75f, 0.4f);
        public static readonly Color Label = new(1.0f, 1.0f, 1.0f, 0.95f);
        public static readonly Color Error = Color.red;
    }
}
#endif