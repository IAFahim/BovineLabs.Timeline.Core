using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;
using UnityEngine.Playables;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "director_inspect",
        Group = "vex",
        Description = "Per director in the SubScene: path, playableAsset, binding table, exposed refs, and the TimelineBeginAuthoring activation marker (§3.3 + §3.5 in one read).")]
    public static class DirectorInspectTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("One director by hierarchy path/name. Omit = all directors in the subscene.")]
            public string Director { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                string subscene = p.OptString("subscene");
                string directorSel = p.OptString("director");

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    var directors = new List<Capture.DirectorPre>();
                    var all = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var d in all)
                    {
                        if (d.gameObject.scene != session.Subscene) continue;
                        if (!string.IsNullOrEmpty(directorSel) &&
                            Hierarchy.PathOf(d.gameObject) != directorSel && d.gameObject.name != directorSel)
                            continue;
                        directors.Add(Capture.Director(d));
                    }

                    if (!string.IsNullOrEmpty(directorSel) && directors.Count == 0)
                        return ToolEnvelope.Error("NOT_FOUND", $"No director '{directorSel}' in {session.SubscenePath}.");

                    string sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"{directors.Count} director(s) in '{sceneName}'.",
                        result: new { subscene = session.SubscenePath, directors });
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
