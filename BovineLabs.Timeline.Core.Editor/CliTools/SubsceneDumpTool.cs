using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_dump",
        Group = "vex",
        Description =
            "Field-level dump of a SubScene: every root -> object -> component -> serialized field values. Read-only; auto-detects the subscene and restores the parent scene.")]
    public static class SubsceneDumpTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var subscene = p.OptString("subscene");
                var objectPath = p.OptString("object");
                var compFilter = p.OptString("component");
                var fields = p.OptBool("fields", true);
                var maxDepth = p.OptInt("depth", -1);

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    var roots = new List<object>();
                    var compCount = 0;

                    if (!string.IsNullOrEmpty(objectPath))
                    {
                        var go = session.Find(objectPath);
                        if (go == null)
                            return ToolEnvelope.Error("NOT_FOUND",
                                $"No object '{objectPath}' in {session.SubscenePath}.");
                        roots.Add(DumpGo(go.transform, 0, maxDepth, fields, compFilter, ref compCount));
                    }
                    else
                    {
                        foreach (var root in session.Subscene.GetRootGameObjects())
                            roots.Add(DumpGo(root.transform, 0, maxDepth, fields, compFilter, ref compCount));
                    }

                    var sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"Dumped '{sceneName}' ({roots.Count} root(s), {compCount} component(s)).",
                        new { subscene = session.SubscenePath, roots });
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        private static object DumpGo(Transform t, int depth, int maxDepth, bool fields, string compFilter,
            ref int compCount)
        {
            var go = t.gameObject;
            var comps = new List<object>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var tn = c.GetType().Name;
                if (!string.IsNullOrEmpty(compFilter) && tn != compFilter) continue;
                compCount++;
                if (fields)
                {
                    JObject f;
                    try
                    {
                        f = TimelineReflect.ReadSerializedFields(c);
                    }
                    catch
                    {
                        f = null;
                    }

                    comps.Add(new { type = tn, fields = f });
                }
                else
                {
                    comps.Add(new { type = tn });
                }
            }

            List<object> children = null;
            var descend = maxDepth < 0 || depth < maxDepth;
            if (descend && t.childCount > 0)
            {
                children = new List<object>();
                for (var i = 0; i < t.childCount; i++)
                    children.Add(DumpGo(t.GetChild(i), depth + 1, maxDepth, fields, compFilter, ref compCount));
            }

            return new
            {
                path = Hierarchy.PathOf(go),
                go.name,
                active = go.activeInHierarchy,
                components = comps,
                t.childCount,
                children
            };
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("Restrict to one hierarchy path (and its descendants). Omit = whole subscene.")]
            public string Object { get; set; }

            [ToolParameter("Restrict to one component type (simple name) across objects.")]
            public string Component { get; set; }

            [ToolParameter("Include serialized field values (default true; false = names only, like scene_structure).")]
            public bool Fields { get; set; }

            [ToolParameter("Max recursion depth (default -1 = unlimited).")]
            public int Depth { get; set; }
        }
    }
}