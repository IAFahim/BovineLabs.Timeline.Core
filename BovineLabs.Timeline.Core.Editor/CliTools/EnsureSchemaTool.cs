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
        Name = "ensure_schema",
        Group = "vex",
        Description =
            "Idempotent: ensure a registered schema asset (StatSchemaObject / IntrinsicSchemaObject / EntityLinkSchema / …) of a given type and name exists with a non-zero AutoRef id. already-ok when it exists and is registered; otherwise imports (to assign the id) or creates it at 'path'. dry_run reports without mutating. Never hand-edits the settings registry.")]
    public static class EnsureSchemaTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var schemaTypeName = p.RequireString("schema_type");
                var name = p.RequireString("name");
                var dryRun = p.OptBool("dry_run", false);

                var schemaType = TimelineReflect.ResolveType(schemaTypeName);
                var target = new { schemaType = schemaType.FullName, name };

                var existing = FindByName(schemaType.Name, name, out var existingPath, out var existingId);
                var registered = existing != null && existingId > 0;
                if (registered)
                    return EnsureResult.Satisfied(
                        $"Schema '{name}' ({schemaType.Name}) present and registered (id {existingId}).",
                        target, new { path = existingPath, id = existingId });

                if (dryRun)
                {
                    var what = existing == null ? "create" : "import to register";
                    return EnsureResult.WouldFixResult($"Would {what} schema '{name}' ({schemaType.Name}).", target,
                        new { exists = existing != null, id = existingId });
                }

                var undo = new List<object>();
                var path = existingPath;
                if (existing == null)
                {
                    path = ResolveCreatePath(p.OptString("path"), name);
                    if (string.IsNullOrEmpty(path))
                        return ToolEnvelope.Error("MISSING_PREREQUISITE",
                            $"Schema '{name}' ({schemaType.Name}) not found and no 'path' given to create it.");
                    var pre = Capture.AssetExistence(path);
                    AssetUtil.EnsureFolders(path);
                    var inst = ScriptableObject.CreateInstance(schemaType);
                    AssetDatabase.CreateAsset(inst, path);
                    undo.Add(new
                    {
                        tool = "asset_delete",
                        @params = new { asset = path, folder_if_empty = pre.folderExisted ? null : pre.folder }
                    });
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();

                FindByName(schemaType.Name, name, out var finalPath, out var finalId);
                var note = finalId > 0 ? $"id {finalId}" : "id still pending (postprocessor)";
                return EnsureResult.Applied($"Ensured schema '{name}' ({schemaType.Name}, {note}).",
                    target, new { exists = existing != null, id = existingId }, new { path = finalPath, id = finalId },
                    undo.ToArray());
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        private static Object FindByName(string typeName, string name, out string path, out int id)
        {
            path = null;
            id = 0;

            var matches = new List<(string path, Object asset)>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeName} {name}"))
            {
                var ap = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(ap);
                if (asset == null || asset.name != name) continue;
                matches.Add((ap, asset));
            }

            if (matches.Count == 0) return null;

            matches.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
            if (matches.Count > 1)
                throw new ToolException("AMBIGUOUS",
                    $"Schema '{name}' ({typeName}) matches {matches.Count} assets — pass a more specific name or remove the duplicate.",
                    matches.Select(m => m.path).ToArray());

            path = matches[0].path;
            id = new SerializedObject(matches[0].asset).FindProperty("id")?.intValue ?? 0;
            return matches[0].asset;
        }

        private static string ResolveCreatePath(string pathOrFolder, string name)
        {
            if (string.IsNullOrEmpty(pathOrFolder)) return null;
            if (pathOrFolder.EndsWith(".asset")) return pathOrFolder;
            return $"{pathOrFolder.TrimEnd('/')}/{name}.asset";
        }

        public class Parameters
        {
            [ToolParameter("Schema ScriptableObject type name, simple or full (e.g. EntityLinkSchema).",
                Required = true)]
            public string SchemaType { get; set; }

            [ToolParameter("Asset name to find or create (matched against the asset file name).", Required = true)]
            public string Name { get; set; }

            [ToolParameter("Asset path or folder to create at when missing (required only if it must be created).")]
            public string Path { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }
    }
}