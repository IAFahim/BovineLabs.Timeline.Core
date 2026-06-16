using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "mechanic_author",
        Group = "vex",
        Description = "Author a whole mechanic from a few keys: resolve its requirement manifest (reflectable from the clip/track types + the package's hand-declared traps), run each idempotent ensure_* in phase order, add the core clip, verify, and roll back the whole journal on any failure. The generic L2 engine — pass kind=trap (or explicit clip_type/track_type) plus source/prefab/objdef.")]
    public static class MechanicAuthorTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("Mechanic kind preset (e.g. trap) — fills track_type/clip_type when omitted.")]
            public string Kind { get; set; }

            [ToolParameter("Director hierarchy path/name that runs the timeline.", Required = true)]
            public string Director { get; set; }

            [ToolParameter("The .playable path to author.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track type (overrides the kind preset).")]
            public string TrackType { get; set; }

            [ToolParameter("Track name (default = track type).")]
            public string TrackName { get; set; }

            [ToolParameter("Clip type (overrides the kind preset).")]
            public string ClipType { get; set; }

            [ToolParameter("Clip display name (default = clip type).")]
            public string ClipName { get; set; }

            [ToolParameter("The trigger SOURCE object (also the default bind target).")]
            public string Source { get; set; }

            [ToolParameter("Payload prefab path.")]
            public string Prefab { get; set; }

            [ToolParameter("ObjectDefinition asset path for the payload.")]
            public string Objdef { get; set; }

            [ToolParameter("JSON object of clip fields (e.g. {\"triggerState\":1}).")]
            public object ClipFields { get; set; }

            [ToolParameter("JSON object of ExposedReference<T> field -> SubScene object path.")]
            public object Exposed { get; set; }

            [ToolParameter("Run timeline_verify at the end (default true).")]
            public bool Verify { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string kind = p.OptString("kind");
                var ctx = MechanicResolver.BuildContext(@params);
                var reqs = MechanicResolver.BuildRequirements(ctx);

                bool verify = p.OptBool("verify", true);
                var journal = new List<object>(); // forward order; reversed before replay
                var steps = new List<object>();

                foreach (var req in reqs)
                {
                    var handler = ToolDiscovery.FindHandler(req.Tool);
                    if (handler == null)
                        return Fail(req.Label, ToolEnvelope.Error("NOT_FOUND", $"No tool '{req.Tool}' for requirement '{req.Label}'."), journal);

                    object resp;
                    try { resp = handler.Invoke(null, new object[] { req.Params }); }
                    catch (System.Exception ex)
                    {
                        return Fail(req.Label, ToolEnvelope.Error("RECIPE_FAILED", $"{req.Tool} threw: {ex.Message}"), journal);
                    }

                    if (Responses.IsError(resp))
                        return Fail(req.Label, resp, journal);

                    var u = Responses.Undo(resp);
                    if (u != null) journal.AddRange(u);
                    steps.Add(new { phase = req.Phase.ToString(), tool = req.Tool, label = req.Label, message = Responses.Message(resp) });
                }

                object verifyData = null;
                if (verify)
                {
                    var vp = new JObject { ["asset"] = ctx.Asset, ["subscene"] = ctx.Subscene, ["director"] = ctx.Director };
                    verifyData = Responses.Section(TimelineVerifyTool.HandleCommand(vp), "verify");
                }

                journal.Reverse();
                return ToolEnvelope.Ok(
                    $"Authored {kind ?? ctx.ClipType.Name} on {ctx.Director} ({steps.Count} requirement(s)).",
                    result: new { mechanic = kind ?? ctx.ClipType.Name, director = ctx.Director, asset = ctx.Asset, steps },
                    undo: journal.ToArray(),
                    verify: verifyData);
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        // Roll back the journal collected so far, then return the originating error — never leave a half-built mechanic.
        private static object Fail(string label, object resp, List<object> journal)
        {
            bool rollbackComplete = true;
            if (journal.Count > 0)
            {
                var rev = new List<object>(journal);
                rev.Reverse();
                var undoResp = ToolUndoTool.HandleCommand(new JObject { ["undo"] = JArray.FromObject(rev) });
                rollbackComplete = !(undoResp is ErrorResponse);
            }

            string msg = Responses.Message(resp) ?? "failed";
            return ToolEnvelope.Error("RECIPE_FAILED",
                $"mechanic_author rolled back at '{label}'{(rollbackComplete ? "" : " (ROLLBACK INCOMPLETE — manual cleanup may be needed)")}: {msg}",
                new { label, rollbackComplete });
        }
    }
}
