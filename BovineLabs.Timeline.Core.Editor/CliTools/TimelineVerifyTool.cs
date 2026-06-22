using System;
using System.Collections.Generic;
using System.Reflection;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "timeline_verify",
        Group = "vex",
        Description =
            "Run a track skill's §7 verification in one read: fresh-load asset dump, binding read-back from a reloaded SubScene, scene-restore assertion, console scan, and optional 'expect' diffing. Read-only.")]
    public static class TimelineVerifyTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var checks = new List<object>();
                var allPass = true;

                void Add(string name, bool pass, string detail)
                {
                    checks.Add(new { name, pass, detail });
                    if (!pass) allPass = false;
                }

                var assetPath = p.OptString("asset");
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                    if (tl == null)
                    {
                        Add("asset-dump", false, $"no TimelineAsset at '{assetPath}'");
                    }
                    else
                    {
                        int tracks = 0, clips = 0;
                        foreach (var t in tl.GetOutputTracks())
                        {
                            tracks++;
                            foreach (var _ in t.GetClips()) clips++;
                        }

                        Add("asset-dump", true, $"{tracks} track(s), {clips} clip(s)");
                    }
                }

                Capture.DirectorPre pre = null;
                string parentPath = null;
                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;
                    parentPath = session.ParentPath;
                    var dirSel = p.OptString("director");
                    if (!string.IsNullOrEmpty(dirSel))
                    {
                        var go = session.Find(dirSel);
                        if (go == null)
                        {
                            Add("binding", false, $"no director '{dirSel}' in subscene");
                        }
                        else
                        {
                            var d = go.GetComponent<PlayableDirector>();
                            if (d == null)
                            {
                                Add("binding", false, $"'{dirSel}' has no PlayableDirector");
                            }
                            else
                            {
                                pre = Capture.Director(d);
                                var bs = pre.bindings.Count == 0
                                    ? "no bindings"
                                    : string.Join("; ",
                                        pre.bindings.ConvertAll(b =>
                                            $"{b.trackName}->{b.boundPath}({b.boundComponentType})"));
                                Add("binding", true, bs);
                            }
                        }
                    }
                }

                var active = EditorSceneManager.GetActiveScene();
                var sceneCount = EditorSceneManager.sceneCount;
                var restored = (parentPath == null || active.path == parentPath) && !active.isDirty;
                Add("scene-restore", restored,
                    $"active='{active.path}' (parent), sceneCount={sceneCount}, dirty={active.isDirty}");

                if (p.OptBool("console", true))
                {
                    ConsoleCounts(out var err, out var warn);
                    Add("console", true,
                        err < 0
                            ? "counts unavailable"
                            : $"errors={err}, warnings={warn} (no baseline diff; use unity-cli console)");
                }

                var expect = p.OptObject("expect");
                if (expect != null)
                {
                    if (expect["playableAsset"] != null)
                    {
                        var want = expect["playableAsset"].ToString();
                        var got = pre?.playableAsset;
                        Add("expect.playableAsset", got == want, $"want '{want}', got '{got ?? "null"}'");
                    }

                    if (expect["bindings"] is JArray wantBindings && pre != null)
                        foreach (var wb in wantBindings)
                        {
                            if (!(wb is JObject o)) continue;
                            var wt = o["track"]?.ToString();
                            var wo = o["object"]?.ToString();
                            var wc = o["component"]?.ToString();
                            var match = pre.bindings.Find(b => b.trackName == wt);
                            var ok = match != null
                                     && (wo == null || EndsWith(match.boundPath, wo))
                                     && (wc == null || match.boundComponentType == wc);
                            Add($"expect.binding[{wt}]", ok,
                                match == null ? "track not bound" : $"{match.boundPath}({match.boundComponentType})");
                        }
                }

                return ToolEnvelope.Ok(
                    $"Verify: {(allPass ? "PASS" : "FAIL")} ({checks.Count} check(s)).",
                    verify: new { pass = allPass, checks });
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        private static bool EndsWith(string full, string suffix)
        {
            return full != null && (full == suffix || full.EndsWith("/" + suffix));
        }

        private static void ConsoleCounts(out int err, out int warn)
        {
            err = -1;
            warn = -1;
            try
            {
                var t = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                var m = t?.GetMethod("GetCountsByType",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (m == null) return;
                object[] args = { 0, 0, 0 };
                m.Invoke(null, args);
                err = (int)args[0];
                warn = (int)args[1];
            }
            catch
            {
                err = -1;
                warn = -1;
            }
        }

        public class Parameters
        {
            [ToolParameter("The .playable to dump (optional if only checking the scene).")]
            public string Asset { get; set; }

            [ToolParameter("Subscene to reload for the binding read-back. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("Director whose binding table to verify.")]
            public string Director { get; set; }

            [ToolParameter(
                "JSON object of expected values to assert (e.g. { \"bindings\": [...], \"playableAsset\": \"...\" }).")]
            public object Expect { get; set; }

            [ToolParameter("Run a console error scan and report counts (default true).")]
            public bool Console { get; set; }
        }
    }
}