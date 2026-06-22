using System;
using System.Collections.Generic;
using System.Reflection;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "ensure_component",
        Group = "vex",
        Description =
            "Idempotent: ensure a SubScene object carries a component (added if missing) with given serialized fields. already-ok when present and fields already match; otherwise adds the component and/or sets the differing fields, recording an undo that removes-if-added or restores prior field values. dry_run reports without mutating. The shared 'fix if X missing' block.")]
    public static class EnsureComponentTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var objPath = p.RequireString("object");
                var compName = p.RequireString("component");
                var fields = p.OptObject("fields");
                var dryRun = p.OptBool("dry_run", false);

                var compType = SceneObjectUtil.ResolveComponentType(compName);
                var target = new { @object = objPath, component = compType.Name };

                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;

                    var go = session.Find(objPath);
                    if (go == null)
                        return ToolEnvelope.Error("NOT_FOUND", $"No object '{objPath}' in {session.SubscenePath}.");

                    var comp = go.GetComponent(compType);
                    var present = comp != null;

                    var differing = new List<string>();
                    if (present && fields != null)
                        foreach (var kv in fields)
                            if (!FieldMatches(comp, kv.Key, kv.Value))
                                differing.Add(kv.Key);

                    if (present && differing.Count == 0)
                        return EnsureResult.Satisfied(
                            $"'{objPath}' already has {compType.Name}{(fields != null ? " with matching fields" : "")}.",
                            target, new { present = true });

                    if (dryRun)
                    {
                        var what = !present ? "add component" : $"set {differing.Count} field(s)";
                        return EnsureResult.WouldFixResult($"Would {what} on '{objPath}'.", target,
                            new { present, differingFields = differing });
                    }

                    object[] undo;
                    if (!present)
                    {
                        comp = go.AddComponent(compType);
                        try
                        {
                            if (fields != null)
                                foreach (var kv in fields)
                                    TimelineReflect.SetSerializedField(comp, kv.Key, kv.Value);
                        }
                        catch
                        {
                            Object.DestroyImmediate(comp);
                            throw;
                        }

                        undo = new object[]
                        {
                            new
                            {
                                tool = "subscene_component_remove",
                                @params = new
                                    { subscene = session.SubscenePath, @object = objPath, component = compType.Name }
                            }
                        };
                    }
                    else
                    {
                        var all = TimelineReflect.ReadSerializedFields(comp);
                        var prior = new JObject();
                        foreach (var f in differing)
                        {
                            prior[f] = all.SelectToken(f) ?? all[f];
                            TimelineReflect.SetSerializedField(comp, f, fields[f]);
                        }

                        undo = new object[]
                        {
                            new
                            {
                                tool = "ensure_component",
                                @params = new
                                {
                                    subscene = session.SubscenePath, @object = objPath, component = compType.Name,
                                    fields = prior
                                }
                            }
                        };
                    }

                    EditorUtility.SetDirty(comp);
                    session.Save();

                    var msg = !present
                        ? $"Added {compType.Name} to '{objPath}'."
                        : $"Set {differing.Count} field(s) on {compType.Name} of '{objPath}'.";
                    return EnsureResult.Applied(msg, target,
                        new { present, differingFields = differing },
                        new { present = true },
                        undo);
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        private static bool FieldMatches(Component comp, string field, JToken wanted)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new ToolException("NOT_FOUND", $"Field '{field}' not found on {comp.GetType().Name}.");

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue == wanted.Value<int>();
                case SerializedPropertyType.Boolean: return prop.boolValue == wanted.Value<bool>();
                case SerializedPropertyType.Float: return Mathf.Approximately(prop.floatValue, wanted.Value<float>());
                case SerializedPropertyType.String: return prop.stringValue == wanted.Value<string>();
                case SerializedPropertyType.Enum:
                    if (wanted.Type == JTokenType.String)
                    {
                        var enumType = ResolveEnumType(comp.GetType(), field);
                        if (enumType != null)
                            try
                            {
                                return prop.intValue ==
                                       Convert.ToInt32(Enum.Parse(enumType, wanted.Value<string>(), true));
                            }
                            catch (Exception)
                            {
                                return false;
                            }

                        var names = prop.enumNames;
                        var wantedName = wanted.Value<string>();
                        for (var i = 0; i < names.Length; i++)
                            if (string.Equals(names[i], wantedName, StringComparison.OrdinalIgnoreCase))
                                return prop.enumValueIndex == i;
                        return false;
                    }

                    return prop.intValue == wanted.Value<int>();
                case SerializedPropertyType.ObjectReference:
                {
                    var path = wanted.Type == JTokenType.String
                        ? wanted.Value<string>()
                        : wanted is JObject o && o["guid"] != null
                            ? AssetDatabase.GUIDToAssetPath(o["guid"].Value<string>())
                            : wanted is JObject ro && ro["assetPath"] != null
                                ? ro["assetPath"].Value<string>()
                                : null;
                    var cur = prop.objectReferenceValue;
                    if (string.IsNullOrEmpty(path)) return cur == null;
                    return cur != null && AssetDatabase.GetAssetPath(cur) == path;
                }
                default:
                    return false;
            }
        }

        private static Type ResolveEnumType(Type root, string dottedPath)
        {
            var t = root;
            FieldInfo fi = null;
            foreach (var part in dottedPath.Split('.'))
            {
                fi = t.GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi == null) return null;
                t = fi.FieldType;
            }

            return fi != null && fi.FieldType.IsEnum ? fi.FieldType : null;
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("SubScene object hierarchy path.", Required = true)]
            public string Object { get; set; }

            [ToolParameter("Component type name, simple or full.", Required = true)]
            public string Component { get; set; }

            [ToolParameter(
                "JSON object of fieldName -> value to ensure (dotted paths allowed, e.g. Initialize.Target).")]
            public object Fields { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }
    }
}