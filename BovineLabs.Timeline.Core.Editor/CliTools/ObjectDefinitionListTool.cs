using System;
using System.Collections.Generic;
using System.Linq;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "object_definition_list",
        Group = "vex",
        Description =
            "Lists every ObjectDefinition asset (the spawn name-tags): assetPath, runtime id, referenced prefab, and whether the prefab's ObjectDefinitionAuthoring back-link points at the same definition. Surfaces the two silent-spawn traps — duplicate / zero ids and broken back-links (§3.4 objdef sweep). READ-ONLY project asset scan.")]
    public static class ObjectDefinitionListTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var filter = p.OptString("filter");

                var guids = AssetDatabase.FindAssets("t:ObjectDefinition");
                var definitions = new List<object>();
                var idToPaths = new Dictionary<int, List<string>>();

                foreach (var guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;
                    if (!string.IsNullOrEmpty(filter) &&
                        assetPath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var def = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (def == null)
                        continue;

                    var so = new SerializedObject(def);
                    var id = so.FindProperty("id")?.intValue ?? 0;
                    var prefabProp = so.FindProperty("prefab");
                    var prefab = prefabProp?.objectReferenceValue as GameObject;
                    var prefabPath = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
                    var friendlyName = so.FindProperty("friendlyName")?.stringValue;
                    if (string.IsNullOrWhiteSpace(friendlyName))
                        friendlyName = def.name;

                    if (!idToPaths.TryGetValue(id, out var list))
                        idToPaths[id] = list = new List<string>();
                    list.Add(assetPath);

                    bool? backLinkOk = null;
                    string backLink = null;
                    if (prefab == null)
                    {
                        backLink = "NO_PREFAB";
                    }
                    else
                    {
                        var authoring = prefab.GetComponentsInChildren<Component>(true)
                            .FirstOrDefault(c => c != null && c.GetType().Name == "ObjectDefinitionAuthoring");
                        if (authoring == null)
                        {
                            backLink = "NO_AUTHORING";
                            backLinkOk = false;
                        }
                        else
                        {
                            var aso = new SerializedObject(authoring);
                            var defRef = aso.FindProperty("Definition")?.objectReferenceValue;
                            if (defRef == null)
                            {
                                backLink = "NULL_DEFINITION";
                                backLinkOk = false;
                            }
                            else if (defRef == def)
                            {
                                backLink = "OK";
                                backLinkOk = true;
                            }
                            else
                            {
                                backLink = "WRONG_DEFINITION:" + AssetDatabase.GetAssetPath(defRef);
                                backLinkOk = false;
                            }
                        }
                    }

                    var idUsable = id > 0;

                    definitions.Add(new
                    {
                        assetPath,
                        friendlyName,
                        id,
                        idUsable,
                        prefab = prefabPath,
                        backLink,
                        backLinkOk
                    });
                }

                var duplicateIds = idToPaths
                    .Where(kv => kv.Value.Count > 1)
                    .Select(kv => new { id = kv.Key, assetPaths = kv.Value })
                    .ToList();
                var zeroIdPaths = idToPaths.TryGetValue(0, out var zeros) ? zeros : new List<string>();

                var problems = definitions.Count(d =>
                {
                    var t = d.GetType();
                    var idBad = !(bool)t.GetProperty("idUsable").GetValue(d);
                    var bl = (bool?)t.GetProperty("backLinkOk").GetValue(d);
                    return idBad || bl == false;
                });

                var msg = $"{definitions.Count} object definition(s).";
                if (duplicateIds.Count > 0)
                    msg += $" {duplicateIds.Count} duplicate id(s).";
                if (zeroIdPaths.Count > 0)
                    msg += $" {zeroIdPaths.Count} with id==0.";
                if (problems > 0)
                    msg += $" {problems} unusable/broken-back-link.";

                return ToolEnvelope.Ok(
                    msg,
                    new
                    {
                        definitions,
                        duplicateIds,
                        zeroIdPaths
                    });
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter(
                "Limit to definitions whose assetPath contains this substring (case-insensitive). Omit = every ObjectDefinition in the project.")]
            public string Filter { get; set; }
        }
    }
}