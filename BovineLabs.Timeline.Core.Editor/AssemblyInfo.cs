using System.Runtime.CompilerServices;

// The CliTools Shared/ helpers (SubSceneSession, ToolEnvelope, Params, Capture,
// TimelineReflect, Hierarchy) are internal to Core.Editor but reused by the
// per-package vex editor tools (phase 2+). Expose them to those .Editor
// assemblies so each package's tools build on the one proven foundation instead
// of re-deriving it. Additive only — does not change any public surface.
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Essence.Editor")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.EntityLinks.Editor")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Physics.Editor")]
