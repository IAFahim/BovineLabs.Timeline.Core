using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine.Playables;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "track_author",
        Group = "vex",
        Description = "Author a whole track end-to-end in one call: timeline_create -> clip_add(xN) -> director_bind -> exposed_ref_wire(opt) -> timeline_verify, returning a memory-card result with the combined reverse-ordered undo. Rolls back on partial failure.")]
    public static class TrackAuthorTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("Director to wire.", Required = true)]
            public string Director { get; set; }

            [ToolParameter("Path for the new .playable.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Track type (name).", Required = true)]
            public string TrackType { get; set; }

            [ToolParameter("Track name (default = track type).")]
            public string TrackName { get; set; }

            [ToolParameter("JSON object of track-level fields.")]
            public object TrackFields { get; set; }

            [ToolParameter("Array of clip_add specs: { clip_type, start, duration, blend_in, blend_out, display_name, fields }.")]
            public object[] Clips { get; set; }

            [ToolParameter("The director_bind binding for this track: { object, component }.", Required = true)]
            public object Bind { get; set; }

            [ToolParameter("Array of exposed_ref_wire specs: { clip, field, target }.")]
            public object[] ExposedRefs { get; set; }

            [ToolParameter("Run timeline_verify at the end (default true).")]
            public bool Verify { get; set; }

            [ToolParameter("Allow replacing an existing asset at the path (default false). When false, an existing asset fails the call before anything is mutated, so a hand-built timeline is never clobbered.")]
            public bool Overwrite { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string subscene = p.OptString("subscene");
                string director = p.RequireString("director");
                string asset = p.RequireString("asset");
                string trackType = p.RequireString("track_type");
                string trackName = p.OptString("track_name");
                var trackFields = p.OptObject("track_fields");
                var clips = p.OptArray("clips");
                var bind = p.RequireObject("bind");
                var exposedRefs = p.OptArray("exposed_refs");
                bool verify = p.OptBool("verify", true);
                bool overwrite = p.OptBool("overwrite", false);

                // Track name must agree with what timeline_create will produce (= resolved type simple name).
                string realTrackName = string.IsNullOrEmpty(trackName)
                    ? TimelineReflect.ResolveType(trackType).Name
                    : trackName;

                // Capture the director PRE up front (its own short session).
                Capture.DirectorPre dirPre;
                using (var s = SubSceneSession.Open(subscene))
                {
                    if (s.Error != null) return s.Error;
                    subscene = s.SubscenePath;
                    var go = s.Find(director);
                    if (go == null) return ToolEnvelope.Error("NOT_FOUND", $"No object '{director}' in {s.SubscenePath}.");
                    var d = go.GetComponent<PlayableDirector>();
                    if (d == null) return ToolEnvelope.Error("NOT_FOUND", $"'{director}' has no PlayableDirector.");
                    dirPre = Capture.Director(d);
                }

                var undo = new List<object>(); // forward order; reversed at the end

                // 1. Create the asset + track.
                var createParams = new JObject
                {
                    ["asset"] = asset,
                    ["track_type"] = trackType,
                    ["track_name"] = realTrackName,
                    ["overwrite"] = overwrite,
                };
                if (trackFields != null) createParams["track_fields"] = trackFields;
                var rCreate = TimelineCreateTool.HandleCommand(createParams);
                if (rCreate is ErrorResponse) return Fail("timeline_create", rCreate, undo);
                CollectUndo(rCreate, undo);

                // 2. Clips.
                if (clips != null)
                    foreach (var ct in clips)
                    {
                        if (!(ct is JObject co)) continue;
                        var cp = new JObject { ["asset"] = asset, ["track"] = realTrackName, ["clip_type"] = co["clip_type"] };
                        foreach (var key in new[] { "start", "duration", "display_name", "blend_in", "blend_out", "fields" })
                            if (co[key] != null) cp[key] = co[key];
                        var rClip = ClipAddTool.HandleCommand(cp);
                        if (rClip is ErrorResponse) return Fail("clip_add", rClip, undo);
                        CollectUndo(rClip, undo);
                    }

                // 3. Bind the director.
                var bindParams = new JObject
                {
                    ["subscene"] = subscene,
                    ["director"] = director,
                    ["asset"] = asset,
                    ["bindings"] = new JArray(new JObject
                    {
                        ["track"] = realTrackName,
                        ["object"] = bind["object"],
                        ["component"] = bind["component"],
                    }),
                };
                var rBind = DirectorBindTool.HandleCommand(bindParams);
                if (rBind is ErrorResponse) return Fail("director_bind", rBind, undo);
                CollectUndo(rBind, undo);

                // 4. Exposed references.
                if (exposedRefs != null)
                    foreach (var et in exposedRefs)
                    {
                        if (!(et is JObject eo)) continue;
                        var ep = new JObject
                        {
                            ["subscene"] = subscene,
                            ["director"] = director,
                            ["asset"] = asset,
                            ["track"] = realTrackName,
                            ["clip"] = eo["clip"],
                            ["field"] = eo["field"],
                            ["target"] = eo["target"],
                        };
                        var rExp = ExposedRefWireTool.HandleCommand(ep);
                        if (rExp is ErrorResponse) return Fail("exposed_ref_wire", rExp, undo);
                        CollectUndo(rExp, undo);
                    }

                // 5. Verify.
                object verifyData = null;
                if (verify)
                {
                    var vp = new JObject { ["asset"] = asset, ["subscene"] = subscene, ["director"] = director };
                    var rVerify = TimelineVerifyTool.HandleCommand(vp);
                    verifyData = ExtractSection(rVerify, "verify");
                }

                undo.Reverse(); // replay top-to-bottom inverts in reverse action order

                return ToolEnvelope.Ok(
                    $"Authored {realTrackName} on {director}{(verify ? " (verify attached)" : "")}.",
                    result: new { created = new { assetPath = asset, trackName = realTrackName }, director, verify = verifyData },
                    pre: new { director = dirPre },
                    undo: undo.ToArray());
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        private static void CollectUndo(object resp, List<object> undo)
        {
            if (resp is SuccessResponse sr && sr.data is IDictionary<string, object> d
                && d.TryGetValue("undo", out var u) && u is object[] arr)
                undo.AddRange(arr);
        }

        private static object ExtractSection(object resp, string key)
        {
            if (resp is SuccessResponse sr && sr.data is IDictionary<string, object> d
                && d.TryGetValue(key, out var v))
                return v;
            return null;
        }

        // Roll back whatever already succeeded, then return the error — never leave a half-authored track.
        private static object Fail(string step, object resp, List<object> undo)
        {
            bool rollbackComplete = true;
            if (undo.Count > 0)
            {
                var rev = new List<object>(undo);
                rev.Reverse();
                var undoResp = ToolUndoTool.HandleCommand(new JObject { ["undo"] = JArray.FromObject(rev) });
                rollbackComplete = !(undoResp is ErrorResponse);
            }

            string msg = resp is ErrorResponse er ? er.message : "failed";
            return ToolEnvelope.Error("RECIPE_FAILED",
                $"track_author rolled back at {step}{(rollbackComplete ? "" : " (ROLLBACK INCOMPLETE — manual cleanup may be needed)")}: {msg}",
                new { step, rollbackComplete });
        }
    }
}
