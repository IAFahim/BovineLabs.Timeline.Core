using System.Linq;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "prefab_objdef_link",
        Group = "vex",
        Description =
            "Set (or clear) a prefab's ObjectDefinitionAuthoring.Definition back-link — adding the authoring component if absent. Reversible (its own inverse restores the prior Definition), so it fixes the broken-back-link silent spawn trap and replays cleanly.")]
    public static class PrefabObjdefLinkTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var prefabPath = p.RequireString("prefab");
                var clear = !p.Has("definition");
                var defPath = p.OptString("definition");

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    return ToolEnvelope.Error("NOT_FOUND", $"No prefab GameObject at '{prefabPath}'.");

                Object defAsset = null;
                if (!clear)
                {
                    defAsset = AssetDatabase.LoadAssetAtPath<Object>(defPath);
                    if (defAsset == null) return ToolEnvelope.Error("NOT_FOUND", $"No asset at '{defPath}'.");
                }

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                string priorDefPath;
                try
                {
                    var authoring = root.GetComponentsInChildren<Component>(true)
                        .FirstOrDefault(c => c != null && c.GetType().Name == "ObjectDefinitionAuthoring");

                    if (clear && authoring == null)
                        return ToolEnvelope.Ok(
                            $"Cleared back-link on {prefabPath}.",
                            new { prefab = prefabPath, definition = (string)null },
                            new { priorDefinition = (string)null },
                            new object[0]);

                    if (authoring == null)
                        authoring = root.AddComponent(TimelineReflect.ResolveType("ObjectDefinitionAuthoring"));

                    var aso = new SerializedObject(authoring);
                    var defProp = aso.FindProperty("Definition");
                    if (defProp == null)
                        return ToolEnvelope.Error("NOT_FOUND", "ObjectDefinitionAuthoring has no 'Definition' field.");

                    var prior = defProp.objectReferenceValue;
                    priorDefPath = prior != null ? AssetDatabase.GetAssetPath(prior) : null;

                    if (clear)
                    {
                        Object.DestroyImmediate(authoring, true);
                    }
                    else
                    {
                        defProp.objectReferenceValue = defAsset;
                        aso.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                var undo = new object[]
                {
                    new
                    {
                        tool = "prefab_objdef_link", @params = new { prefab = prefabPath, definition = priorDefPath }
                    }
                };

                return ToolEnvelope.Ok(
                    clear ? $"Cleared back-link on {prefabPath}." : $"Linked {prefabPath} -> {defPath}.",
                    new { prefab = prefabPath, definition = clear ? null : defPath },
                    new { priorDefinition = priorDefPath },
                    undo);
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("Prefab asset path.", Required = true)]
            public string Prefab { get; set; }

            [ToolParameter("ObjectDefinition asset path to link; null/omitted clears the link.")]
            public string Definition { get; set; }
        }
    }
}