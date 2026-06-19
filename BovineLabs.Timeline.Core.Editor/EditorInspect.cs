// <copyright file="EditorInspect.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core.Editor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Shared "locate" affordance for the designer-clarity drawers/inspectors. A ◎ button that opens the
    /// resolved object's <b>floating Properties window</b> — Unity's <c>Alt+P</c> (<c>UnityEditor.PropertyEditor</c>,
    /// reached by reflection like the rest of Core's editor tooling). It does NOT frame the Hierarchy, so it works
    /// for SubScene / Timeline-preview objects that <see cref="EditorGUIUtility.PingObject" /> crashes on; ping is
    /// only a last-resort fallback.
    /// </summary>
    public static class EditorInspect
    {
        private static MethodInfo openMethod;
        private static bool resolved;

        /// <summary> Open the object's floating Properties window (Alt+P). Falls back to a guarded ping. </summary>
        public static void Open(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            var method = GetOpenMethod();
            if (method != null)
            {
                try
                {
                    var args = method.GetParameters().Length == 1 ? new object[] { obj } : new object[] { obj, true };
                    method.Invoke(null, args);
                    return;
                }
                catch
                {
                    // Internal API drifted — fall through to ping.
                }
            }

            // Synchronous throw from PingObject is catchable; never touch Selection (it defers a Hierarchy frame
            // that throws asynchronously and escapes try/catch).
            try
            {
                EditorGUIUtility.PingObject(obj);
            }
            catch
            {
            }
        }

        /// <summary> IMGUI ◎ button (disabled when target is null). </summary>
        public static void OpenButton(Rect rect, Object target, string disabledTooltip)
        {
            using (new EditorGUI.DisabledScope(target == null))
            {
                var tooltip = target != null ? $"Open '{target.name}' in a Properties window (Alt+P)." : disabledTooltip;
                if (GUI.Button(rect, new GUIContent("◎", tooltip)) && target != null)
                {
                    Open(target);
                }
            }
        }

        /// <summary> UI Toolkit ◎ button; <paramref name="resolve" /> is evaluated at click time. </summary>
        public static Button CreateButton(Func<Object> resolve, string text, string tooltip = null)
        {
            var button = new Button(() => Open(resolve())) { text = text };
            if (tooltip != null)
            {
                button.tooltip = tooltip;
            }

            return button;
        }

        private static MethodInfo GetOpenMethod()
        {
            if (resolved)
            {
                return openMethod;
            }

            resolved = true;

            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
            if (type != null)
            {
                foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != "OpenPropertyEditor")
                    {
                        continue;
                    }

                    var ps = m.GetParameters();
                    var matches = ps.Length >= 1 && typeof(Object).IsAssignableFrom(ps[0].ParameterType) &&
                                  (ps.Length == 1 || (ps.Length == 2 && ps[1].ParameterType == typeof(bool)));
                    if (!matches)
                    {
                        continue;
                    }

                    openMethod = m;
                    if (ps.Length == 1)
                    {
                        break; // prefer the simplest overload
                    }
                }
            }

            return openMethod;
        }
    }
}
