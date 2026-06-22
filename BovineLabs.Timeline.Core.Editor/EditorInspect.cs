using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.Core.Editor
{
    using Object = Object;

    public static class EditorInspect
    {
        private static MethodInfo openMethod;
        private static bool resolved;

        public static void Open(Object obj)
        {
            if (obj == null) return;

            var method = GetOpenMethod();
            if (method != null)
                try
                {
                    var args = method.GetParameters().Length == 1 ? new object[] { obj } : new object[] { obj, true };
                    method.Invoke(null, args);
                    return;
                }
                catch
                {
                }

            try
            {
                EditorGUIUtility.PingObject(obj);
            }
            catch
            {
            }
        }

        public static void OpenButton(Rect rect, Object target, string disabledTooltip)
        {
            using (new EditorGUI.DisabledScope(target == null))
            {
                var tooltip = target != null
                    ? $"Open '{target.name}' in a Properties window (Alt+P)."
                    : disabledTooltip;
                if (GUI.Button(rect, new GUIContent("◎", tooltip)) && target != null) Open(target);
            }
        }

        public static Button CreateButton(System.Func<Object> resolve, string text, string tooltip = null)
        {
            var button = new Button(() => Open(resolve())) { text = text };
            if (tooltip != null) button.tooltip = tooltip;

            return button;
        }

        private static MethodInfo GetOpenMethod()
        {
            if (resolved) return openMethod;

            resolved = true;

            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
            if (type != null)
                foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != "OpenPropertyEditor") continue;

                    var ps = m.GetParameters();
                    var matches = ps.Length >= 1 && typeof(Object).IsAssignableFrom(ps[0].ParameterType) &&
                                  (ps.Length == 1 || (ps.Length == 2 && ps[1].ParameterType == typeof(bool)));
                    if (!matches) continue;

                    openMethod = m;
                    if (ps.Length == 1) break;
                }

            return openMethod;
        }
    }
}