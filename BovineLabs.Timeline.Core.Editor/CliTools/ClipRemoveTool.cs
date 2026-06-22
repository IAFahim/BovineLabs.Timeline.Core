using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "clip_remove",
        Group = "vex",
        Description =
            "Remove a clip from a track by display name or index. The inverse of clip_add; an undo primitive.")]
    public static class ClipRemoveTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var assetPath = p.RequireString("asset");
                var trackSel = p.RequireString("track");
                var clipSel = p.RequireString("clip");

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                if (timeline == null) return ToolEnvelope.Error("NOT_FOUND", $"No TimelineAsset at '{assetPath}'.");

                var track = TimelineReflect.FindTrack(timeline, trackSel);
                if (track == null) return ToolEnvelope.Error("NOT_FOUND", $"No track '{trackSel}' in {assetPath}.");

                var clip = TimelineReflect.FindClip(track, clipSel);
                if (clip == null)
                    return ToolEnvelope.Error("NOT_FOUND", $"No clip '{clipSel}' on track '{track.name}'.");

                var name = clip.displayName;
                if (!timeline.DeleteClip(clip))
                    return ToolEnvelope.Error("BAD_VALUE", $"DeleteClip failed for '{clipSel}'.");

                EditorUtility.SetDirty(track);
                AssetDatabase.SaveAssets();
                RebakeUtil.ReimportOpenSubScenes();

                return ToolEnvelope.Ok(
                    $"Removed clip '{name}' from {track.name}.",
                    new { asset = assetPath, track = track.name, clip = name });
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("The .playable.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Locate the track (name or index).", Required = true)]
            public string Track { get; set; }

            [ToolParameter("Locate the clip (display name or index).", Required = true)]
            public string Clip { get; set; }
        }
    }
}