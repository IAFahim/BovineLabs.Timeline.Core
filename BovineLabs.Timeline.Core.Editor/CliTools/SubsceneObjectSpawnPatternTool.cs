using System;
using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_object_spawn_pattern",
        Group = "vex",
        Description =
            "Spawn N primitives/prefabs into the open SubScene in a grid/circle/line/stack pattern, parented under one container; self-verifies and returns an undo journal.")]
    public static class SubsceneObjectSpawnPatternTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var subscene = p.OptString("subscene");
                var primitive = p.OptString("primitive");
                var prefab = p.OptString("prefab");
                var name = p.OptString("name");
                var parentPath = p.OptString("parent");
                var pattern = p.RequireString("pattern").ToLowerInvariant();
                var count = p.OptInt("count", 0);
                var spacing = p.OptFloat("spacing", 1f);
                var radius = p.OptFloat("radius", spacing);

                ValidateSpawnArgs(count, primitive, prefab, pattern);
                var primType = ResolvePrimitive(primitive);
                var prefabAsset = ResolvePrefab(prefab);

                var origin = SceneObjectUtil.ReadVector3(@params, "origin") ?? Vector3.zero;
                var scale = ReadScale(@params, "scale");

                var baseName = !string.IsNullOrEmpty(name) ? name
                    : !string.IsNullOrEmpty(primitive) ? primitive : Path.GetFileNameWithoutExtension(prefab);

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    Transform container;
                    bool containerExisted;
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        var existing = session.Find(parentPath);
                        if (existing == null)
                            return ToolEnvelope.Error("NOT_FOUND",
                                $"No parent '{parentPath}' in {session.SubscenePath}.");
                        container = existing.transform;
                        containerExisted = true;
                    }
                    else
                    {
                        var containerGo = new GameObject(baseName);
                        EditorSceneManager.MoveGameObjectToScene(containerGo, session.Subscene);
                        containerGo.transform.localPosition = origin;
                        container = containerGo.transform;
                        containerExisted = false;
                    }

                    var createdGos = new List<GameObject>();
                    for (var i = 0; i < count; i++)
                    {
                        GameObject go;
                        if (prefabAsset != null)
                            go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                        else
                            go = GameObject.CreatePrimitive(primType);

                        if (go == null)
                        {
                            foreach (var created in createdGos)
                                Object.DestroyImmediate(created);
                            if (!containerExisted)
                                Object.DestroyImmediate(container.gameObject);
                            return ToolEnvelope.Error("BAD_VALUE", $"Could not instantiate prefab '{prefab}'.");
                        }

                        go.name = $"{baseName}_{i}";

                        EditorSceneManager.MoveGameObjectToScene(go, session.Subscene);
                        go.transform.SetParent(container, false);

                        var offset = PatternOffset(pattern, i, count, spacing, radius);
                        go.transform.localPosition = containerExisted ? origin + offset : offset;

                        if (scale.HasValue)
                            go.transform.localScale = scale.Value;

                        createdGos.Add(go);
                    }

                    session.Save();

                    var containerPath = Hierarchy.PathOf(container.gameObject);
                    var createdPaths = new List<string>();
                    var createdResult = new List<object>();
                    bool allExist = true, allInScene = true;
                    foreach (var go in createdGos)
                    {
                        var path = Hierarchy.PathOf(go);
                        createdPaths.Add(path);
                        createdResult.Add(new { path });

                        var found = session.Find(path);
                        if (found == null)
                        {
                            allExist = false;
                            continue;
                        }

                        if (found.scene != session.Subscene) allInScene = false;
                    }

                    var countMatches = createdPaths.Count == count;
                    var saved = !session.Subscene.isDirty;

                    var checks = new List<object>
                    {
                        new
                        {
                            name = "count-matches", pass = countMatches,
                            detail = $"created {createdPaths.Count} of {count}"
                        },
                        new { name = "object-exists", pass = allExist, detail = $"{createdPaths.Count} re-resolved" },
                        new { name = "all-in-subscene", pass = allInScene, detail = session.SubscenePath },
                        new { name = "saved", pass = saved, detail = saved ? "scene clean" : "scene still dirty" }
                    };
                    var pass = countMatches && allExist && allInScene && saved;

                    var undoTargets = containerExisted ? createdPaths.ToArray() : new[] { containerPath };
                    var undo = new object[]
                    {
                        new
                        {
                            tool = "subscene_object_delete",
                            @params = new { subscene = session.SubscenePath, objects = undoTargets }
                        }
                    };

                    var sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"Spawned {createdPaths.Count} '{pattern}' object(s) under '{containerPath}' in '{sceneName}'.",
                        new
                        {
                            container = new { path = containerPath, container.name }, created = createdResult,
                            count = createdPaths.Count
                        },
                        new { subscene = session.SubscenePath, containerExisted },
                        undo,
                        new { pass, checks });
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        private static void ValidateSpawnArgs(int count, string primitive, string prefab, string pattern)
        {
            if (count < 1)
                throw new ToolException("BAD_VALUE", "'count' must be >= 1.");
            if (string.IsNullOrEmpty(primitive) && string.IsNullOrEmpty(prefab))
                throw new ToolException("MISSING_PREREQUISITE", "Provide either 'primitive' or 'prefab'.");
            if (!string.IsNullOrEmpty(primitive) && !string.IsNullOrEmpty(prefab))
                throw new ToolException("BAD_VALUE", "Provide only one of 'primitive' or 'prefab', not both.");
            if (pattern != "grid" && pattern != "circle" && pattern != "line" && pattern != "stack")
                throw new ToolException("BAD_VALUE", $"Unknown pattern '{pattern}'. Use grid/circle/line/stack.");
        }

        private static PrimitiveType ResolvePrimitive(string primitive)
        {
            if (string.IsNullOrEmpty(primitive))
                return default;
            if (!Enum.TryParse(primitive, true, out PrimitiveType primType))
                throw new ToolException("BAD_VALUE",
                    $"Unknown primitive '{primitive}'. Use Cube/Sphere/Capsule/Cylinder/Plane/Quad.");
            return primType;
        }

        private static Object ResolvePrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
                return null;
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (prefabAsset == null)
                throw new ToolException("NOT_FOUND", $"No prefab GameObject at '{prefab}'.");
            return prefabAsset;
        }

        private static Vector3 PatternOffset(string pattern, int i, int count, float spacing, float radius)
        {
            switch (pattern)
            {
                case "line":
                    return new Vector3(i * spacing, 0f, 0f);
                case "stack":
                    return new Vector3(0f, i * spacing, 0f);
                case "circle":
                {
                    var ang = 2f * Mathf.PI * i / count;
                    return new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                }
                case "grid":
                default:
                {
                    var cols = Mathf.CeilToInt(Mathf.Sqrt(count));
                    var row = i / cols;
                    var col = i % cols;
                    return new Vector3(col * spacing, 0f, row * spacing);
                }
            }
        }

        private static Vector3? ReadScale(JObject @params, string key)
        {
            if (@params == null || !@params.TryGetValue(key, out var tok) || tok == null || tok.Type == JTokenType.Null)
                return null;
            if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
            {
                var s = tok.Value<float>();
                return new Vector3(s, s, s);
            }

            return SceneObjectUtil.ParseVector3(tok);
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("Primitive type: Cube/Sphere/Capsule/Cylinder/Plane/Quad. Mutually exclusive with prefab.")]
            public string Primitive { get; set; }

            [ToolParameter("Prefab asset path to instantiate. Mutually exclusive with primitive.")]
            public string Prefab { get; set; }

            [ToolParameter(
                "Base name. The container takes this name; children are suffixed _0.._n-1. Default = primitive/prefab name.")]
            public string Name { get; set; }

            [ToolParameter(
                "Hierarchy path of an existing container inside the subscene to parent under. Omit = create a new container GameObject named 'name'.")]
            public string Parent { get; set; }

            [ToolParameter("Pattern: grid | circle | line | stack.", Required = true)]
            public string Pattern { get; set; }

            [ToolParameter("How many objects to spawn.", Required = true)]
            public int Count { get; set; }

            [ToolParameter("Spacing between objects (grid/line/stack). Default 1.")]
            public float Spacing { get; set; }

            [ToolParameter("Circle radius (circle pattern). Default = spacing.")]
            public float Radius { get; set; }

            [ToolParameter("Base position [x,y,z] or {x,y,z} for the pattern. Default 0,0,0.")]
            public object Origin { get; set; }

            [ToolParameter("Per-object scale: a uniform number, or [x,y,z] / {x,y,z}.")]
            public object Scale { get; set; }
        }
    }
}