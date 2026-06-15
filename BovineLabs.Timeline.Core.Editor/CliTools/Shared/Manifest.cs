using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// Marks a class as a mechanic-manifest fragment, discovered by live reflection exactly like
    /// [UnityCliTool] (no registry). A fragment lives in the SAME package as the clip type it knows
    /// about (which already references Core.Editor), so the hand-declared "silent trap" requirements
    /// stay next to the code that owns them and Core.Editor never has to reference the track packages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class UnityCliManifestAttribute : Attribute { }

    /// <summary>The pipeline phase a requirement runs in. The engine sorts requirements by phase so
    /// project assets exist before the clip references them, the track exists before the clip is added,
    /// and exposed-references are wired only after the clip exists.</summary>
    internal enum ReqPhase
    {
        Assets = 10,     // schemas, object definitions — clip fields reference these
        SceneSetup = 20, // components on the source/payload, trigger-source config
        Track = 30,      // timeline asset + track
        Clip = 40,       // the core clip mutation (NOT idempotent)
        Bind = 50,       // director binding
        Wire = 60,       // exposed references (need the clip to exist)
    }

    /// <summary>
    /// One setup step the engine must satisfy. <see cref="Tool"/> is dispatched BY NAME through
    /// <c>ToolDiscovery.FindHandler</c> — so a requirement may resolve to an ensure_* tool in any
    /// package without Core.Editor referencing it. <see cref="Idempotent"/> ensures (default) are the
    /// only steps the doctor replays in dry_run; non-idempotent core mutations are skipped there.
    /// </summary>
    internal sealed class Requirement
    {
        public string Tool;
        public JObject Params;
        public string Label;
        public ReqPhase Phase;
        public bool Idempotent;

        public Requirement(string tool, JObject @params, string label, ReqPhase phase, bool idempotent = true)
        {
            Tool = tool;
            Params = @params ?? new JObject();
            Label = label;
            Phase = phase;
            Idempotent = idempotent;
        }
    }

    /// <summary>
    /// The resolved "few key things" a caller passes to <c>mechanic_author</c>, handed to both the
    /// reflectable deriver and the hand-declared manifest fragment so each can compute its requirement
    /// params. <see cref="Args"/> is the raw param object — extra mechanic-specific keys (source, prefab,
    /// objdef, stat, amount, exposed, …) are read from it via <see cref="P"/>.
    /// </summary>
    internal sealed class ManifestContext
    {
        public string Subscene;
        public string Director;
        public string Asset;
        public string TrackName;
        public Type TrackType;
        public Type ClipType;
        public string ClipName;
        public JObject Args;

        public Params P => new Params(Args);
    }

    /// <summary>A package-local fragment contributing the hand-declared (non-reflectable) requirements
    /// — the silent traps — for the clip types it recognises.</summary>
    internal interface IMechanicManifest
    {
        bool Handles(Type clipType);
        IEnumerable<Requirement> Requirements(ManifestContext ctx);
    }

    /// <summary>
    /// Discovers manifest fragments and derives the reflectable requirements. The union of
    /// <see cref="Reflectable"/> (generic, free) and the matching fragment's <see cref="IMechanicManifest.Requirements"/>
    /// (hand-declared) is the full requirement set the engine runs.
    /// </summary>
    internal static class ManifestDiscovery
    {
        /// <summary>First [UnityCliManifest] IMechanicManifest that handles <paramref name="clipType"/>, or null.</summary>
        public static IMechanicManifest ForClip(Type clipType)
        {
            if (clipType == null) return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

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

        /// <summary>
        /// Requirements derivable from the clip/track TYPES alone (no semantic knowledge):
        /// the track + asset (ensure_track), the director binding implied by the track's
        /// [TrackBindingType] (ensure_binding), the core clip mutation (clip_add), and an
        /// ensure_exposed_ref for every <c>ExposedReference&lt;T&gt;</c> clip field the caller targets.
        /// </summary>
        public static List<Requirement> Reflectable(ManifestContext ctx)
        {
            var p = ctx.P;
            var reqs = new List<Requirement>();

            // --- Track + asset ---------------------------------------------------------------------
            var trackParams = new JObject
            {
                ["asset"] = ctx.Asset,
                ["track_type"] = ctx.TrackType.FullName,
                ["track_name"] = ctx.TrackName,
            };
            var trackFields = p.OptObject("track_fields");
            if (trackFields != null) trackParams["track_fields"] = trackFields;
            reqs.Add(new Requirement("ensure_track", trackParams,
                $"track {ctx.TrackName} ({ctx.TrackType.Name}) in {ctx.Asset}", ReqPhase.Track));

            // --- Core clip mutation (not idempotent) ----------------------------------------------
            var clipParams = new JObject
            {
                ["asset"] = ctx.Asset,
                ["track"] = ctx.TrackName,
                ["clip_type"] = ctx.ClipType.FullName,
            };
            if (!string.IsNullOrEmpty(ctx.ClipName)) clipParams["display_name"] = ctx.ClipName;
            foreach (var key in new[] { "start", "duration", "blend_in", "blend_out" })
                if (p.Has(key)) clipParams[key] = ctx.Args[key];
            var clipFields = p.OptObject("clip_fields");
            if (clipFields != null) clipParams["fields"] = clipFields;
            reqs.Add(new Requirement("clip_add", clipParams,
                $"clip {ctx.ClipType.Name} on {ctx.TrackName}", ReqPhase.Clip, idempotent: false));

            // --- Director binding (from the track's [TrackBindingType]) -----------------------------
            string bindObject = p.OptString("bind_object") ?? p.OptString("source");
            if (!string.IsNullOrEmpty(bindObject))
            {
                string bindComponent = p.OptString("bind_component") ?? BindingComponentTypeName(ctx.TrackType);
                if (!string.IsNullOrEmpty(bindComponent))
                {
                    // The track binds to a component of this type — ensure it exists on the bind object
                    // BEFORE binding (director_bind refuses to bind a missing component).
                    reqs.Add(new Requirement("ensure_component", new JObject
                    {
                        ["subscene"] = ctx.Subscene,
                        ["object"] = bindObject,
                        ["component"] = bindComponent,
                    }, $"component {bindComponent} on {bindObject}", ReqPhase.SceneSetup));

                    var bindParams = new JObject
                    {
                        ["subscene"] = ctx.Subscene,
                        ["director"] = ctx.Director,
                        ["asset"] = ctx.Asset,
                        ["track"] = ctx.TrackName,
                        ["object"] = bindObject,
                        ["component"] = bindComponent,
                    };
                    reqs.Add(new Requirement("ensure_binding", bindParams,
                        $"bind {ctx.TrackName} -> {bindObject}.{bindComponent}", ReqPhase.Bind));
                }
            }

            // --- ExposedReference<T> clip fields the caller targets ---------------------------------
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
                        ["target"] = kv.Value,
                    };
                    reqs.Add(new Requirement("ensure_exposed_ref", wireParams,
                        $"wire {kv.Key} -> {kv.Value}", ReqPhase.Wire));
                }

            return reqs;
        }

        /// <summary>The component type a track binds to, from its <c>[TrackBindingType(typeof(X))]</c>
        /// attribute. Read reflectively over the attribute's type member so Core.Editor needn't pin a
        /// specific Timeline attribute API. Null when the track declares no binding type.</summary>
        public static string BindingComponentTypeName(Type trackType)
        {
            foreach (var attr in trackType.GetCustomAttributes(true))
            {
                var at = attr.GetType();
                if (at.Name != "TrackBindingTypeAttribute") continue;
                var member = (object)at.GetProperty("type") ?? at.GetField("type")
                             ?? (object)at.GetProperty("Type") ?? at.GetField("Type");
                Type bound = member switch
                {
                    PropertyInfo pi => pi.GetValue(attr) as Type,
                    FieldInfo fi => fi.GetValue(attr) as Type,
                    _ => null,
                };
                if (bound != null) return bound.Name;
            }
            return null;
        }

        private static bool IsExposedReferenceField(Type clipType, string field)
        {
            var fi = clipType.GetField(field, BindingFlags.Public | BindingFlags.Instance);
            return fi != null && fi.FieldType.IsGenericType
                && fi.FieldType.GetGenericTypeDefinition() == typeof(UnityEngine.ExposedReference<>);
        }
    }
}
