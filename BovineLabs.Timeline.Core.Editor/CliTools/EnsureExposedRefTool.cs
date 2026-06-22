using System.Reflection;
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
        Name = "ensure_exposed_ref",
        Group = "vex",
        Description =
            "Idempotent: ensure a clip's ExposedReference<T> field resolves to a SubScene object via the director. already-ok when it already points there; otherwise wires it via exposed_ref_wire (passing its two-sided undo through). dry_run reports without mutating.")]
    public static class EnsureExposedRefTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var directorSel = p.RequireString("director");
                var assetPath = p.RequireString("asset");
                var trackSel = p.RequireString("track");
                var clipSel = p.RequireString("clip");
                var field = p.RequireString("field");
                var targetPath = p.RequireString("target");
                var dryRun = p.OptBool("dry_run", false);
                var target = new
                    { director = directorSel, track = trackSel, clip = clipSel, field, target = targetPath };

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

                    var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                    if (timeline == null) return ToolEnvelope.Error("NOT_FOUND", $"No TimelineAsset at '{assetPath}'.");
                    var track = TimelineReflect.FindTrack(timeline, trackSel);
                    if (track == null) return ToolEnvelope.Error("NOT_FOUND", $"No track '{trackSel}' in {assetPath}.");
                    var clip = TimelineReflect.FindClip(track, clipSel);
                    if (clip == null)
                        return ToolEnvelope.Error("NOT_FOUND", $"No clip '{clipSel}' on track '{track.name}'.");
                    var clipAsset = clip.asset;
                    if (clipAsset == null) return ToolEnvelope.Error("NOT_FOUND", $"Clip '{clipSel}' has no asset.");

                    var fi = clipAsset.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                    if (fi == null)
                        return ToolEnvelope.Error("NOT_FOUND", $"No field '{field}' on {clipAsset.GetType().Name}.");
                    var ft = fi.FieldType;
                    if (!(ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(ExposedReference<>)))
                        return ToolEnvelope.Error("BAD_VALUE",
                            $"Field '{field}' is not an ExposedReference<T> (it is {ft.Name}).");

                    var exposedNameField = ft.GetField("exposedName");
                    var er = fi.GetValue(clipAsset);
                    var exposed = (PropertyName)exposedNameField.GetValue(er);

                    string wiredPath = null;
                    if (!(exposed == new PropertyName()))
                    {
                        var wired = d.GetReferenceValue(exposed, out var valid);
                        if (valid && wired is Component wc) wiredPath = Hierarchy.PathOf(wc.gameObject);
                        else if (valid && wired is GameObject wg) wiredPath = Hierarchy.PathOf(wg);
                    }

                    satisfied = wiredPath == targetPath;
                    before = new { wiredPath };
                }

                if (satisfied)
                    return EnsureResult.Satisfied($"{clipSel}.{field} already wired to {targetPath}.", target, before);
                if (dryRun)
                    return EnsureResult.WouldFixResult($"Would wire {clipSel}.{field} -> {targetPath}.", target,
                        before);

                var wireParams = new JObject
                {
                    ["subscene"] = p.OptString("subscene"),
                    ["director"] = directorSel,
                    ["asset"] = assetPath,
                    ["track"] = trackSel,
                    ["clip"] = clipSel,
                    ["field"] = field,
                    ["target"] = targetPath
                };
                var resp = ExposedRefWireTool.HandleCommand(wireParams);
                if (Responses.IsError(resp)) return resp;

                return EnsureResult.Applied($"Wired {clipSel}.{field} -> {targetPath}.",
                    target, before, new { wiredPath = targetPath }, Responses.Undo(resp));
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

            [ToolParameter("The .playable containing the clip.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track name/index.", Required = true)]
            public string Track { get; set; }

            [ToolParameter("Clip display name/index.", Required = true)]
            public string Clip { get; set; }

            [ToolParameter("The ExposedReference<T> field name.", Required = true)]
            public string Field { get; set; }

            [ToolParameter("SubScene object hierarchy path to reference.", Required = true)]
            public string Target { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }
    }
}