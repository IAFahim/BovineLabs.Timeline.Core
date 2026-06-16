using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "project_doctor",
        Group = "vex",
        Description = "READ-ONLY project health scan. Finds folders that contain more than one .asmdef file — Unity refuses to compile ANY assembly in such a folder, and the failure surfaces elsewhere as a confusing 'metadata file <X>.dll could not be found', blocking the whole editor and every CLI tool. Run this FIRST whenever the editor 'won't compile' or a tool cannot reach the editor. Default scans Assets + Packages.")]
    public static class ProjectDoctorTool
    {
        public class Parameters
        {
            [ToolParameter("Project-relative folder to scan (e.g. 'Assets' or 'Packages'). Omit = scan both Assets and Packages.")]
            public string Root { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
                string root = p.OptString("root");
                var roots = string.IsNullOrEmpty(root)
                    ? new[] { "Assets", "Packages" }
                    : new[] { root };

                var byFolder = new Dictionary<string, List<string>>();
                foreach (var rel in roots)
                {
                    var abs = Path.Combine(projectRoot, rel);
                    if (!Directory.Exists(abs))
                        continue;

                    foreach (var file in Directory.GetFiles(abs, "*.asmdef", SearchOption.AllDirectories))
                    {
                        var folder = Path.GetDirectoryName(file)!.Replace('\\', '/');
                        var folderRel = folder.StartsWith(projectRoot.Replace('\\', '/'))
                            ? folder.Substring(projectRoot.Length).TrimStart('/')
                            : folder;
                        if (!byFolder.TryGetValue(folderRel, out var list))
                            byFolder[folderRel] = list = new List<string>();
                        list.Add(Path.GetFileName(file));
                    }
                }

                var conflicts = new List<object>();
                foreach (var kv in byFolder)
                    if (kv.Value.Count > 1)
                        conflicts.Add(new { folder = kv.Key, asmdefs = kv.Value.ToArray() });

                bool healthy = conflicts.Count == 0;
                string scanned = string.Join(", ", roots);
                string msg = healthy
                    ? $"No asmdef-per-folder conflicts found in {scanned} ({byFolder.Count} folder(s) with an asmdef)."
                    : $"{conflicts.Count} folder(s) contain multiple .asmdef files — Unity will not compile any assembly in them. " +
                      "Give each .asmdef its own subfolder.";

                return ToolEnvelope.Ok(
                    msg,
                    result: new { scanned = roots, healthy, asmdefFolders = byFolder.Count, multiAsmdefFolders = conflicts.ToArray() },
                    verify: new { pass = healthy, checks = conflicts.ToArray() });
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
