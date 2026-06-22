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
        Name = "ensure_binding",
        Group = "vex",
        Description =
            "Idempotent: ensure a director's track is bound to a component on a SubScene object. already-ok when the binding already matches; otherwise sets it via director_bind (passing its undo through). dry_run reports without mutating.")]
    public static class EnsureBindingTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var directorSel = p.RequireString("director");
                var assetPath = p.RequireString("asset");
                var trackSel = p.RequireString("track");
                var objPath = p.RequireString("object");
                var component = p.RequireString("component");
                var dryRun = p.OptBool("dry_run", false);
                var target = new { director = directorSel, track = trackSel, @object = objPath, component };

                bool satisfied;
                object before;
                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;

                    var go = session.Find(directorSel);
                    if (go == null)
                        return ToolEnvelope.Error("NOT_FOUND", $"No object '{directorSel}' in {session.SubscenePath}.");
                    var d = go.GetComponent<PlayableDirector>();
                    if (d == null) return ToolEnvelope.Error("NOT_FOUND", $"'{directorSel}' has no PlayableDirector.");

                    var timeline = d.playableAsset as TimelineAsset;
                    string boundPath = null, boundComp = null;
                    if (timeline != null && AssetDatabase.GetAssetPath(timeline) == assetPath)
                    {
                        var track = TimelineReflect.FindTrack(timeline, trackSel);
                        if (track != null)
                        {
                            var bound = d.GetGenericBinding(track);
                            if (bound is Component bc)
                            {
                                boundPath = Hierarchy.PathOf(bc.gameObject);
                                boundComp = bc.GetType().Name;
                            }
                            else if (bound is GameObject bg)
                            {
                                boundPath = Hierarchy.PathOf(bg);
                                boundComp = "GameObject";
                            }
                        }
                    }

                    satisfied = boundPath == objPath && boundComp == component;
                    before = new { assetAssigned = timeline != null, boundPath, boundComponent = boundComp };
                }

                if (satisfied)
                    return EnsureResult.Satisfied($"{trackSel} already bound to {objPath}.{component}.", target,
                        before);
                if (dryRun)
                    return EnsureResult.WouldFixResult($"Would bind {trackSel} -> {objPath}.{component}.", target,
                        before);

                var bindParams = new JObject
                {
                    ["subscene"] = p.OptString("subscene"),
                    ["director"] = directorSel,
                    ["asset"] = assetPath,
                    ["bindings"] = new JArray(new JObject
                    {
                        ["track"] = trackSel,
                        ["object"] = objPath,
                        ["component"] = component
                    })
                };
                var resp = DirectorBindTool.HandleCommand(bindParams);
                if (Responses.IsError(resp)) return resp;

                return EnsureResult.Applied($"Bound {trackSel} -> {objPath}.{component}.",
                    target, before, new { boundPath = objPath, boundComponent = component }, Responses.Undo(resp));
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

            [ToolParameter("Director hierarchy path/name.", Required = true)]
            public string Director { get; set; }

            [ToolParameter("The .playable the director should run.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track name/index to bind.", Required = true)]
            public string Track { get; set; }

            [ToolParameter("SubScene object hierarchy path to bind to.", Required = true)]
            public string Object { get; set; }

            [ToolParameter("Component type name on that object (or \"GameObject\").", Required = true)]
            public string Component { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }
    }
}