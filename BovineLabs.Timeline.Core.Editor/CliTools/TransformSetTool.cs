using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "transform_set",
        Group = "vex",
        Description = "Set local position / euler / scale on object(s) by hierarchy path (only the components provided). General transform primitive AND the per-object undo target for transform_orient. If 'subscene' is given, opens + saves that subscene; else operates on the open scene(s).")]
    public static class TransformSetTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. If set, the subscene is opened and saved. Omit = operate on the open scene(s).")]
            public string Subscene { get; set; }

            [ToolParameter("Hierarchy paths of the objects to transform.", Required = true)]
            public string[] Objects { get; set; }

            [ToolParameter("Local position [x,y,z] or {x,y,z}. Omit = leave unchanged.")]
            public object Position { get; set; }

            [ToolParameter("Local euler angles [x,y,z] or {x,y,z}. Omit = leave unchanged.")]
            public object Euler { get; set; }

            [ToolParameter("Local scale [x,y,z] or {x,y,z}. Omit = leave unchanged.")]
            public object Scale { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string subscene = p.OptString("subscene");
                bool useSubscene = p.Has("subscene");
                var objectsArr = p.RequireArray("objects");

                var paths = new List<string>();
                foreach (var tok in objectsArr)
                {
                    var path = tok?.ToString();
                    if (!string.IsNullOrEmpty(path)) paths.Add(path);
                }
                if (paths.Count == 0)
                    throw new ToolException("MISSING_PREREQUISITE", "'objects' must contain at least one path.");

                var pos = SceneObjectUtil.ReadVector3(@params, "position");
                var eul = SceneObjectUtil.ReadVector3(@params, "euler");
                var scl = SceneObjectUtil.ReadVector3(@params, "scale");

                // Reject non-finite components before any write. NaN/Infinity would be written verbatim
                // (ReadVector3 does not validate) and then poison the verify block (every comparison
                // against NaN is false, so 'ok' stays true), saving a corrupt transform as a false success.
                RequireFinite(pos, "position");
                RequireFinite(eul, "euler");
                RequireFinite(scl, "scale");

                if (useSubscene)
                {
                    using (var session = SubSceneSession.Open(subscene))
                    {
                        if (session.Error != null) return session.Error;
                        var result = Apply(paths, pos, eul, scl, path => session.Find(path), session.SubscenePath);
                        session.Save();
                        result.saved = !session.Subscene.isDirty;
                        return Envelope(result, session.SubscenePath);
                    }
                }
                else
                {
                    var result = Apply(paths, pos, eul, scl, ResolveInOpenScenes, null);
                    result.saved = true; // no subscene save bracket; not asserting scene cleanliness here
                    return Envelope(result, null);
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        private static void RequireFinite(Vector3? v, string field)
        {
            if (!v.HasValue) return;
            var c = v.Value;
            if (!float.IsFinite(c.x) || !float.IsFinite(c.y) || !float.IsFinite(c.z))
                throw new ToolException("BAD_VALUE", $"'{field}' components must be finite (no NaN/Infinity).");
        }

        internal sealed class ApplyResult
        {
            public List<string> applied = new List<string>();
            public List<string> notFound = new List<string>();
            public List<object> undo = new List<object>();
            public List<object> verifyChecks = new List<object>();
            public bool allVerified = true;
            public bool saved = true;
        }

        internal delegate GameObject Resolver(string path);

        internal static ApplyResult Apply(List<string> paths, Vector3? pos, Vector3? eul, Vector3? scl, Resolver resolve, string subscenePath)
        {
            var r = new ApplyResult();
            foreach (var path in paths)
            {
                var go = resolve(path);
                if (go == null) { r.notFound.Add(path); r.allVerified = false; continue; }
                var t = go.transform;

                // Record prior values for a per-object exact-restore undo entry.
                var prePos = t.localPosition;
                var preEul = t.localEulerAngles;
                var preScl = t.localScale;

                if (pos.HasValue) t.localPosition = pos.Value;
                if (eul.HasValue) t.localEulerAngles = eul.Value;
                if (scl.HasValue) t.localScale = scl.Value;

                r.applied.Add(path);

                // Per-object undo restores exactly the fields we touched (full snapshot is safe). The subscene
                // path MUST travel with the entry, or replay resolves against the closed subscene and silently
                // fails to restore (the object is unreachable once SubSceneSession.Dispose closes it).
                var undoParams = new Dictionary<string, object>
                {
                    ["objects"] = new[] { path },
                    ["position"] = new[] { prePos.x, prePos.y, prePos.z },
                    ["euler"] = new[] { preEul.x, preEul.y, preEul.z },
                    ["scale"] = new[] { preScl.x, preScl.y, preScl.z },
                };
                if (!string.IsNullOrEmpty(subscenePath))
                    undoParams["subscene"] = subscenePath;

                r.undo.Add(new { tool = "transform_set", @params = undoParams });

                // Verify by re-reading the transform values we set.
                bool ok = true;
                if (pos.HasValue && (t.localPosition - pos.Value).sqrMagnitude > 1e-6f) ok = false;
                if (scl.HasValue && (t.localScale - scl.Value).sqrMagnitude > 1e-6f) ok = false;
                // euler comparison via quaternion to avoid 360/wraparound mismatch
                if (eul.HasValue && Quaternion.Angle(t.localRotation, Quaternion.Euler(eul.Value)) > 0.1f) ok = false;
                if (!ok) r.allVerified = false;
                r.verifyChecks.Add(new { name = $"set:{path}", pass = ok, detail = ok ? "values match" : "mismatch after set" });
            }
            return r;
        }

        internal static GameObject ResolveInOpenScenes(string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath)) return null;
            var parts = hierarchyPath.Split('/');
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;
                    if (parts.Length == 1) return root;
                    var rest = string.Join("/", parts, 1, parts.Length - 1);
                    var child = root.transform.Find(rest);
                    if (child != null) return child.gameObject;
                }
            }
            return null;
        }

        private static object Envelope(ApplyResult r, string subscenePath)
        {
            bool pass = r.allVerified && r.notFound.Count == 0 && r.saved;
            var checks = new List<object>(r.verifyChecks);
            foreach (var nf in r.notFound)
                checks.Add(new { name = $"set:{nf}", pass = false, detail = "object not found" });
            checks.Add(new { name = "saved", pass = r.saved, detail = r.saved ? "ok" : "scene still dirty" });

            return ToolEnvelope.Ok(
                $"Set transform on {r.applied.Count}/{r.applied.Count + r.notFound.Count} object(s).",
                result: new { subscene = subscenePath, applied = r.applied.ToArray(), notFound = r.notFound.ToArray() },
                undo: r.undo.ToArray(),
                verify: new { pass, checks });
        }
    }
}
