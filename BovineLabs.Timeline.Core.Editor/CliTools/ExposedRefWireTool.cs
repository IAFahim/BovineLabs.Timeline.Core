using System;
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
        Name = "exposed_ref_wire",
        Group = "vex",
        Description =
            "Wire an ExposedReference<T> clip field to a scene object — the correct asset->scene bridge. Two-sided: mints a GUID into the clip field (asset) + director.SetReferenceValue (scene), two saves. Undo clears the outgoing GUID so no orphan table entry survives.")]
    public static class ExposedRefWireTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var directorSel = p.RequireString("director");
                var assetPath = p.RequireString("asset");
                var trackSel = p.RequireString("track");
                var clipSel = p.RequireString("clip");
                var field = p.RequireString("field");
                var targetIsNull = !p.Has("target");
                var targetPath = p.OptString("target");
                var hasExposedName = p.Has("exposed_name") || p.IsExplicitNull("exposed_name");

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
                    var elemType = ft.GetGenericArguments()[0];
                    var exposedNameField = ft.GetField("exposedName");

                    var curER = fi.GetValue(clipAsset);
                    var outgoing = (PropertyName)exposedNameField.GetValue(curER);
                    var outgoingEmpty = outgoing == new PropertyName();
                    var priorExposedNameStr = outgoingEmpty ? "" : outgoing.ToString();

                    string priorTargetPath = null;
                    if (!outgoingEmpty)
                    {
                        var priorObj = d.GetReferenceValue(outgoing, out var pv);
                        if (pv && priorObj is Component pc) priorTargetPath = Hierarchy.PathOf(pc.gameObject);
                        else if (pv && priorObj is GameObject pg) priorTargetPath = Hierarchy.PathOf(pg);
                    }

                    PropertyName newPn;
                    string newExposedStr;
                    if (hasExposedName)
                    {
                        var en = p.OptString("exposed_name") ?? "";
                        newPn = string.IsNullOrEmpty(en) ? new PropertyName() : new PropertyName(en);
                        newExposedStr = en;
                    }
                    else if (!outgoingEmpty)
                    {
                        newPn = outgoing;
                        newExposedStr = priorExposedNameStr;
                    }
                    else
                    {
                        var g = Guid.NewGuid().ToString();
                        newPn = new PropertyName(g);
                        newExposedStr = g;
                    }

                    if (!outgoingEmpty && !(outgoing == newPn))
                        d.ClearReferenceValue(outgoing);

                    var newER = Activator.CreateInstance(ft);
                    exposedNameField.SetValue(newER, newPn);
                    fi.SetValue(clipAsset, newER);
                    EditorUtility.SetDirty(clipAsset);
                    AssetDatabase.SaveAssets();

                    if (!targetIsNull)
                    {
                        if (newPn == new PropertyName())
                            return ToolEnvelope.Error("BAD_VALUE", "Cannot wire a target with an empty exposed_name.");
                        var tgo = session.Find(targetPath);
                        if (tgo == null)
                            return ToolEnvelope.Error("SCENE_REF_REFUSED",
                                $"Target '{targetPath}' is not in the SubScene.");
                        var comp = tgo.GetComponent(elemType);
                        if (comp == null)
                            return ToolEnvelope.Error("NOT_FOUND",
                                $"Target '{targetPath}' has no {elemType.Name} component.");
                        d.SetReferenceValue(newPn, comp);
                    }
                    else if (!(newPn == new PropertyName()))
                    {
                        d.ClearReferenceValue(newPn);
                    }

                    EditorUtility.SetDirty(d);
                    session.Save();

                    string wiredName = null;
                    var idValid = false;
                    if (!(newPn == new PropertyName()))
                    {
                        var wired = d.GetReferenceValue(newPn, out idValid);
                        wiredName = wired != null ? wired.name : null;
                    }

                    var undo = new object[]
                    {
                        new
                        {
                            tool = "exposed_ref_wire", @params = new
                            {
                                subscene = session.SubscenePath,
                                director = directorSel,
                                asset = assetPath,
                                track = trackSel,
                                clip = clipSel,
                                field,
                                target = priorTargetPath,
                                exposed_name = priorExposedNameStr
                            }
                        }
                    };

                    return ToolEnvelope.Ok(
                        $"Wired {track.name}/{clip.displayName}.{field} -> {(targetIsNull ? "(cleared)" : targetPath)} (exposedName {newExposedStr}).",
                        new
                        {
                            exposedName = newExposedStr,
                            assetSideSaved = true,
                            sceneSideSaved = true,
                            verify = new { getReferenceValue = wiredName, idValid }
                        },
                        new { priorExposedName = priorExposedNameStr, priorTarget = priorTargetPath },
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

            [ToolParameter("Director hierarchy path/name.", Required = true)]
            public string Director { get; set; }

            [ToolParameter("The .playable containing the clip.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Locate the track (name or index).", Required = true)]
            public string Track { get; set; }

            [ToolParameter("Locate the clip (display name or index).", Required = true)]
            public string Clip { get; set; }

            [ToolParameter("The ExposedReference<T> field name (e.g. Target).", Required = true)]
            public string Field { get; set; }

            [ToolParameter("Scene object hierarchy path to reference; null/omitted clears.")]
            public string Target { get; set; }

            [ToolParameter(
                "Force a specific exposedName GUID on the asset side (\"\" restores a previously-empty field). Default: mint new. Used by undo.")]
            public string ExposedName { get; set; }
        }
    }
}