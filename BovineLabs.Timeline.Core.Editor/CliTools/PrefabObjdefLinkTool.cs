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
        Description = "Set (or clear) a prefab's ObjectDefinitionAuthoring.Definition back-link — adding the authoring component if absent. Reversible (its own inverse restores the prior Definition), so it fixes the broken-back-link silent spawn trap and replays cleanly.")]
    public static class PrefabObjdefLinkTool
    {
        public class Parameters
        {
            [ToolParameter("Prefab asset path.", Required = true)]
            public string Prefab { get; set; }

            [ToolParameter("ObjectDefinition asset path to link; null/omitted clears the link.")]
            public string Definition { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string prefabPath = p.RequireString("prefab");
                bool clear = !p.Has("definition");
                string defPath = p.OptString("definition");

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

                    // Clear request against a prefab that has no authoring is already in the
                    // desired state — do NOT add (and save) an empty component for a no-op.
                    // This also makes link-mode's add fully reversible: its inverse (a clear)
                    // removes the freshly-added authoring below rather than orphaning it.
                    if (clear && authoring == null)
                    {
                        return ToolEnvelope.Ok(
                            $"Cleared back-link on {prefabPath}.",
                            result: new { prefab = prefabPath, definition = (string)null },
                            pre: new { priorDefinition = (string)null },
                            undo: new object[0]);
                    }

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
                        // Removing the authoring (rather than nulling Definition) is the true
                        // "no back-link" state and lets a link-mode add be undone with no residue:
                        // the inverse clear deletes the component the add introduced.
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
                    new { tool = "prefab_objdef_link", @params = new { prefab = prefabPath, definition = priorDefPath } },
                };

                return ToolEnvelope.Ok(
                    clear ? $"Cleared back-link on {prefabPath}." : $"Linked {prefabPath} -> {defPath}.",
                    result: new { prefab = prefabPath, definition = clear ? null : defPath },
                    pre: new { priorDefinition = priorDefPath },
                    undo: undo);
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
