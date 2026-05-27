#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Quill;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Debug
{
    /// <summary>
    /// Shared color palette for all timeline debug drawing. Ensures visual consistency
    /// across packages (Physics, EntityLinks, Essence, etc.).
    /// </summary>
    public static class TimelineDebugColors
    {
        // Target / Entity Links
        public static readonly Color TargetLink = new(1.0f, 0.2f, 0.4f, 0.9f);
        public static readonly Color SourceLink = new(1.0f, 0.6f, 0.1f, 0.9f);
        public static readonly Color OwnerLink  = new(0.2f, 0.8f, 1.0f, 0.9f);
        public static readonly Color CustomLink = new(0.4f, 1.0f, 0.4f, 0.9f);

        // Force / Velocity
        public static readonly Color LinearForce    = new(1.0f, 0.0f, 1.0f, 0.8f);
        public static readonly Color AngularForce   = new(0.6f, 0.0f, 1.0f, 0.8f);
        public static readonly Color LinearVelocity = new(0.0f, 1.0f, 1.0f, 0.8f);

        // PID
        public static readonly Color PidTarget   = Color.yellow;
        public static readonly Color PidGoal     = new(1.0f, 0.8f, 0.0f, 1.0f);
        public static readonly Color PidPredicted = new(0.0f, 1.0f, 0.0f, 0.5f);

        // Teleport / Spatial
        public static readonly Color Radius    = new(0.9f, 0.7f, 0.2f, 0.4f);
        public static readonly Color Clearance = new(0.95f, 0.4f, 0.3f, 0.6f);
        public static readonly Color LineOfSight = new(0.3f, 1.0f, 0.5f, 0.5f);

        // General
        public static readonly Color Anchor     = new(0.1f, 0.95f, 0.85f, 0.9f);
        public static readonly Color Connection = new(0.1f, 0.85f, 0.75f, 0.4f);
        public static readonly Color Label      = new(1.0f, 1.0f, 1.0f, 0.95f);
        public static readonly Color Error      = Color.red;
    }
}
#endif
