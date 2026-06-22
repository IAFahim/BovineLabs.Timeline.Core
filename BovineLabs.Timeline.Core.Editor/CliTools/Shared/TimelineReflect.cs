using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class TimelineReflect
    {
        private const int MaxArrayDump = 128;
        private const int MaxDepth = 5;
        private static MethodInfo s_createClip;

        public static Type ResolveType(string name)
        {
            var full = new List<Type>();
            var simple = new List<Type>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] ts;
                try
                {
                    ts = a.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in ts)
                    if (t.FullName == name) full.Add(t);
                    else if (t.Name == name) simple.Add(t);
            }

            if (full.Count == 1) return full[0];
            if (full.Count > 1)
                throw new ToolException("AMBIGUOUS", $"Type '{name}' matches several full names.", Names(full));
            if (simple.Count == 1) return simple[0];
            if (simple.Count == 0)
                throw new ToolException("NOT_FOUND", $"Type '{name}' not found in any loaded assembly.");
            throw new ToolException("AMBIGUOUS",
                $"Type '{name}' is ambiguous across {simple.Count} assemblies — pass a fuller name.", Names(simple));
        }

        private static string[] Names(List<Type> ts)
        {
            var arr = new string[ts.Count];
            for (var i = 0; i < ts.Count; i++) arr[i] = ts[i].FullName;
            return arr;
        }

        public static TrackAsset CreateTrack(TimelineAsset timeline, Type trackType, TrackAsset parent, string name)
        {
            if (!typeof(TrackAsset).IsAssignableFrom(trackType))
                throw new ToolException("BAD_VALUE", $"'{trackType.Name}' is not a TrackAsset.");
            try
            {
                return timeline.CreateTrack(trackType, parent, name);
            }
            catch (Exception e)
            {
                throw new ToolException("BAD_VALUE", $"CreateTrack({trackType.Name}) failed: {e.Message}");
            }
        }

        public static TimelineClip CreateClip(TrackAsset track, Type clipType)
        {
            if (s_createClip == null)
            {
                foreach (var m in typeof(TrackAsset).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (m.Name == "CreateClip" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                    {
                        s_createClip = m;
                        break;
                    }

                if (s_createClip == null)
                    throw new ToolException("BAD_VALUE", "No public generic TrackAsset.CreateClip<T>() found.");
            }

            try
            {
                return (TimelineClip)s_createClip.MakeGenericMethod(clipType).Invoke(track, null);
            }
            catch (TargetInvocationException tie)
            {
                throw new ToolException("BAD_VALUE",
                    $"CreateClip<{clipType.Name}> failed: {tie.InnerException?.Message ?? tie.Message}");
            }
        }

        public static TrackAsset FindTrack(TimelineAsset timeline, string sel)
        {
            if (timeline == null || string.IsNullOrEmpty(sel)) return null;

            var tracks = timeline.GetOutputTracks();

            foreach (var t in tracks)
                if (t.name == sel)
                    return t;

            return int.TryParse(sel, out var n) ? AtIndex(tracks, n) : null;
        }

        private static TrackAsset AtIndex(IEnumerable<TrackAsset> tracks, int n)
        {
            if (n < 0) return null;
            var idx = 0;
            foreach (var t in tracks)
            {
                if (idx == n) return t;
                idx++;
            }

            return null;
        }

        public static TimelineClip FindClip(TrackAsset track, string sel)
        {
            if (track == null || string.IsNullOrEmpty(sel)) return null;

            var clips = track.GetClips();

            foreach (var c in clips)
                if (c.displayName == sel)
                    return c;

            return int.TryParse(sel, out var n) ? AtIndex(clips, n) : null;
        }

        private static TimelineClip AtIndex(IEnumerable<TimelineClip> clips, int n)
        {
            if (n < 0) return null;
            var idx = 0;
            foreach (var c in clips)
            {
                if (idx == n) return c;
                idx++;
            }

            return null;
        }

        public static void SetSerializedField(Object owner, string field, JToken value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new ToolException("NOT_FOUND", $"Field '{field}' not found on {owner.GetType().Name}.");

            try
            {
                ApplyValue(prop, field, owner, value);
            }
            catch (ToolException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ToolException("BAD_VALUE", $"Field '{field}': could not apply value '{value}': {e.Message}");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyValue(SerializedProperty prop, string field, Object owner, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.Value<int>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.Value<bool>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.Value<float>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.Value<string>();
                    break;
                case SerializedPropertyType.Enum:

                    if (value.Type == JTokenType.String)
                    {
                        var et = EnumFieldType(owner, field);
                        if (et == null)
                            throw new ToolException("BAD_VALUE",
                                $"Field '{field}': cannot resolve enum type for name '{value}'.");
                        prop.intValue = Convert.ToInt32(Enum.Parse(et, value.Value<string>(), true));
                    }
                    else
                    {
                        prop.intValue = value.Value<int>();
                    }

                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = ResolveAssetRef(value, field);
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = Vec2(value, field);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = Vec3(value, field);
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = Vec4(value, field);
                    break;
                default:
                    throw new ToolException("BAD_VALUE",
                        $"Field '{field}' has unsupported serialized type {prop.propertyType}.");
            }
        }

        private static Type EnumFieldType(Object owner, string field)
        {
            var t = owner.GetType();
            FieldInfo fi = null;
            foreach (var part in field.Split('.'))
            {
                fi = t.GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi == null) return null;
                t = fi.FieldType;
            }

            return fi != null && fi.FieldType.IsEnum ? fi.FieldType : null;
        }

        private static Object ResolveAssetRef(JToken value, string field)
        {
            if (value == null || value.Type == JTokenType.Null) return null;

            string path = null;
            if (value.Type == JTokenType.String) path = value.Value<string>();
            else if (value is JObject o && o["guid"] != null)
                path = AssetDatabase.GUIDToAssetPath(o["guid"].Value<string>());

            if (string.IsNullOrEmpty(path))
                throw new ToolException("BAD_VALUE",
                    $"Field '{field}': an object reference must be an asset path string or {{\"guid\":\"...\"}}.");

            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null)
                throw new ToolException("SCENE_REF_REFUSED",
                    $"Field '{field}': '{path}' is not a loadable asset. A scene object cannot be stored in an asset field " +
                    "(it serializes as {fileID:0}) — use exposed_ref_wire or an EntityLinks track for scene references.");
            return obj;
        }

        private static Vector2 Vec2(JToken v, string field)
        {
            if (v is JArray a) return new Vector2(F(a, 0), F(a, 1));
            if (v is JObject o) return new Vector2(o.Value<float>("x"), o.Value<float>("y"));
            throw new ToolException("BAD_VALUE", $"Field '{field}': expected [x,y] or {{x,y}}.");
        }

        private static Vector3 Vec3(JToken v, string field)
        {
            if (v is JArray a) return new Vector3(F(a, 0), F(a, 1), F(a, 2));
            if (v is JObject o) return new Vector3(o.Value<float>("x"), o.Value<float>("y"), o.Value<float>("z"));
            throw new ToolException("BAD_VALUE", $"Field '{field}': expected [x,y,z] or {{x,y,z}}.");
        }

        private static Vector4 Vec4(JToken v, string field)
        {
            if (v is JArray a) return new Vector4(F(a, 0), F(a, 1), F(a, 2), F(a, 3));
            if (v is JObject o)
                return new Vector4(o.Value<float>("x"), o.Value<float>("y"), o.Value<float>("z"), o.Value<float>("w"));
            throw new ToolException("BAD_VALUE", $"Field '{field}': expected [x,y,z,w] or {{x,y,z,w}}.");
        }

        private static float F(JArray a, int i)
        {
            return i < a.Count ? a[i].Value<float>() : 0f;
        }

        public static JObject ReadSerializedFields(Object owner)
        {
            var result = new JObject();
            var so = new SerializedObject(owner);
            var sp = so.GetIterator();
            var ok = sp.NextVisible(true);
            while (ok)
            {
                if (sp.name != "m_Script")
                    result[sp.name] = Render(sp, 0);
                ok = sp.NextVisible(false);
            }

            return result;
        }

        private static JToken Render(SerializedProperty sp, int depth)
        {
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer: return sp.intValue;
                case SerializedPropertyType.Boolean: return sp.boolValue;
                case SerializedPropertyType.Float: return sp.floatValue;
                case SerializedPropertyType.String: return sp.stringValue;
                case SerializedPropertyType.Enum: return sp.intValue;
                case SerializedPropertyType.Vector2: return new JArray(sp.vector2Value.x, sp.vector2Value.y);
                case SerializedPropertyType.Vector3:
                    return new JArray(sp.vector3Value.x, sp.vector3Value.y, sp.vector3Value.z);
                case SerializedPropertyType.Vector4:
                    return new JArray(sp.vector4Value.x, sp.vector4Value.y, sp.vector4Value.z, sp.vector4Value.w);
                case SerializedPropertyType.Quaternion:
                    return new JArray(sp.quaternionValue.x, sp.quaternionValue.y, sp.quaternionValue.z,
                        sp.quaternionValue.w);
                case SerializedPropertyType.Color:
                    return new JArray(sp.colorValue.r, sp.colorValue.g, sp.colorValue.b, sp.colorValue.a);
                case SerializedPropertyType.ObjectReference: return RenderObjectRef(sp.objectReferenceValue);
                default:
                    if (sp.isArray) return RenderArray(sp, depth);
                    if (sp.hasVisibleChildren && depth < MaxDepth) return RenderChildren(sp, depth + 1);
                    return sp.propertyType.ToString();
            }
        }

        private static JToken RenderObjectRef(Object o)
        {
            if (o == null) return JValue.CreateNull();
            var assetPath = AssetDatabase.GetAssetPath(o);
            var isAsset = !string.IsNullOrEmpty(assetPath);
            return new JObject
            {
                ["name"] = o.name,
                ["assetPath"] = isAsset ? assetPath : JValue.CreateNull(),
                ["guid"] = isAsset ? AssetDatabase.AssetPathToGUID(assetPath) : JValue.CreateNull()
            };
        }

        private static JToken RenderArray(SerializedProperty sp, int depth)
        {
            var arr = new JArray();
            var n = Math.Min(sp.arraySize, MaxArrayDump);
            for (var i = 0; i < n; i++)
                arr.Add(Render(sp.GetArrayElementAtIndex(i), depth + 1));
            if (sp.arraySize > MaxArrayDump)
                arr.Add($"...(+{sp.arraySize - MaxArrayDump} more)");
            return arr;
        }

        private static JToken RenderChildren(SerializedProperty sp, int depth)
        {
            var obj = new JObject();
            var end = sp.GetEndProperty();
            var it = sp.Copy();
            var enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                obj[it.name] = Render(it, depth);
            }

            return obj;
        }
    }

    internal static class Hierarchy
    {
        public static string PathOf(GameObject go)
        {
            if (go == null) return null;
            var t = go.transform;
            var sb = new StringBuilder(t.name);
            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }

            return sb.ToString();
        }
    }
}