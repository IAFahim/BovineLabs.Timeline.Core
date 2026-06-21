using System;
using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_object_spawn_pattern",
        Group = "vex",
        Description = "Spawn N primitives/prefabs into the open SubScene in a grid/circle/line/stack pattern, parented under one container; self-verifies and returns an undo journal.")]
    public static class SubsceneObjectSpawnPatternTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("Primitive type: Cube/Sphere/Capsule/Cylinder/Plane/Quad. Mutually exclusive with prefab.")]
            public string Primitive { get; set; }

            [ToolParameter("Prefab asset path to instantiate. Mutually exclusive with primitive.")]
            public string Prefab { get; set; }

            [ToolParameter("Base name. The container takes this name; children are suffixed _0.._n-1. Default = primitive/prefab name.")]
            public string Name { get; set; }

            [ToolParameter("Hierarchy path of an existing container inside the subscene to parent under. Omit = create a new container GameObject named 'name'.")]
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

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string subscene = p.OptString("subscene");
                string primitive = p.OptString("primitive");
                string prefab = p.OptString("prefab");
                string name = p.OptString("name");
                string parentPath = p.OptString("parent");
                string pattern = p.RequireString("pattern").ToLowerInvariant();
                int count = p.OptInt("count", 0);
                float spacing = p.OptFloat("spacing", 1f);
                float radius = p.OptFloat("radius", spacing);

                ValidateSpawnArgs(count, primitive, prefab, pattern);
                PrimitiveType primType = ResolvePrimitive(primitive);
                UnityEngine.Object prefabAsset = ResolvePrefab(prefab);

                Vector3 origin = SceneObjectUtil.ReadVector3(@params, "origin") ?? Vector3.zero;
                Vector3? scale = ReadScale(@params, "scale");

                string baseName = !string.IsNullOrEmpty(name) ? name
                    : !string.IsNullOrEmpty(primitive) ? primitive : Path.GetFileNameWithoutExtension(prefab);

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    // Resolve / create the single container all spawned objects parent under.
                    Transform container;
                    bool containerExisted;
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        var existing = session.Find(parentPath);
                        if (existing == null)
                            return ToolEnvelope.Error("NOT_FOUND", $"No parent '{parentPath}' in {session.SubscenePath}.");
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
                    for (int i = 0; i < count; i++)
                    {
                        GameObject go;
                        if (prefabAsset != null)
                            go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                        else
                            go = GameObject.CreatePrimitive(primType);
                        go.name = $"{baseName}_{i}";

                        EditorSceneManager.MoveGameObjectToScene(go, session.Subscene);
                        go.transform.SetParent(container, false);

                        // Pattern offset relative to the container. When we created the container we
                        // already placed it at origin, so children sit at the local offset. When an
                        // existing container is reused, fold origin into the local offset.
                        Vector3 offset = PatternOffset(pattern, i, count, spacing, radius);
                        go.transform.localPosition = containerExisted ? origin + offset : offset;

                        if (scale.HasValue)
                            go.transform.localScale = scale.Value;

                        createdGos.Add(go);
                    }

                    session.Save();

                    // Re-read state after save to verify (never assert success from the fact code ran).
                    string containerPath = Hierarchy.PathOf(container.gameObject);
                    var createdPaths = new List<string>();
                    var createdResult = new List<object>();
                    bool allExist = true, allInScene = true;
                    foreach (var go in createdGos)
                    {
                        string path = Hierarchy.PathOf(go);
                        createdPaths.Add(path);
                        createdResult.Add(new { path });

                        var found = session.Find(path);
                        if (found == null) { allExist = false; continue; }
                        if (found.scene != session.Subscene) allInScene = false;
                    }

                    bool countMatches = createdPaths.Count == count;
                    bool saved = !session.Subscene.isDirty;

                    var checks = new List<object>
                    {
                        new { name = "count-matches", pass = countMatches, detail = $"created {createdPaths.Count} of {count}" },
                        new { name = "object-exists", pass = allExist, detail = $"{createdPaths.Count} re-resolved" },
                        new { name = "all-in-subscene", pass = allInScene, detail = session.SubscenePath },
                        new { name = "saved", pass = saved, detail = saved ? "scene clean" : "scene still dirty" },
                    };
                    bool pass = countMatches && allExist && allInScene && saved;

                    // Undo: delete the container removes all children in one step. If we reused an
                    // existing container we must NOT delete it — list the child paths instead.
                    string[] undoTargets = containerExisted ? createdPaths.ToArray() : new[] { containerPath };
                    var undo = new object[]
                    {
                        new { tool = "subscene_object_delete", @params = new { subscene = session.SubscenePath, objects = undoTargets } },
                    };

                    string sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"Spawned {createdPaths.Count} '{pattern}' object(s) under '{containerPath}' in '{sceneName}'.",
                        result: new { container = new { path = containerPath, name = container.name }, created = createdResult, count = createdPaths.Count },
                        pre: new { subscene = session.SubscenePath, containerExisted },
                        undo: undo,
                        verify: new { pass, checks });
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        /// <summary>Reject bad counts, the primitive/prefab xor, and unknown patterns before any mutation.</summary>
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

        /// <summary>Parse the primitive enum (default when no primitive requested).</summary>
        private static PrimitiveType ResolvePrimitive(string primitive)
        {
            if (string.IsNullOrEmpty(primitive))
                return default;
            if (!Enum.TryParse(primitive, true, out PrimitiveType primType))
                throw new ToolException("BAD_VALUE", $"Unknown primitive '{primitive}'. Use Cube/Sphere/Capsule/Cylinder/Plane/Quad.");
            return primType;
        }

        /// <summary>Load the prefab asset (null when no prefab requested).</summary>
        private static UnityEngine.Object ResolvePrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
                return null;
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (prefabAsset == null)
                throw new ToolException("NOT_FOUND", $"No prefab GameObject at '{prefab}'.");
            return prefabAsset;
        }

        /// <summary>Local-space offset for the i-th object in the chosen pattern, centered on the container origin.</summary>
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
                    float ang = (2f * Mathf.PI * i) / count;
                    return new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                }
                case "grid":
                default:
                {
                    int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
                    int row = i / cols;
                    int col = i % cols;
                    return new Vector3(col * spacing, 0f, row * spacing);
                }
            }
        }

        /// <summary>Parse a scale param: a single number (uniform) or a [x,y,z]/{x,y,z} vector. Null/missing -> null.</summary>
        private static Vector3? ReadScale(JObject @params, string key)
        {
            if (@params == null || !@params.TryGetValue(key, out var tok) || tok == null || tok.Type == JTokenType.Null)
                return null;
            if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
            {
                float s = tok.Value<float>();
                return new Vector3(s, s, s);
            }
            return SceneObjectUtil.ParseVector3(tok);
        }
    }
}
