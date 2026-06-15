using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// Shared resolution so <c>mechanic_author</c> (build it) and <c>mechanic_doctor</c> (check/repair it)
    /// derive the IDENTICAL requirement list from the same keys — the author runs them all then adds the
    /// clip; the doctor runs only the idempotent ensures in dry_run/fix.
    /// </summary>
    internal static class MechanicResolver
    {
        // Friendly kind -> (track_type, clip_type) so callers can say kind=trap instead of naming types.
        public static readonly Dictionary<string, (string track, string clip)> Kinds =
            new Dictionary<string, (string, string)>
            {
                { "trap", ("StatefulTriggerTrack", "PhysicsTriggerInstantiateClip") },
            };

        public static ManifestContext BuildContext(JObject @params)
        {
            var p = new Params(@params);
            string kind = p.OptString("kind");
            string trackTypeName = p.OptString("track_type");
            string clipTypeName = p.OptString("clip_type");
            if (!string.IsNullOrEmpty(kind) && Kinds.TryGetValue(kind, out var preset))
            {
                trackTypeName ??= preset.track;
                clipTypeName ??= preset.clip;
            }
            if (string.IsNullOrEmpty(trackTypeName))
                throw new ToolException("MISSING_PREREQUISITE", "Provide track_type or a known kind.");
            if (string.IsNullOrEmpty(clipTypeName))
                throw new ToolException("MISSING_PREREQUISITE", "Provide clip_type or a known kind.");

            var trackType = TimelineReflect.ResolveType(trackTypeName);
            var clipType = TimelineReflect.ResolveType(clipTypeName);

            return new ManifestContext
            {
                Subscene = p.OptString("subscene"),
                Director = p.RequireString("director"),
                Asset = p.RequireString("asset"),
                TrackType = trackType,
                ClipType = clipType,
                TrackName = p.OptString("track_name", trackType.Name),
                ClipName = p.OptString("clip_name", clipType.Name),
                Args = @params,
            };
        }

        /// <summary>Reflectable (generic) UNION hand-declared (package fragment), sorted by phase.</summary>
        public static List<Requirement> BuildRequirements(ManifestContext ctx)
        {
            var reqs = new List<Requirement>(ManifestDiscovery.Reflectable(ctx));
            var fragment = ManifestDiscovery.ForClip(ctx.ClipType);
            if (fragment != null) reqs.AddRange(fragment.Requirements(ctx));
            return reqs.OrderBy(r => (int)r.Phase).ToList();
        }
    }
}
