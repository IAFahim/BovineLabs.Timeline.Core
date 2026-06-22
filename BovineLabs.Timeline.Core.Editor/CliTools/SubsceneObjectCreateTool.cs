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
using Object = UnityEngine.Object;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_object_create",
        Group = "vex",
        Description =
            "Author GameObject(s) into a SubScene's editing scene: a primitive (Cube/Sphere/Capsule/Cylinder/Plane/Quad) or an instantiated prefab, parented + posed + with components added, then saved. Deterministic; emits an undo journal that round-trips through subscene_object_delete. Replaces hand-written exec for 'build cubes into the SubScene'.")]
    public static class SubsceneObjectCreateTool
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
                var componentsArr = p.OptArray("components");
                var count = p.OptInt("count", 1);

                if (string.IsNullOrEmpty(primitive) && string.IsNullOrEmpty(prefab))
                    throw new ToolException("MISSING_PREREQUISITE", "Provide either 'primitive' or 'prefab'.");
                if (!string.IsNullOrEmpty(primitive) && !string.IsNullOrEmpty(prefab))
                    throw new ToolException("BAD_VALUE", "Provide only one of 'primitive' or 'prefab', not both.");
                if (count < 1) count = 1;

                var primType = ResolvePrimitive(primitive);
                var prefabAsset = ResolvePrefab(prefab);

                var compTypes = ResolveComponentTypes(componentsArr);

                var baseName = !string.IsNullOrEmpty(name) ? name
                    : !string.IsNullOrEmpty(primitive) ? primitive : Path.GetFileNameWithoutExtension(prefab);

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    Transform parentTransform = null;
                    var parentExisted = string.IsNullOrEmpty(parentPath);
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        var parentGo = session.Find(parentPath);
                        if (parentGo == null)
                            return ToolEnvelope.Error("NOT_FOUND",
                                $"No parent '{parentPath}' in {session.SubscenePath}.");
                        parentTransform = parentGo.transform;
                        parentExisted = true;
                    }

                    var createdGos = new List<GameObject>();
                    for (var i = 0; i < count; i++)
                    {
                        var goName = count > 1 ? $"{baseName}_{i + 1}" : baseName;

                        GameObject go;
                        if (prefabAsset != null)
                        {
                            go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                            if (go == null)
                                return ToolEnvelope.Error("BAD_VALUE", $"Could not instantiate prefab '{prefab}'.");
                        }
                        else
                        {
                            go = GameObject.CreatePrimitive(primType);
                        }

                        go.name = goName;

                        EditorSceneManager.MoveGameObjectToScene(go, session.Subscene);
                        if (parentTransform != null)
                            go.transform.SetParent(parentTransform, false);

                        go.name = UniqueSiblingName(parentTransform, session.Subscene, goName, go);

                        SceneObjectUtil.ApplyTransform(@params, go.transform, "position", "euler", "scale");

                        foreach (var ct in compTypes)
                            go.AddComponent(ct);

                        createdGos.Add(go);
                    }

                    session.Save();

                    var createdPaths = new List<string>();
                    var createdResult = new List<object>();
                    bool allExist = true, allInScene = true, allParented = true;
                    foreach (var go in createdGos)
                    {
                        var path = Hierarchy.PathOf(go);
                        createdPaths.Add(path);
                        createdResult.Add(new { path, go.name });

                        var found = session.Find(path);
                        if (found == null)
                        {
                            allExist = false;
                            continue;
                        }

                        if (found.scene != session.Subscene) allInScene = false;
                        if (parentTransform != null && found.transform.parent != parentTransform) allParented = false;
                    }

                    var saved = !session.Subscene.isDirty;

                    var checks = new List<object>
                    {
                        new { name = "object-exists", pass = allExist, detail = $"{createdPaths.Count} created" },
                        new { name = "in-subscene", pass = allInScene, detail = session.SubscenePath },
                        new
                        {
                            name = "parented", pass = allParented,
                            detail = parentTransform != null ? parentPath : "(root)"
                        },
                        new { name = "saved", pass = saved, detail = saved ? "scene clean" : "scene still dirty" }
                    };
                    var pass = allExist && allInScene && allParented && saved;

                    var undo = new object[]
                    {
                        new
                        {
                            tool = "subscene_object_delete",
                            @params = new { subscene = session.SubscenePath, objects = createdPaths.ToArray() }
                        }
                    };

                    var sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"Created {createdPaths.Count} object(s) in '{sceneName}'.",
                        new { created = createdResult },
                        new { subscene = session.SubscenePath, parentExisted },
                        undo,
                        new { pass, checks });
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
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

        private static string UniqueSiblingName(Transform parent, Scene scene, string desired, GameObject self)
        {
            bool Collides(string n)
            {
                return parent != null
                    ? CollidesWithChild(parent, self, n)
                    : CollidesWithRoot(scene, self, n);
            }

            if (!Collides(desired))
                return desired;

            for (var i = 2;; i++)
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

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }

            [ToolParameter("Primitive type: Cube/Sphere/Capsule/Cylinder/Plane/Quad. Mutually exclusive with prefab.")]
            public string Primitive { get; set; }

            [ToolParameter("Prefab asset path to instantiate. Mutually exclusive with primitive.")]
            public string Prefab { get; set; }

            [ToolParameter(
                "Name for the created object (default = primitive/prefab name). Suffixed _1.._n when count > 1.")]
            public string Name { get; set; }

            [ToolParameter(
                "Hierarchy path of an existing object inside the subscene to parent under. Omit = scene root.")]
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
    }

    internal static class SceneObjectUtil
    {
        public static Type ResolveComponentType(string name)
        {
            var t = TimelineReflect.ResolveType(name);
            if (!typeof(Component).IsAssignableFrom(t))
                throw new ToolException("BAD_VALUE", $"'{t.FullName}' is not a Component.");
            return t;
        }

        public static Vector3? ParseVector3(JToken tok)
        {
            if (tok == null || tok.Type == JTokenType.Null) return null;
            try
            {
                if (tok is JArray a)
                {
                    if (a.Count != 3)
                        throw new ToolException("BAD_VALUE", $"Vector array must have 3 elements, got {a.Count}.");
                    return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
                }

                if (tok is JObject o)
                {
                    var x = o["x"]?.Value<float>() ?? 0f;
                    var y = o["y"]?.Value<float>() ?? 0f;
                    var z = o["z"]?.Value<float>() ?? 0f;
                    return new Vector3(x, y, z);
                }
            }
            catch (ToolException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ToolException("BAD_VALUE", $"Could not parse vector: {e.Message}");
            }

            throw new ToolException("BAD_VALUE", "Vector must be a [x,y,z] array or {x,y,z} object.");
        }

        public static Vector3? ReadVector3(JObject @params, string key)
        {
            if (@params == null) return null;
            return @params.TryGetValue(key, out var tok) ? ParseVector3(tok) : null;
        }

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