using System.Collections.Generic;
using System.Linq;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "ensure_objdef",
        Group = "vex",
        Description = "Idempotent: ensure an ObjectDefinition asset exists, points at a prefab, is registered (id>0 via the AutoRef postprocessor — never hand-edits the settings array), and the prefab's back-link points home. Fixes the two silent spawn traps (id==0, broken back-link). dry_run reports without mutating. Undo deletes a freshly-created definition and restores any changed back-link.")]
    public static class EnsureObjdefTool
    {
        public class Parameters
        {
            [ToolParameter("ObjectDefinition asset path (created if missing).", Required = true)]
            public string Definition { get; set; }

            [ToolParameter("Payload prefab path the definition spawns.", Required = true)]
            public string Prefab { get; set; }

            [ToolParameter("Friendly name (default = asset file name).")]
            public string FriendlyName { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string defPath = p.RequireString("definition");
                string prefabPath = p.RequireString("prefab");
                string friendlyName = p.OptString("friendly_name");
                bool dryRun = p.OptBool("dry_run", false);
                var target = new { definition = defPath, prefab = prefabPath };

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    return ToolEnvelope.Error("NOT_FOUND", $"No prefab GameObject at '{prefabPath}'.");

                var state = Inspect(defPath, prefabPath);
                if (state.Satisfied)
                    return EnsureResult.Satisfied($"ObjectDefinition '{defPath}' ok (id {state.Id}, back-link OK).", target, state.Before);

                if (dryRun)
                    return EnsureResult.WouldFixResult($"Would repair ObjectDefinition '{defPath}'.", target, state.Before);

                var undo = new List<object>();

                // 1. Create the definition asset at the exact path (deterministic — not OMUtility's unique path).
                if (!state.Exists)
                {
                    var pre = Capture.AssetExistence(defPath);
                    AssetUtil.EnsureFolders(defPath);
                    var defType = TimelineReflect.ResolveType("ObjectDefinition");
                    var inst = ScriptableObject.CreateInstance(defType);
                    AssetDatabase.CreateAsset(inst, defPath);
                    undo.Add(new { tool = "asset_delete", @params = new { asset = defPath, folder_if_empty = pre.folderExisted ? null : pre.folder } });
                }

                // 2. Set prefab + friendly name on the definition.
                var def = AssetDatabase.LoadMainAssetAtPath(defPath);
                var dso = new SerializedObject(def);
                dso.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!string.IsNullOrEmpty(friendlyName))
                    dso.FindProperty("friendlyName").stringValue = friendlyName;
                dso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);

                // 3. Force a synchronous import so the AutoRef postprocessor assigns a non-zero id + registers it.
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(defPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();

                // 4. Repair the prefab back-link (reversible via prefab_objdef_link).
                if (!state.BackLinkOk)
                {
                    var resp = PrefabObjdefLinkTool.HandleCommand(new JObject { ["prefab"] = prefabPath, ["definition"] = defPath });
                    if (Responses.IsError(resp)) { RollbackPartial(undo); return resp; }
                    var u = Responses.Undo(resp);
                    if (u != null) undo.AddRange(u);
                }

                var after = Inspect(defPath, prefabPath);
                string note = after.Id > 0 ? $"id {after.Id}" : "id still pending (postprocessor)";
                return EnsureResult.Applied($"Repaired ObjectDefinition '{defPath}' ({note}).",
                    target, state.Before, after.Before, undo.ToArray());
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        private sealed class DefState
        {
            public bool Exists;
            public int Id;
            public bool PrefabOk;
            public bool BackLinkOk;
            public object Before;
            public bool Satisfied => Exists && Id > 0 && PrefabOk && BackLinkOk;
        }

        private static DefState Inspect(string defPath, string prefabPath)
        {
            var s = new DefState();
            var def = AssetDatabase.LoadMainAssetAtPath(defPath);
            s.Exists = def != null;
            if (s.Exists)
            {
                var so = new SerializedObject(def);
                s.Id = so.FindProperty("id")?.intValue ?? 0;
                var prefabRef = so.FindProperty("prefab")?.objectReferenceValue;
                s.PrefabOk = prefabRef != null && AssetDatabase.GetAssetPath(prefabRef) == prefabPath;
                s.BackLinkOk = BackLinkPointsTo(prefabPath, def);
            }
            s.Before = new { exists = s.Exists, id = s.Id, prefabOk = s.PrefabOk, backLinkOk = s.BackLinkOk };
            return s;
        }

        private static bool BackLinkPointsTo(string prefabPath, Object def)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;
            var authoring = prefab.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name == "ObjectDefinitionAuthoring");
            if (authoring == null) return false;
            var aso = new SerializedObject(authoring);
            return aso.FindProperty("Definition")?.objectReferenceValue == def;
        }

        private static void RollbackPartial(List<object> undo)
        {
            if (undo.Count == 0) return;
            var rev = new List<object>(undo);
            rev.Reverse();
            ToolUndoTool.HandleCommand(new JObject { ["undo"] = JArray.FromObject(rev) });
        }
    }
}
