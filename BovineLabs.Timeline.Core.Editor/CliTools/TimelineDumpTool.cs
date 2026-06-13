using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "timeline_dump",
        Group = "vex",
        Description = "Load a .playable and dump every track -> clip -> serialized fields (byte enums as ints, object refs as path/guid). The §7.1 fresh-load dump + §7.2 raw check in one read.")]
    public static class TimelineDumpTool
    {
        public class Parameters
        {
            [ToolParameter("Path to the .playable.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Restrict to one track by name/index. Omit = all tracks.")]
            public string Track { get; set; }

            [ToolParameter("Also include serialized field maps of each track and clip asset (default false = summary only).")]
            public bool Raw { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                string assetPath = p.RequireString("asset");
                string trackSel = p.OptString("track");
                bool raw = p.OptBool("raw", false);

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                if (timeline == null)
                    return ToolEnvelope.Error("NOT_FOUND", $"No TimelineAsset at '{assetPath}'.");

                var tracks = new List<object>();
                int clipTotal = 0;
                int index = 0;
                foreach (var track in timeline.GetOutputTracks())
                {
                    int thisIndex = index++;
                    if (!string.IsNullOrEmpty(trackSel) &&
                        track.name != trackSel && thisIndex.ToString() != trackSel)
                        continue;

                    var clips = new List<object>();
                    foreach (var clip in track.GetClips())
                    {
                        clipTotal++;
                        JObject clipFields = null;
                        if (raw && clip.asset != null)
                        {
                            try { clipFields = TimelineReflect.ReadSerializedFields(clip.asset); }
                            catch { clipFields = null; }
                        }
                        clips.Add(new
                        {
                            displayName = clip.displayName,
                            start = clip.start,
                            duration = clip.duration,
                            blendIn = clip.blendInDuration,
                            blendOut = clip.blendOutDuration,
                            caps = clip.clipCaps.ToString(),
                            type = clip.asset != null ? clip.asset.GetType().Name : null,
                            fields = clipFields,
                        });
                    }

                    JObject trackFields = null;
                    if (raw)
                    {
                        try { trackFields = TimelineReflect.ReadSerializedFields(track); }
                        catch { trackFields = null; }
                    }

                    tracks.Add(new
                    {
                        index = thisIndex,
                        name = track.name,
                        type = track.GetType().Name,
                        muted = track.muted,
                        clipCount = clips.Count,
                        fields = trackFields,
                        clips,
                    });
                }

                return ToolEnvelope.Ok(
                    $"{System.IO.Path.GetFileName(assetPath)}: {tracks.Count} track(s), {clipTotal} clip(s).",
                    result: new { asset = assetPath, tracks });
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
