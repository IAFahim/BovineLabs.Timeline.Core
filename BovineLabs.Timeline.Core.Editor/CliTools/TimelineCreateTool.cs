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
        Description =
            "Create a .playable TimelineAsset with one track of a given type (resolved by name), optionally setting track-level fields. The asset half of §4.")]
    public static class TimelineCreateTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var assetPath = p.RequireString("asset");
                var trackTypeName = p.RequireString("track_type");
                var trackName = p.OptString("track_name");
                var trackFields = p.OptObject("track_fields");
                var overwrite = p.OptBool("overwrite", false);

                var pre = Capture.AssetExistence(assetPath);
                if (pre.assetExisted && !overwrite)
                    return ToolEnvelope.Error("BAD_VALUE",
                        $"Asset exists at '{assetPath}'. Set overwrite=true to replace.");

                var trackType = TimelineReflect.ResolveType(trackTypeName);
                if (!typeof(TrackAsset).IsAssignableFrom(trackType))
                    return ToolEnvelope.Error("BAD_VALUE", $"'{trackType.FullName}' is not a TrackAsset.");

                AssetUtil.EnsureFolders(assetPath);

                var replacing = pre.assetExisted && overwrite;
                var buildPath = replacing
                    ? Path.Combine(Path.GetDirectoryName(assetPath),
                            Path.GetFileNameWithoutExtension(assetPath) + "__vexnew" + Path.GetExtension(assetPath))
                        .Replace('\\', '/')
                    : assetPath;
                if (replacing && Capture.AssetExistence(buildPath).assetExisted)
                    return ToolEnvelope.Error("BAD_VALUE",
                        $"The private scratch path '{buildPath}' is occupied by an existing asset; move or delete it first " +
                        $"(it is the temp build path used to safely overwrite '{assetPath}').");

                var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                var finalTrackName = string.IsNullOrEmpty(trackName) ? trackType.Name : trackName;
                TrackAsset track;

                try
                {
                    AssetDatabase.CreateAsset(timeline, buildPath);
                    track = TimelineReflect.CreateTrack(timeline, trackType, null, finalTrackName);

                    if (trackFields != null)
                        foreach (var kv in trackFields)
                            TimelineReflect.SetSerializedField(track, kv.Key, kv.Value);
                }
                catch
                {
                    AssetDatabase.DeleteAsset(buildPath);
                    throw;
                }

                EditorUtility.SetDirty(timeline);
                EditorUtility.SetDirty(track);
                AssetDatabase.SaveAssets();
                RebakeUtil.ReimportOpenSubScenes();

                if (replacing)
                {
                    if (!AssetDatabase.DeleteAsset(assetPath))
                        return ToolEnvelope.Error("BAD_VALUE",
                            $"Could not delete the existing asset at '{assetPath}' to replace it; the rebuilt replacement is preserved at '{buildPath}'.",
                            new { orphan = buildPath, target = assetPath });

                    var moveErr = AssetDatabase.MoveAsset(buildPath, assetPath);
                    if (!string.IsNullOrEmpty(moveErr))
                        return ToolEnvelope.Error("BAD_VALUE",
                            $"Original '{assetPath}' was deleted but the rebuilt replacement could not be moved into place: {moveErr}. " +
                            $"The built asset is preserved at '{buildPath}' — move it to '{assetPath}' to recover.",
                            new { orphan = buildPath, target = assetPath });
                }

                var undo = replacing
                    ? new object[0]
                    : new object[]
                    {
                        new
                        {
                            tool = "asset_delete",
                            @params = new { asset = assetPath, folder_if_empty = pre.folderExisted ? null : pre.folder }
                        }
                    };

                return ToolEnvelope.Ok(
                    $"Created {finalTrackName} in {Path.GetFileName(assetPath)}." +
                    (replacing ? " Overwrote the existing asset (irreversible)." : string.Empty),
                    new
                    {
                        assetPath, trackName = finalTrackName, trackType = trackType.FullName,
                        overwroteExisting = replacing
                    },
                    new { pre.folderExisted, pre.assetExisted },
                    undo);
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("Path for the new .playable (folders auto-created).", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track type, simple or full name (e.g. TransformPositionTrack).", Required = true)]
            public string TrackType { get; set; }

            [ToolParameter("Name for the created track (default = track type).")]
            public string TrackName { get; set; }

            [ToolParameter(
                "JSON object of fieldName -> value set on the track (e.g. {\"ResetPositionOnDeactivate\": true}).")]
            public object TrackFields { get; set; }

            [ToolParameter("Allow replacing an existing asset at the path (default false).")]
            public bool Overwrite { get; set; }
        }
    }
}