using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class MechanicResolver
    {
        public static readonly Dictionary<string, (string track, string clip)> Kinds =
            new()
            {
                { "trap", ("StatefulTriggerTrack", "PhysicsTriggerInstantiateClip") }
            };

        public static ManifestContext BuildContext(JObject @params)
        {
            var p = new Params(@params);
            var kind = p.OptString("kind");
            var trackTypeName = p.OptString("track_type");
            var clipTypeName = p.OptString("clip_type");
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
                Args = @params
            };
        }

        public static List<Requirement> BuildRequirements(ManifestContext ctx)
        {
            var reqs = new List<Requirement>(ManifestDiscovery.Reflectable(ctx));
            var fragment = ManifestDiscovery.ForClip(ctx.ClipType);
            if (fragment != null) reqs.AddRange(fragment.Requirements(ctx));
            return reqs.OrderBy(r => (int)r.Phase).ToList();
        }
    }
}