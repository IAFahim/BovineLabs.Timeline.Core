using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "clip_add",
        Group = "vex",
        Description = "Add a clip of a given type to a track in a .playable; set timing (start/duration/blend) and serialized fields with correct type coercion. The clip half of §4.")]
    public static class ClipAddTool
    {
        public class Parameters
        {
            [ToolParameter("The .playable to add the clip to.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Locate the track by name or index.", Required = true)]
            public string Track { get; set; }

            [ToolParameter("Clip type, simple or full name (e.g. PositionClip).", Required = true)]
            public string ClipType { get; set; }

            [ToolParameter("Clip start in seconds (default 0).")]
            public float Start { get; set; }

            [ToolParameter("Clip length in seconds (default = clip default).")]
            public float Duration { get; set; }

            [ToolParameter("Clip display name (default = clip type).")]
            public string DisplayName { get; set; }

            [ToolParameter("Blend-in duration in seconds.")]
            public float BlendIn { get; set; }

            [ToolParameter("Blend-out duration in seconds.")]
            public float BlendOut { get; set; }

            [ToolParameter("JSON object of clipFieldName -> value. Byte enums as the underlying int, asset refs by path/{guid}, vectors as [x,y,z], scalars literal.")]
            public object Fields { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string assetPath = p.RequireString("asset");
                string trackSel = p.RequireString("track");
                string clipTypeName = p.RequireString("clip_type");

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                if (timeline == null) return ToolEnvelope.Error("NOT_FOUND", $"No TimelineAsset at '{assetPath}'.");

                var track = TimelineReflect.FindTrack(timeline, trackSel);
                if (track == null) return ToolEnvelope.Error("NOT_FOUND", $"No track '{trackSel}' in {assetPath}.");

                var clipType = TimelineReflect.ResolveType(clipTypeName);
                var clip = TimelineReflect.CreateClip(track, clipType);

                clip.start = p.OptFloat("start", 0f);
                if (p.Has("duration")) clip.duration = p.OptFloat("duration", (float)clip.duration);
                clip.displayName = UniqueClipName(track, clip, p.OptString("display_name", clipType.Name));
                if (p.Has("blend_in")) clip.blendInDuration = p.OptFloat("blend_in", 0f);
                if (p.Has("blend_out")) clip.blendOutDuration = p.OptFloat("blend_out", 0f);

                var fields = p.OptObject("fields");
                if (fields != null && clip.asset != null)
                    foreach (var kv in fields)
                        TimelineReflect.SetSerializedField(clip.asset, kv.Key, kv.Value);

                EditorUtility.SetDirty(track);
                if (clip.asset != null) EditorUtility.SetDirty(clip.asset);
                AssetDatabase.SaveAssets();
                RebakeUtil.ReimportOpenSubScenes(); // SaveAssets alone won't re-bake an open SubScene (see RebakeUtil).

                var undo = new object[]
                {
                    new { tool = "clip_remove", @params = new { asset = assetPath, track = track.name, clip = clip.displayName } },
                };

                return ToolEnvelope.Ok(
                    $"Added {clipType.Name} '{clip.displayName}' to {track.name} ({clip.start}-{clip.start + clip.duration}s).",
                    result: new
                    {
                        asset = assetPath,
                        track = track.name,
                        clip = new
                        {
                            displayName = clip.displayName,
                            start = clip.start,
                            duration = clip.duration,
                            blendIn = clip.blendInDuration,
                            blendOut = clip.blendOutDuration,
                            type = clip.asset != null ? clip.asset.GetType().Name : null,
                        },
                    },
                    undo: undo);
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        // Guarantee the new clip's display name is unique on the track, so the clip_remove undo
        // (which matches by display name) can never remove a pre-existing same-named clip instead.
        private static string UniqueClipName(TrackAsset track, TimelineClip self, string desired)
        {
            bool Collides(string n)
            {
                foreach (var c in track.GetClips())
                    if (c != self && c.displayName == n)
                        return true;
                return false;
            }

            if (!Collides(desired))
                return desired;

            for (var i = 2; ; i++)
            {
                var candidate = $"{desired} ({i})";
                if (!Collides(candidate))
                    return candidate;
            }
        }
    }
}
