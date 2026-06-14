using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "transform_orient",
        Group = "vex",
        Description = "Rotate object(s) to face a target object (by hierarchy path) or a world point. Sets rotation = LookRotation(dir, up); optional flatten_y zeroes the look direction's Y (face horizontally). Verifies by re-reading each forward vector (dot > 0.9). If 'subscene' is given, opens + saves it. The 'face the player' primitive.")]
    public static class TransformOrientTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. If set, the subscene is opened and saved. Omit = operate on the open scene(s).")]
            public string Subscene { get; set; }

            [ToolParameter("Hierarchy paths of the objects to orient.", Required = true)]
            public string[] Objects { get; set; }

            [ToolParameter("Hierarchy path of the object to face. Mutually exclusive with point.")]
            public string Target { get; set; }

            [ToolParameter("World point {x,y,z} or [x,y,z] to face. Mutually exclusive with target.")]
            public object Point { get; set; }

            [ToolParameter("Up vector {x,y,z} or [x,y,z] for LookRotation (default 0,1,0).")]
            public object Up { get; set; }

            [ToolParameter("Zero the Y of the look direction so objects only yaw (face horizontally). Default false.")]
            public bool FlattenY { get; set; }
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
                string target = p.OptString("target");
                var objectsArr = p.RequireArray("objects");

                var paths = new List<string>();
                foreach (var tok in objectsArr)
                {
                    var path = tok?.ToString();
                    if (!string.IsNullOrEmpty(path)) paths.Add(path);
                }
                if (paths.Count == 0)
                    throw new ToolException("MISSING_PREREQUISITE", "'objects' must contain at least one path.");

                var pointVec = SceneObjectUtil.ReadVector3(@params, "point");
                if (string.IsNullOrEmpty(target) && !pointVec.HasValue)
                    throw new ToolException("MISSING_PREREQUISITE", "Provide either 'target' (path) or 'point' (world coords).");
                if (!string.IsNullOrEmpty(target) && pointVec.HasValue)
                    throw new ToolException("BAD_VALUE", "Provide only one of 'target' or 'point'.");

                var up = SceneObjectUtil.ReadVector3(@params, "up") ?? Vector3.up;
                bool flattenY = p.OptBool("flatten_y", false);

                if (useSubscene)
                {
                    using (var session = SubSceneSession.Open(subscene))
                    {
                        if (session.Error != null) return session.Error;
                        // Objects resolve inside the subscene; the target may live in the subscene OR the
                        // additively-loaded parent scene (e.g. a 'Player'), so fall back to all open scenes.
                        TransformSetTool.Resolver objResolve = path => session.Find(path);
                        TransformSetTool.Resolver targetResolve = path => session.Find(path) ?? TransformSetTool.ResolveInOpenScenes(path);
                        var result = Orient(paths, target, pointVec, up, flattenY, objResolve, targetResolve);
                        if (result.error != null) return result.error;
                        session.Save();
                        result.saved = !session.Subscene.isDirty;
                        return Envelope(result, session.SubscenePath);
                    }
                }
                else
                {
                    var result = Orient(paths, target, pointVec, up, flattenY, TransformSetTool.ResolveInOpenScenes, TransformSetTool.ResolveInOpenScenes);
                    if (result.error != null) return result.error;
                    result.saved = true;
                    return Envelope(result, null);
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        private sealed class OrientResult
        {
            public List<string> applied = new List<string>();
            public List<string> notFound = new List<string>();
            public List<object> undo = new List<object>();
            public List<object> verifyChecks = new List<object>();
            public Dictionary<string, object> preEuler = new Dictionary<string, object>();
            public bool allVerified = true;
            public bool saved = true;
            public object error;
        }

        private static OrientResult Orient(List<string> paths, string target, Vector3? point, Vector3 up, bool flattenY, TransformSetTool.Resolver resolve, TransformSetTool.Resolver targetResolve)
        {
            var r = new OrientResult();

            Vector3 targetPos;
            if (point.HasValue)
            {
                targetPos = point.Value;
            }
            else
            {
                var targetGo = targetResolve(target);
                if (targetGo == null)
                {
                    r.error = ToolEnvelope.Error("NOT_FOUND", $"Target object '{target}' not found.");
                    return r;
                }
                targetPos = targetGo.transform.position;
            }

            foreach (var path in paths)
            {
                var go = resolve(path);
                if (go == null) { r.notFound.Add(path); r.allVerified = false; continue; }
                var t = go.transform;

                var preEul = t.localEulerAngles;
                r.preEuler[path] = new[] { preEul.x, preEul.y, preEul.z };

                var dir = targetPos - t.position;
                if (flattenY) dir.y = 0f;

                bool degenerate = dir.sqrMagnitude < 1e-8f;
                if (!degenerate)
                    t.rotation = Quaternion.LookRotation(dir.normalized, up);

                r.applied.Add(path);

                r.undo.Add(new
                {
                    tool = "transform_set",
                    @params = new
                    {
                        objects = new[] { path },
                        euler = new[] { preEul.x, preEul.y, preEul.z },
                    },
                });

                // Verify by re-reading the forward vector vs direction-to-target.
                var checkDir = targetPos - t.position;
                if (flattenY) checkDir.y = 0f;
                bool pass;
                string detail;
                if (checkDir.sqrMagnitude < 1e-8f)
                {
                    pass = false; detail = "degenerate: object and target coincide";
                }
                else
                {
                    float dot = Vector3.Dot(t.forward, checkDir.normalized);
                    pass = dot > 0.9f;
                    detail = $"dot={dot:F3}";
                }
                if (!pass) r.allVerified = false;
                r.verifyChecks.Add(new { name = $"facing:{path}", pass, detail });
            }
            return r;
        }

        private static object Envelope(OrientResult r, string subscenePath)
        {
            bool pass = r.allVerified && r.notFound.Count == 0 && r.saved;
            var checks = new List<object>(r.verifyChecks);
            foreach (var nf in r.notFound)
                checks.Add(new { name = $"facing:{nf}", pass = false, detail = "object not found" });
            checks.Add(new { name = "saved", pass = r.saved, detail = r.saved ? "ok" : "scene still dirty" });

            return ToolEnvelope.Ok(
                $"Oriented {r.applied.Count}/{r.applied.Count + r.notFound.Count} object(s).",
                result: new { subscene = subscenePath, oriented = r.applied.ToArray(), notFound = r.notFound.ToArray() },
                pre: r.preEuler,
                undo: r.undo.ToArray(),
                verify: new { pass, checks });
        }
    }
}
