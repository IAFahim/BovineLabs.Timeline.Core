using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "timeline_create",
        Group = "vex",
        Description = "Create a .playable TimelineAsset with one track of a given type (resolved by name), optionally setting track-level fields. The asset half of §4.")]
    public static class TimelineCreateTool
    {
        public class Parameters
        {
            [ToolParameter("Path for the new .playable (folders auto-created).", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track type, simple or full name (e.g. TransformPositionTrack).", Required = true)]
            public string TrackType { get; set; }

            [ToolParameter("Name for the created track (default = track type).")]
            public string TrackName { get; set; }

            [ToolParameter("JSON object of fieldName -> value set on the track (e.g. {\"ResetPositionOnDeactivate\": true}).")]
            public object TrackFields { get; set; }

            [ToolParameter("Allow replacing an existing asset at the path (default false).")]
            public bool Overwrite { get; set; }
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
                string trackName = p.OptString("track_name");
                var trackFields = p.OptObject("track_fields");
                bool overwrite = p.OptBool("overwrite", false);

                var pre = Capture.AssetExistence(assetPath);
                if (pre.assetExisted && !overwrite)
                    return ToolEnvelope.Error("BAD_VALUE", $"Asset exists at '{assetPath}'. Set overwrite=true to replace.");

                var trackType = TimelineReflect.ResolveType(trackTypeName);
                if (!typeof(TrackAsset).IsAssignableFrom(trackType))
                    return ToolEnvelope.Error("BAD_VALUE", $"'{trackType.FullName}' is not a TrackAsset.");

                if (pre.assetExisted && overwrite) AssetDatabase.DeleteAsset(assetPath);
                AssetUtil.EnsureFolders(assetPath);

                var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, assetPath);

                string finalTrackName = string.IsNullOrEmpty(trackName) ? trackType.Name : trackName;
                var track = TimelineReflect.CreateTrack(timeline, trackType, null, finalTrackName);

                if (trackFields != null)
                    foreach (var kv in trackFields)
                        TimelineReflect.SetSerializedField(track, kv.Key, kv.Value);

                EditorUtility.SetDirty(timeline);
                EditorUtility.SetDirty(track);
                AssetDatabase.SaveAssets();

                var undo = new object[]
                {
                    new { tool = "asset_delete", @params = new { asset = assetPath, folder_if_empty = pre.folderExisted ? null : pre.folder } },
                };

                return ToolEnvelope.Ok(
                    $"Created {finalTrackName} in {Path.GetFileName(assetPath)}.",
                    result: new { assetPath, trackName = finalTrackName, trackType = trackType.FullName },
                    pre: new { pre.folderExisted, pre.assetExisted },
                    undo: undo);
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
