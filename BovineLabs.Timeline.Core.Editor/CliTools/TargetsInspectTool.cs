using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "targets_inspect",
        Group = "vex",
        Description = "Per TargetsAuthoring holder in the SubScene: path + the Owner/Source/Target/Custom address-book slots and the Initialize.Target re-route flag, so a designer can see who an effect lands on (read reflection-free via SerializedObject; no Reaction compile dependency).")]
    public static class TargetsInspectTool
    {
        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }
        }

        // Mirror of BovineLabs.Reaction.Data.Core.Target (byte) — resolved by value so we need no compile dep.
        private static string TargetName(long v) => v switch
        {
            0 => "None",
            1 => "Target",
            2 => "Owner",
            3 => "Source",
            4 => "Self",
            6 => "Custom",
            _ => "Unknown",
        };

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                string subscene = p.OptString("subscene");

                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;

                    var holders = new List<object>();

                    // TargetsAuthoring lives in an unreferenced assembly; resolve every Component by type name.
                    var allComponents = Object.FindObjectsByType<Component>(FindObjectsInactive.Include);
                    foreach (var component in allComponents)
                    {
                        if (component == null) continue;
                        if (component.GetType().Name != "TargetsAuthoring") continue;
                        if (component.gameObject.scene != session.Subscene) continue;

                        holders.Add(ReadHolder(component));
                    }

                    string sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"{holders.Count} TargetsAuthoring holder(s) in '{sceneName}'.",
                        result: new { subscene = session.SubscenePath, holders });
                }
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        private static object ReadHolder(Component component)
        {
            var so = new SerializedObject(component);

            // Object-ref slots (GameObject Owner/Source/Target/Custom).
            object owner = ReadObjectRef(so.FindProperty("Owner"));
            object source = ReadObjectRef(so.FindProperty("Source"));
            object target = ReadObjectRef(so.FindProperty("Target"));
            object custom = ReadObjectRef(so.FindProperty("Custom"));

            // Initialize.Target enum (re-route on instantiation). Stored as a byte enum.
            object initializeTarget = ReadEnum(so.FindProperty("Initialize.Target"));

            // Robust fallback: surface any other visible serialized fields we didn't name explicitly.
            var extra = new Dictionary<string, object>();
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                string path = it.propertyPath;
                if (path == "m_Script") continue;
                if (path == "Owner" || path == "Source" || path == "Target" || path == "Custom") continue;
                if (path == "Initialize" || path == "Initialize.Target") continue;
                if (it.propertyType == SerializedPropertyType.Generic) continue;
                extra[path] = ScalarOf(it);
            }

            return new
            {
                path = Hierarchy.PathOf(component.gameObject),
                owner,
                source,
                target,
                custom,
                initializeTarget,
                extra = extra.Count > 0 ? extra : null,
            };
        }

        private static object ReadObjectRef(SerializedProperty prop)
        {
            if (prop == null) return new { unresolved = "field missing" };
            var obj = prop.objectReferenceValue;
            if (obj == null) return null; // empty slot → defaults to prefab root at bake (see unity-targets)
            string path = obj is GameObject go ? Hierarchy.PathOf(go)
                : obj is Component c ? Hierarchy.PathOf(c.gameObject)
                : null;
            return new { name = obj.name, path };
        }

        private static object ReadEnum(SerializedProperty prop)
        {
            if (prop == null) return new { unresolved = "Initialize.Target missing" };
            int v = prop.intValue; // byte enum stored as int by SerializedProperty
            return new { value = v, name = TargetName(v) };
        }

        private static object ScalarOf(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum: return new { value = prop.intValue, name = prop.enumValueIndex >= 0 && prop.enumNames != null && prop.enumValueIndex < prop.enumNames.Length ? prop.enumNames[prop.enumValueIndex] : null };
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue == null ? null : prop.objectReferenceValue.name;
                default: return prop.propertyType.ToString();
            }
        }
    }
}
