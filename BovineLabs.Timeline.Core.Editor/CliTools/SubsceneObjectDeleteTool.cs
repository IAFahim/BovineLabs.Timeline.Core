using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_object_delete",
        Group = "vex",
        Description = "Destroy GameObject(s) by hierarchy path inside a SubScene's editing scene, then save. The replayable undo target for subscene_object_create. Auto-detects the editable subscene when omitted.")]
    public static class SubsceneObjectDeleteTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("Hierarchy paths inside the subscene to destroy.", Required = true)]
            public string[] Objects { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string subscene = p.OptString("subscene");
                var objectsArr = p.RequireArray("objects");

                var paths = new List<string>();
                foreach (var tok in objectsArr)
                {
                    var path = tok?.ToString();
                    if (!string.IsNullOrEmpty(path)) paths.Add(path);
                }
                if (paths.Count == 0)
                    throw new ToolException("MISSING_PREREQUISITE", "'objects' must contain at least one path.");

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    var deleted = new List<string>();
                    var notFound = new List<string>();
                    foreach (var path in paths)
                    {
                        var go = session.Find(path);
                        if (go == null) { notFound.Add(path); continue; }
                        Object.DestroyImmediate(go);
                        deleted.Add(path);
                    }

                    session.Save();

                    // Verify by re-reading: each requested path must no longer resolve.
                    var checks = new List<object>();
                    bool allGone = true;
                    foreach (var path in paths)
                    {
                        bool gone = session.Find(path) == null;
                        if (!gone) allGone = false;
                        checks.Add(new { name = $"removed:{path}", pass = gone, detail = gone ? "not found" : "still present" });
                    }

                    bool saved = !session.Subscene.isDirty;
                    checks.Add(new { name = "saved", pass = saved, detail = saved ? "scene clean" : "scene still dirty" });
                    bool pass = allGone && saved;

                    string sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"Deleted {deleted.Count}/{paths.Count} object(s) from '{sceneName}'.",
                        result: new { subscene = session.SubscenePath, deleted = deleted.ToArray(), notFound = notFound.ToArray() },
                        verify: new { pass, checks });
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
