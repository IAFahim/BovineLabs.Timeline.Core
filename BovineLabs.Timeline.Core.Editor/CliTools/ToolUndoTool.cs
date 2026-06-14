using System;
using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "tool_undo",
        Group = "vex",
        Description = "Replay a data.undo journal in one call: dispatch each { tool, params } entry IN ORDER. Closes the undo loop so a mutator's undo is replayable in one step.")]
    public static class ToolUndoTool
    {
        public class Parameters
        {
            [ToolParameter("The data.undo journal: an ordered array of { tool, params } invocations to replay.", Required = true)]
            public object[] Undo { get; set; }
        }

        // Direct in-process handlers — NOT CommandRouter.Dispatch (the dispatch semaphore is
        // non-reentrant; re-dispatching from inside a handler would deadlock the connector).
        private static readonly Dictionary<string, Func<JObject, object>> Handlers =
            new Dictionary<string, Func<JObject, object>>
            {
                { "asset_delete", AssetDeleteTool.HandleCommand },
                { "clip_remove", ClipRemoveTool.HandleCommand },
                { "director_bind", DirectorBindTool.HandleCommand },
                { "exposed_ref_wire", ExposedRefWireTool.HandleCommand },
                { "timeline_create", TimelineCreateTool.HandleCommand },
                { "clip_add", ClipAddTool.HandleCommand },
                { "subscene_object_create", SubsceneObjectCreateTool.HandleCommand },
                { "subscene_object_delete", SubsceneObjectDeleteTool.HandleCommand },
                { "subscene_object_spawn_pattern", SubsceneObjectSpawnPatternTool.HandleCommand },
                { "transform_set", TransformSetTool.HandleCommand },
                { "transform_orient", TransformOrientTool.HandleCommand },
            };

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var arr = p.RequireArray("undo");
                var steps = new List<object>();
                int ok = 0, fail = 0;

                foreach (var entry in arr)
                {
                    if (!(entry is JObject eo))
                    {
                        fail++;
                        steps.Add(new { ok = false, error = "undo entry is not an object." });
                        continue;
                    }

                    string tool = eo["tool"]?.ToString();
                    var prm = eo["params"] as JObject ?? new JObject();

                    if (string.IsNullOrEmpty(tool) || !Handlers.TryGetValue(tool, out var handler))
                    {
                        fail++;
                        steps.Add(new { tool, ok = false, error = $"no replayable tool '{tool}'." });
                        continue;
                    }

                    object response;
                    try { response = handler(prm); }
                    catch (Exception ex)
                    {
                        fail++;
                        steps.Add(new { tool, ok = false, error = ex.Message });
                        continue;
                    }

                    bool isErr = response is ErrorResponse;
                    if (isErr) fail++; else ok++;
                    steps.Add(new { tool, ok = !isErr, response });
                }

                return ToolEnvelope.Ok(
                    $"Replayed {arr.Count} undo step(s): {ok} ok, {fail} failed.",
                    result: new { ok, fail, steps });
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }
    }
}
