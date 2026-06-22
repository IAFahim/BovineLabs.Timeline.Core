using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class UnityCliManifestAttribute : Attribute
    {
    }

    internal enum ReqPhase
    {
        Assets = 10,
        SceneSetup = 20,
        Track = 30,
        Clip = 40,
        Bind = 50,
        Wire = 60
    }

    internal sealed class Requirement
    {
        public bool Idempotent;
        public string Label;
        public JObject Params;
        public ReqPhase Phase;
        public string Tool;

        public Requirement(string tool, JObject @params, string label, ReqPhase phase, bool idempotent = true)
        {
            Tool = tool;
            Params = @params ?? new JObject();
            Label = label;
            Phase = phase;
            Idempotent = idempotent;
        }
    }

    internal sealed class ManifestContext
    {
        public JObject Args;
        public string Asset;
        public string ClipName;
        public Type ClipType;
        public string Director;
        public string Subscene;
        public string TrackName;
        public Type TrackType;

        public Params P => new(Args);
    }

    internal interface IMechanicManifest
    {
        bool Handles(Type clipType);
        IEnumerable<Requirement> Requirements(ManifestContext ctx);
    }

    internal static class ManifestDiscovery
    {
        public static IMechanicManifest ForClip(Type clipType)
        {
            if (clipType == null) return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (!type.IsClass || type.IsAbstract) continue;
                    if (type.GetCustomAttribute<UnityCliManifestAttribute>() == null) continue;
                    if (!typeof(IMechanicManifest).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    var fragment = (IMechanicManifest)Activator.CreateInstance(type);
                    if (fragment.Handles(clipType)) return fragment;
                }
            }

            return null;
        }

        public static List<Requirement> Reflectable(ManifestContext ctx)
        {
            var p = ctx.P;
            var reqs = new List<Requirement>();

            var trackParams = new JObject
            {
                ["asset"] = ctx.Asset,
                ["track_type"] = ctx.TrackType.FullName,
                ["track_name"] = ctx.TrackName
            };
            var trackFields = p.OptObject("track_fields");
            if (trackFields != null) trackParams["track_fields"] = trackFields;
            reqs.Add(new Requirement("ensure_track", trackParams,
                $"track {ctx.TrackName} ({ctx.TrackType.Name}) in {ctx.Asset}", ReqPhase.Track));

            var clipParams = new JObject
            {
                ["asset"] = ctx.Asset,
                ["track"] = ctx.TrackName,
                ["clip_type"] = ctx.ClipType.FullName
            };
            if (!string.IsNullOrEmpty(ctx.ClipName)) clipParams["display_name"] = ctx.ClipName;
            foreach (var key in new[] { "start", "duration", "blend_in", "blend_out" })
                if (p.Has(key))
                    clipParams[key] = ctx.Args[key];
            var clipFields = p.OptObject("clip_fields");
            if (clipFields != null) clipParams["fields"] = clipFields;
            reqs.Add(new Requirement("clip_add", clipParams,
                $"clip {ctx.ClipType.Name} on {ctx.TrackName}", ReqPhase.Clip, false));

            var bindObject = p.OptString("bind_object") ?? p.OptString("source");
            if (!string.IsNullOrEmpty(bindObject))
            {
                var bindComponent = p.OptString("bind_component") ?? BindingComponentTypeName(ctx.TrackType);
                if (!string.IsNullOrEmpty(bindComponent))
                {
                    reqs.Add(new Requirement("ensure_component", new JObject
                        {
                            ["subscene"] = ctx.Subscene,
                            ["object"] = bindObject,
                            ["component"] = bindComponent
                        }, $"component {bindComponent} on {bindObject}", ReqPhase.SceneSetup));

                    var bindParams = new JObject
                    {
                        ["subscene"] = ctx.Subscene,
                        ["director"] = ctx.Director,
                        ["asset"] = ctx.Asset,
                        ["track"] = ctx.TrackName,
                        ["object"] = bindObject,
                        ["component"] = bindComponent
                    };
                    reqs.Add(new Requirement("ensure_binding", bindParams,
                        $"bind {ctx.TrackName} -> {bindObject}.{bindComponent}", ReqPhase.Bind));
                }
            }

            var exposed = p.OptObject("exposed");
            if (exposed != null)
                foreach (var kv in exposed)
                {
                    if (!IsExposedReferenceField(ctx.ClipType, kv.Key)) continue;
                    var wireParams = new JObject
                    {
                        ["subscene"] = ctx.Subscene,
                        ["director"] = ctx.Director,
                        ["asset"] = ctx.Asset,
                        ["track"] = ctx.TrackName,
                        ["clip"] = string.IsNullOrEmpty(ctx.ClipName) ? ctx.ClipType.Name : ctx.ClipName,
                        ["field"] = kv.Key,
                        ["target"] = kv.Value
                    };
                    reqs.Add(new Requirement("ensure_exposed_ref", wireParams,
                        $"wire {kv.Key} -> {kv.Value}", ReqPhase.Wire));
                }

            return reqs;
        }

        public static string BindingComponentTypeName(Type trackType)
        {
            foreach (var attr in trackType.GetCustomAttributes(true))
            {
                var at = attr.GetType();
                if (at.Name != "TrackBindingTypeAttribute") continue;
                var member = (object)at.GetProperty("type") ?? at.GetField("type")
                    ?? (object)at.GetProperty("Type") ?? at.GetField("Type");
                var bound = member switch
                {
                    PropertyInfo pi => pi.GetValue(attr) as Type,
                    FieldInfo fi => fi.GetValue(attr) as Type,
                    _ => null
                };
                if (bound != null) return bound.Name;
            }

            return null;
        }

        private static bool IsExposedReferenceField(Type clipType, string field)
        {
            var fi = clipType.GetField(field, BindingFlags.Public | BindingFlags.Instance);
            return fi != null && fi.FieldType.IsGenericType
                              && fi.FieldType.GetGenericTypeDefinition() == typeof(ExposedReference<>);
        }
    }
}