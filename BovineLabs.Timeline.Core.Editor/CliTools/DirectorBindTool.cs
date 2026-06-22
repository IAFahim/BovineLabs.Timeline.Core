using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "director_bind",
        Group = "vex",
        Description =
            "Set a director's playableAsset and generic bindings (SetGenericBinding of a COMPONENT), with full PRE-capture so the returned undo restores the prior asset + binding table. §4 wiring + §6 UNDO-1.")]
    public static class DirectorBindTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var directorSel = p.RequireString("director");

                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;

                    var go = session.Find(directorSel);
                    if (go == null)
                        return ToolEnvelope.Error("NOT_FOUND", $"No object '{directorSel}' in {session.SubscenePath}.");
                    var d = go.GetComponent<PlayableDirector>();
                    if (d == null) return ToolEnvelope.Error("NOT_FOUND", $"'{directorSel}' has no PlayableDirector.");

                    var pre = Capture.Director(d);

                    var preTimeline = d.playableAsset as TimelineAsset;
                    var clearList = p.OptArray("clear");
                    if (clearList != null && preTimeline != null)
                        foreach (var ct in clearList)
                        {
                            var ctrack = TimelineReflect.FindTrack(preTimeline, ct.ToString());
                            if (ctrack != null) d.ClearGenericBinding(ctrack);
                        }

                    if (p.Has("asset") || p.IsExplicitNull("asset"))
                    {
                        if (p.IsExplicitNull("asset"))
                        {
                            d.playableAsset = null;
                        }
                        else
                        {
                            var ap = p.OptString("asset");
                            var pa = AssetDatabase.LoadAssetAtPath<PlayableAsset>(ap);
                            if (pa == null) return ToolEnvelope.Error("NOT_FOUND", $"No PlayableAsset at '{ap}'.");
                            d.playableAsset = pa;
                        }
                    }

                    var timeline = d.playableAsset as TimelineAsset;
                    var appliedTracks = new List<string>();

                    var bindings = p.OptArray("bindings");
                    if (bindings != null && bindings.Count > 0)
                    {
                        if (timeline == null)
                            return ToolEnvelope.Error("MISSING_PREREQUISITE",
                                "Director has no TimelineAsset to bind tracks on (set 'asset' first).");

                        foreach (var bt in bindings)
                        {
                            if (!(bt is JObject bo))
                                return ToolEnvelope.Error("BAD_VALUE",
                                    "Each binding must be an object {track, object, component}.");

                            var trackSel = bo["track"]?.ToString();
                            var track = TimelineReflect.FindTrack(timeline, trackSel);
                            if (track == null)
                                return ToolEnvelope.Error("NOT_FOUND", $"No track '{trackSel}' in the current asset.");

                            var objPath = bo["object"]?.ToString();
                            var tgt = session.Find(objPath);
                            if (tgt == null)
                                return ToolEnvelope.Error("SCENE_REF_REFUSED",
                                    $"Bind target '{objPath}' is not in the SubScene (only SubScene-baked objects bind).");

                            var compName = bo["component"]?.ToString();
                            Object bindObj;
                            if (string.IsNullOrEmpty(compName) || compName == "GameObject")
                            {
                                bindObj = tgt;
                            }
                            else
                            {
                                var comp = tgt.GetComponent(compName);
                                if (comp == null)
                                    return ToolEnvelope.Error("NOT_FOUND",
                                        $"'{objPath}' has no component '{compName}'.");
                                bindObj = comp;
                            }

                            d.SetGenericBinding(track, bindObj);
                            appliedTracks.Add(track.name);
                        }
                    }

                    EditorUtility.SetDirty(d);
                    session.Save();

                    var post = Capture.Director(d);

                    var undoBindings = new List<object>();
                    foreach (var b in pre.bindings)
                        if (b.boundPath != null)
                            undoBindings.Add(new
                                { track = b.trackName, @object = b.boundPath, component = b.boundComponentType });

                    var undoClear = new List<string>();
                    foreach (var tn in appliedTracks)
                        if (!pre.bindings.Exists(b => b.trackName == tn && b.boundPath != null))
                            undoClear.Add(tn);

                    var undo = new object[]
                    {
                        new
                        {
                            tool = "director_bind", @params = new
                            {
                                subscene = session.SubscenePath,
                                director = pre.path,
                                asset = pre.playableAsset,
                                bindings = undoBindings,
                                clear = undoClear
                            }
                        }
                    };

                    return ToolEnvelope.Ok(
                        $"Bound {appliedTracks.Count} track(s) on {pre.path}.",
                        new { director = pre.path, post.playableAsset, post.bindings },
                        pre,
                        undo);
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("Director hierarchy path/name in the subscene.", Required = true)]
            public string Director { get; set; }

            [ToolParameter("The .playable to assign as playableAsset; null clears; omit = leave as-is.")]
            public string Asset { get; set; }

            [ToolParameter(
                "Array of { track: name/index, object: hierarchyPath, component: typeName | \"GameObject\" }.")]
            public object[] Bindings { get; set; }

            [ToolParameter("Array of track names whose binding to ClearGenericBinding.")]
            public string[] Clear { get; set; }
        }
    }
}