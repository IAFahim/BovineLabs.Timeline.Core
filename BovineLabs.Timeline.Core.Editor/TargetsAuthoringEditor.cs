using BovineLabs.Reaction.Authoring.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.Core.Editor
{
    [CustomEditor(typeof(TargetsAuthoring))]
    public sealed class TargetsAuthoringEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            AddRoleWithHint(root, "Owner");
            AddRoleWithHint(root, "Source");
            root.Add(new PropertyField(serializedObject.FindProperty("Target")));
            root.Add(new PropertyField(serializedObject.FindProperty("Custom")));
            root.Add(new PropertyField(serializedObject.FindProperty("Initialize")));

            return root;
        }

        private void AddRoleWithHint(VisualElement parent, string fieldName)
        {
            var prop = serializedObject.FindProperty(fieldName);
            var authoring = (TargetsAuthoring)target;

            var field = new PropertyField(prop);
            parent.Add(field);

            GameObject resolvedRoot = null;
            var hint = new Button(() => EditorInspect.Open(resolvedRoot));
            hint.style.unityFontStyleAndWeight = FontStyle.Italic;
            hint.style.unityTextAlign = TextAnchor.MiddleRight;
            hint.style.fontSize = 10;
            hint.style.opacity = 0.7f;
            hint.style.height = 15;
            hint.style.marginTop = -2;
            hint.style.marginBottom = 2;
            hint.style.paddingTop = 0;
            hint.style.paddingBottom = 0;
            parent.Add(hint);

            void Refresh()
            {
                if (prop.objectReferenceValue == null && authoring != null)
                {
                    resolvedRoot = authoring.transform.root.gameObject;
                    hint.text = $"◎ bakes to “{resolvedRoot.name}” (auto · root)";
                    hint.tooltip =
                        $"Empty → the baker assigns the hierarchy root “{resolvedRoot.name}”. Click to open it.";
                    hint.style.display = DisplayStyle.Flex;
                }
                else
                {
                    resolvedRoot = null;
                    hint.style.display = DisplayStyle.None;
                }
            }

            Refresh();
            field.TrackPropertyValue(prop, _ => Refresh());
        }
    }
}