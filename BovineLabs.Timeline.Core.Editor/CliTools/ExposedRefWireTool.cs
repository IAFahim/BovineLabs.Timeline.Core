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
        Description = "Wire an ExposedReference<T> clip field to a scene object — the correct asset->scene bridge. Two-sided: mints a GUID into the clip field (asset) + director.SetReferenceValue (scene), two saves. Undo clears the outgoing GUID so no orphan table entry survives.")]
    public static class ExposedRefWireTool
    {
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

            [ToolParameter("Force a specific exposedName GUID on the asset side (\"\" restores a previously-empty field). Default: mint new. Used by undo.")]
            public string ExposedName { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string directorSel = p.RequireString("director");
                string assetPath = p.RequireString("asset");
                string trackSel = p.RequireString("track");
                string clipSel = p.RequireString("clip");
                string field = p.RequireString("field");
                bool targetIsNull = !p.Has("target"); // omitted or JSON null => clear
                string targetPath = p.OptString("target");
                bool hasExposedName = p.Has("exposed_name") || p.IsExplicitNull("exposed_name");

                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;

                    var go = session.Find(directorSel);
                    if (go == null) return ToolEnvelope.Error("NOT_FOUND", $"No object '{directorSel}' in {session.SubscenePath}.");
                    var d = go.GetComponent<PlayableDirector>();
                    if (d == null) return ToolEnvelope.Error("NOT_FOUND", $"'{directorSel}' has no PlayableDirector.");

                    var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                    if (timeline == null) return ToolEnvelope.Error("NOT_FOUND", $"No TimelineAsset at '{assetPath}'.");
                    var track = TimelineReflect.FindTrack(timeline, trackSel);
                    if (track == null) return ToolEnvelope.Error("NOT_FOUND", $"No track '{trackSel}' in {assetPath}.");
                    var clip = TimelineReflect.FindClip(track, clipSel);
                    if (clip == null) return ToolEnvelope.Error("NOT_FOUND", $"No clip '{clipSel}' on track '{track.name}'.");
                    var clipAsset = clip.asset;
                    if (clipAsset == null) return ToolEnvelope.Error("NOT_FOUND", $"Clip '{clipSel}' has no asset.");

                    // Reflect the ExposedReference<T> field.
                    var fi = clipAsset.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                    if (fi == null) return ToolEnvelope.Error("NOT_FOUND", $"No field '{field}' on {clipAsset.GetType().Name}.");
                    var ft = fi.FieldType;
                    if (!(ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(ExposedReference<>)))
                        return ToolEnvelope.Error("BAD_VALUE", $"Field '{field}' is not an ExposedReference<T> (it is {ft.Name}).");
                    var elemType = ft.GetGenericArguments()[0];
                    var exposedNameField = ft.GetField("exposedName");

                    // Outgoing exposedName currently on the field (the one to clear when changing it).
                    object curER = fi.GetValue(clipAsset);
                    var outgoing = (PropertyName)exposedNameField.GetValue(curER);
                    bool outgoingEmpty = outgoing == new PropertyName();
                    string priorExposedNameStr = outgoingEmpty ? "" : outgoing.ToString();

                    string priorTargetPath = null;
                    if (!outgoingEmpty)
                    {
                        var priorObj = d.GetReferenceValue(outgoing, out bool pv);
                        if (pv && priorObj is Component pc) priorTargetPath = Hierarchy.PathOf(pc.gameObject);
                        else if (pv && priorObj is GameObject pg) priorTargetPath = Hierarchy.PathOf(pg);
                    }

                    // Determine the new exposedName.
                    PropertyName newPn;
                    string newExposedStr;
                    if (hasExposedName)
                    {
                        string en = p.OptString("exposed_name") ?? "";
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
                        string g = Guid.NewGuid().ToString();
                        newPn = new PropertyName(g);
                        newExposedStr = g;
                    }

                    // Clear the OUTGOING table entry when we're changing it (closes the orphan-GUID hazard).
                    if (!outgoingEmpty && !(outgoing == newPn))
                        d.ClearReferenceValue(outgoing);

                    // ASSET SIDE: write the field's exposedName (save 1).
                    object newER = Activator.CreateInstance(ft);
                    exposedNameField.SetValue(newER, newPn);
                    fi.SetValue(clipAsset, newER);
                    EditorUtility.SetDirty(clipAsset);
                    AssetDatabase.SaveAssets();

                    // SCENE SIDE (save 2).
                    if (!targetIsNull)
                    {
                        if (newPn == new PropertyName())
                            return ToolEnvelope.Error("BAD_VALUE", "Cannot wire a target with an empty exposed_name.");
                        var tgo = session.Find(targetPath);
                        if (tgo == null)
                            return ToolEnvelope.Error("SCENE_REF_REFUSED", $"Target '{targetPath}' is not in the SubScene.");
                        var comp = tgo.GetComponent(elemType);
                        if (comp == null)
                            return ToolEnvelope.Error("NOT_FOUND", $"Target '{targetPath}' has no {elemType.Name} component.");
                        d.SetReferenceValue(newPn, comp);
                    }
                    else if (!(newPn == new PropertyName()))
                    {
                        d.ClearReferenceValue(newPn);
                    }
                    EditorUtility.SetDirty(d);
                    session.Save();

                    // Verify from the wired director.
                    string wiredName = null;
                    bool idValid = false;
                    if (!(newPn == new PropertyName()))
                    {
                        var wired = d.GetReferenceValue(newPn, out idValid);
                        wiredName = wired != null ? wired.name : null;
                    }

                    var undo = new object[]
                    {
                        new { tool = "exposed_ref_wire", @params = new
                        {
                            subscene = session.SubscenePath,
                            director = directorSel,
                            asset = assetPath,
                            track = trackSel,
                            clip = clipSel,
                            field,
                            target = priorTargetPath,
                            exposed_name = priorExposedNameStr,
                        } },
                    };

                    return ToolEnvelope.Ok(
                        $"Wired {track.name}/{clip.displayName}.{field} -> {(targetIsNull ? "(cleared)" : targetPath)} (exposedName {newExposedStr}).",
                        result: new
                        {
                            exposedName = newExposedStr,
                            assetSideSaved = true,
                            sceneSideSaved = true,
                            verify = new { getReferenceValue = wiredName, idValid },
                        },
                        pre: new { priorExposedName = priorExposedNameStr, priorTarget = priorTargetPath },
                        undo: undo);
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
