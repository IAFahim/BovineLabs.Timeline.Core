using System;
using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "mechanic_doctor",
        Group = "vex",
        Description =
            "Check (and optionally --fix) a mechanic's setup: resolves the SAME requirement manifest as mechanic_author and runs every idempotent ensure_* in dry_run — reporting per requirement already-ok / would-fix — without adding the core clip. With fix=true it repairs the unmet ones and returns the combined undo. The 'fix if X missing' layer.")]
    public static class MechanicDoctorTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var fix = p.OptBool("fix", false);
                var ctx = MechanicResolver.BuildContext(@params);
                var reqs = MechanicResolver.BuildRequirements(ctx);

                var findings = new List<object>();
                var journal = new List<object>();
                int ok = 0, unmet = 0, fixedCount = 0, failed = 0;

                foreach (var req in reqs)
                {
                    if (!req.Idempotent) continue;

                    var handler = ToolDiscovery.FindHandler(req.Tool);
                    if (handler == null)
                    {
                        failed++;
                        findings.Add(new { req.Label, tool = req.Tool, satisfied = false, action = "no-tool" });
                        continue;
                    }

                    var args = (JObject)req.Params.DeepClone();
                    args["dry_run"] = !fix;

                    object resp;
                    try
                    {
                        resp = handler.Invoke(null, new object[] { args });
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        findings.Add(new
                            { req.Label, tool = req.Tool, satisfied = false, action = "error", error = ex.Message });
                        continue;
                    }

                    if (Responses.IsError(resp))
                    {
                        failed++;
                        findings.Add(new
                        {
                            req.Label, tool = req.Tool, satisfied = false, action = "error",
                            error = Responses.Message(resp)
                        });
                        continue;
                    }

                    var resultObj = Responses.Section(resp, "result");
                    var jr = resultObj != null ? JObject.FromObject(resultObj) : null;
                    var satisfied = jr?["satisfied"]?.Value<bool>() ?? false;
                    var action = jr?["action"]?.ToString() ?? "unknown";

                    if (action == EnsureResult.AlreadyOk) ok++;
                    else if (action == EnsureResult.WouldFix) unmet++;
                    else if (action == EnsureResult.Fixed) fixedCount++;

                    var u = Responses.Undo(resp);
                    if (u != null) journal.AddRange(u);

                    findings.Add(new
                        { req.Label, tool = req.Tool, satisfied, action, message = Responses.Message(resp) });
                }

                journal.Reverse();
                var verb = fix ? $"{fixedCount} fixed, {ok} already-ok" : $"{unmet} would-fix, {ok} ok";
                if (failed > 0) verb += $", {failed} failed";
                return ToolEnvelope.Ok(
                    $"mechanic_doctor ({(fix ? "fix" : "check")}): {verb}.",
                    new { mode = fix ? "fix" : "check", ok, unmet, @fixed = fixedCount, failed, findings },
                    undo: journal.Count > 0 ? journal.ToArray() : null);
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("Mechanic kind preset (e.g. trap) — fills track_type/clip_type when omitted.")]
            public string Kind { get; set; }

            [ToolParameter("Director hierarchy path/name.", Required = true)]
            public string Director { get; set; }

            [ToolParameter("The .playable path.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track type (overrides the kind preset).")]
            public string TrackType { get; set; }

            [ToolParameter("Clip type (overrides the kind preset).")]
            public string ClipType { get; set; }

            [ToolParameter("The trigger SOURCE / bind object.")]
            public string Source { get; set; }

            [ToolParameter("Payload prefab path.")]
            public string Prefab { get; set; }

            [ToolParameter("ObjectDefinition asset path.")]
            public string Objdef { get; set; }

            [ToolParameter("Apply repairs instead of only reporting (default false).")]
            public bool Fix { get; set; }
        }
    }
}