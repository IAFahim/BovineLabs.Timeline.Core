using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "ensure_track",
        Group = "vex",
        Description = "Idempotent: ensure a .playable holds a track of the given type and name. already-ok when the asset+track already exist; otherwise creates them via timeline_create (and passes its asset_delete undo through). dry_run reports without mutating. The L1 setup block behind every clip.")]
    public static class EnsureTrackTool
    {
        public class Parameters
        {
            [ToolParameter("The .playable path.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track type, simple or full name.", Required = true)]
            public string TrackType { get; set; }

            [ToolParameter("Track name (default = track type).")]
            public string TrackName { get; set; }

            [ToolParameter("JSON object of track-level fields (applied only when creating).")]
            public object TrackFields { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string assetPath = p.RequireString("asset");
                string trackTypeName = p.RequireString("track_type");
                bool dryRun = p.OptBool("dry_run", false);

                var trackType = TimelineReflect.ResolveType(trackTypeName);
                if (!typeof(TrackAsset).IsAssignableFrom(trackType))
                    return ToolEnvelope.Error("BAD_VALUE", $"'{trackType.FullName}' is not a TrackAsset.");
                string trackName = p.OptString("track_name", trackType.Name);
                var target = new { asset = assetPath, track = trackName, type = trackType.FullName };

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                if (timeline != null)
                {
                    var existing = TimelineReflect.FindTrack(timeline, trackName);
                    if (existing != null)
                    {
                        if (!trackType.IsInstanceOfType(existing))
                            return ToolEnvelope.Error("BAD_VALUE",
                                $"Track '{trackName}' exists but is {existing.GetType().Name}, not {trackType.Name}.");
                        return EnsureResult.Satisfied($"Track '{trackName}' already present in {assetPath}.", target);
                    }

                    // The asset exists without this track. Adding a track to a live timeline is a distinct
                    // op (timeline_create would replace the whole asset and strand other tracks) — refuse
                    // rather than silently destroy.
                    return ToolEnvelope.Error("MISSING_PREREQUISITE",
                        $"Asset '{assetPath}' exists but has no '{trackName}' track; refusing to overwrite the asset. " +
                        "Remove it first or pass a fresh asset path.");
                }

                var before = new { assetExisted = false };
                if (dryRun)
                    return EnsureResult.WouldFixResult($"Would create {trackName} ({trackType.Name}) in {assetPath}.", target, before);

                var createParams = new JObject
                {
                    ["asset"] = assetPath,
                    ["track_type"] = trackType.FullName,
                    ["track_name"] = trackName,
                };
                var trackFields = p.OptObject("track_fields");
                if (trackFields != null) createParams["track_fields"] = trackFields;

                var resp = TimelineCreateTool.HandleCommand(createParams);
                if (Responses.IsError(resp)) return resp;

                return EnsureResult.Applied(
                    $"Created {trackName} ({trackType.Name}) in {assetPath}.",
                    target, before, after: new { assetExisted = true },
                    undo: Responses.Undo(resp));
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
