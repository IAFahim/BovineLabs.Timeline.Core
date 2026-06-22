using System;
using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_object_create",
        Group = "vex",
        Description = "Author GameObject(s) into a SubScene's editing scene: a primitive (Cube/Sphere/Capsule/Cylinder/Plane/Quad) or an instantiated prefab, parented + posed + with components added, then saved. Deterministic; emits an undo journal that round-trips through subscene_object_delete. Replaces hand-written exec for 'build cubes into the SubScene'.")]
    public static class SubsceneObjectCreateTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("Primitive type: Cube/Sphere/Capsule/Cylinder/Plane/Quad. Mutually exclusive with prefab.")]
            public string Primitive { get; set; }

            [ToolParameter("Prefab asset path to instantiate. Mutually exclusive with primitive.")]
            public string Prefab { get; set; }

            [ToolParameter("Name for the created object (default = primitive/prefab name). Suffixed _1.._n when count > 1.")]
            public string Name { get; set; }

            [ToolParameter("Hierarchy path of an existing object inside the subscene to parent under. Omit = scene root.")]
            public string Parent { get; set; }

            [ToolParameter("Local position [x,y,z] or {x,y,z}.")]
            public object Position { get; set; }

            [ToolParameter("Local euler angles [x,y,z] or {x,y,z}.")]
            public object Euler { get; set; }

            [ToolParameter("Local scale [x,y,z] or {x,y,z}.")]
            public object Scale { get; set; }

            [ToolParameter("Component type names (simple or full) to AddComponent on each created object.")]
            public string[] Components { get; set; }

            [ToolParameter("How many to create (default 1). Names are suffixed _1.._n when > 1.")]
            public int Count { get; set; }
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
                var componentsArr = p.OptArray("components");
                int count = p.OptInt("count", 1);

                if (string.IsNullOrEmpty(primitive) && string.IsNullOrEmpty(prefab))
                    throw new ToolException("MISSING_PREREQUISITE", "Provide either 'primitive' or 'prefab'.");
                if (!string.IsNullOrEmpty(primitive) && !string.IsNullOrEmpty(prefab))
                    throw new ToolException("BAD_VALUE", "Provide only one of 'primitive' or 'prefab', not both.");
                if (count < 1) count = 1;

                PrimitiveType primType = ResolvePrimitive(primitive);
                UnityEngine.Object prefabAsset = ResolvePrefab(prefab);

                // Resolve component types up-front so a bad name fails before any mutation.
                var compTypes = ResolveComponentTypes(componentsArr);

                string baseName = !string.IsNullOrEmpty(name) ? name
                    : !string.IsNullOrEmpty(primitive) ? primitive : Path.GetFileNameWithoutExtension(prefab);

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    Transform parentTransform = null;
                    bool parentExisted = string.IsNullOrEmpty(parentPath);
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        var parentGo = session.Find(parentPath);
                        if (parentGo == null)
                            return ToolEnvelope.Error("NOT_FOUND", $"No parent '{parentPath}' in {session.SubscenePath}.");
                        parentTransform = parentGo.transform;
                        parentExisted = true;
                    }

                    var createdGos = new List<GameObject>();
                    for (int i = 0; i < count; i++)
                    {
                        string goName = count > 1 ? $"{baseName}_{i + 1}" : baseName;

                        GameObject go;
                        if (prefabAsset != null)
                        {
                            go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                            if (go == null)
                                return ToolEnvelope.Error("BAD_VALUE", $"Could not instantiate prefab '{prefab}'.");
                        }
                        else
                            go = GameObject.CreatePrimitive(primType);
                        go.name = goName;

                        // Move into the subscene's editing scene first, then parent within it.
                        EditorSceneManager.MoveGameObjectToScene(go, session.Subscene);
                        if (parentTransform != null)
                            go.transform.SetParent(parentTransform, false);

                        // Ensure the name is unique among its siblings so the path-based verify below and
                        // the subscene_object_delete undo resolve THIS object, never a pre-existing
                        // same-named sibling (which would pass verify on the wrong instance and leak this one).
                        go.name = UniqueSiblingName(parentTransform, session.Subscene, goName, go);

                        SceneObjectUtil.ApplyTransform(@params, go.transform, "position", "euler", "scale");

                        foreach (var ct in compTypes)
                            go.AddComponent(ct);

                        createdGos.Add(go);
                    }

                    session.Save();

                    // Re-read state to verify (never assert success from the fact code ran).
                    var createdPaths = new List<string>();
                    var createdResult = new List<object>();
                    bool allExist = true, allInScene = true, allParented = true;
                    foreach (var go in createdGos)
                    {
                        string path = Hierarchy.PathOf(go);
                        createdPaths.Add(path);
                        createdResult.Add(new { path, name = go.name });

                        var found = session.Find(path);
                        if (found == null) { allExist = false; continue; }
                        if (found.scene != session.Subscene) allInScene = false;
                        if (parentTransform != null && found.transform.parent != parentTransform) allParented = false;
                    }

                    bool saved = !session.Subscene.isDirty;

                    var checks = new List<object>
                    {
                        new { name = "object-exists", pass = allExist, detail = $"{createdPaths.Count} created" },
                        new { name = "in-subscene", pass = allInScene, detail = session.SubscenePath },
                        new { name = "parented", pass = allParented, detail = parentTransform != null ? parentPath : "(root)" },
                        new { name = "saved", pass = saved, detail = saved ? "scene clean" : "scene still dirty" },
                    };
                    bool pass = allExist && allInScene && allParented && saved;

                    var undo = new object[]
                    {
                        new { tool = "subscene_object_delete", @params = new { subscene = session.SubscenePath, objects = createdPaths.ToArray() } },
                    };

                    string sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"Created {createdPaths.Count} object(s) in '{sceneName}'.",
                        result: new { created = createdResult },
                        pre: new { subscene = session.SubscenePath, parentExisted },
                        undo: undo,
                        verify: new { pass, checks });
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
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

        /// <summary>Resolve each requested component type up-front so a bad name fails before any mutation.</summary>
        private static List<Type> ResolveComponentTypes(JArray componentsArr)
        {
            var compTypes = new List<Type>();
            if (componentsArr == null)
                return compTypes;
            foreach (var tok in componentsArr)
            {
                var tn = tok?.ToString();
                if (string.IsNullOrEmpty(tn)) continue;
                compTypes.Add(SceneObjectUtil.ResolveComponentType(tn));
            }

            return compTypes;
        }

        // A name unique among the object's siblings (existing + already-created-this-batch), so the
        // hierarchy path identifies it uniquely. Excludes the object itself from the collision check.
        private static string UniqueSiblingName(Transform parent, Scene scene, string desired, GameObject self)
        {
            bool Collides(string n) => parent != null
                ? CollidesWithChild(parent, self, n)
                : CollidesWithRoot(scene, self, n);

            if (!Collides(desired))
                return desired;

            for (var i = 2; ; i++)
            {
                var candidate = $"{desired} ({i})";
                if (!Collides(candidate))
                    return candidate;
            }
        }

        private static bool CollidesWithChild(Transform parent, GameObject self, string n)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.gameObject != self && c.name == n)
                    return true;
            }

            return false;
        }

        private static bool CollidesWithRoot(Scene scene, GameObject self, string n)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root != self && root.name == n)
                    return true;
            return false;
        }
    }

    /// <summary>Shared helpers for the SubScene object/transform tools: vector parsing, component-type resolution.</summary>
    internal static class SceneObjectUtil
    {
        /// <summary>Resolve a Component subtype by simple or full name across loaded assemblies (or Type.GetType).</summary>
        public static Type ResolveComponentType(string name)
        {
            var t = TimelineReflect.ResolveType(name);
            if (!typeof(Component).IsAssignableFrom(t))
                throw new ToolException("BAD_VALUE", $"'{t.FullName}' is not a Component.");
            return t;
        }

        /// <summary>Parse a [x,y,z] array or {x,y,z} object token into a Vector3. Null/missing -> null.</summary>
        public static Vector3? ParseVector3(JToken tok)
        {
            if (tok == null || tok.Type == JTokenType.Null) return null;
            try
            {
                if (tok is JArray a)
                {
                    if (a.Count != 3) throw new ToolException("BAD_VALUE", $"Vector array must have 3 elements, got {a.Count}.");
                    return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
                }
                if (tok is JObject o)
                {
                    float x = o["x"]?.Value<float>() ?? 0f;
                    float y = o["y"]?.Value<float>() ?? 0f;
                    float z = o["z"]?.Value<float>() ?? 0f;
                    return new Vector3(x, y, z);
                }
            }
            catch (ToolException) { throw; }
            catch (Exception e) { throw new ToolException("BAD_VALUE", $"Could not parse vector: {e.Message}"); }
            throw new ToolException("BAD_VALUE", "Vector must be a [x,y,z] array or {x,y,z} object.");
        }

        /// <summary>Read a vector param by key from the raw params (array or object form). Null/missing -> null.</summary>
        public static Vector3? ReadVector3(JObject @params, string key)
        {
            if (@params == null) return null;
            return @params.TryGetValue(key, out var tok) ? ParseVector3(tok) : null;
        }

        /// <summary>Apply local position/euler/scale to a transform from the given param keys (only those provided).</summary>
        public static void ApplyTransform(JObject @params, Transform t, string posKey, string eulerKey, string scaleKey)
        {
            var pos = ReadVector3(@params, posKey);
            if (pos.HasValue) t.localPosition = pos.Value;
            var eul = ReadVector3(@params, eulerKey);
            if (eul.HasValue) t.localEulerAngles = eul.Value;
            var scl = ReadVector3(@params, scaleKey);
            if (scl.HasValue) t.localScale = scl.Value;
        }
    }
}
