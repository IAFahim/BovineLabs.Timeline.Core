// <copyright file="TargetsAuthoringEditor.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core.Editor
{
    using BovineLabs.Reaction.Authoring.Core;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Inspector for <see cref="TargetsAuthoring" /> that reveals the baker's hidden fallback: an empty
    /// <c>Owner</c>/<c>Source</c> bakes to the hierarchy root (<see cref="TargetsAuthoring" />'s Baker
    /// <c>GetEntityOrDefaultRoot</c>), not to nothing. Empty fields get a slight italic hint of what they
    /// actually resolve to, so "None" stops lying. <c>Target</c>/<c>Custom</c> have no fallback, so they're
    /// left untouched. UI Toolkit (not IMGUI) so the package's own <c>[PrefabElement]</c> drawer on
    /// <c>Initialize</c> still renders.
    /// </summary>
    [CustomEditor(typeof(TargetsAuthoring))]
    public sealed class TargetsAuthoringEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            this.AddRoleWithHint(root, "Owner");
            this.AddRoleWithHint(root, "Source");
            root.Add(new PropertyField(this.serializedObject.FindProperty("Target")));
            root.Add(new PropertyField(this.serializedObject.FindProperty("Custom")));
            root.Add(new PropertyField(this.serializedObject.FindProperty("Initialize")));

            return root;
        }

        // Draw the role's object field, plus a dim italic hint line beneath it (right-aligned, so it never
        // overlaps the field's own "None (Game Object)" text) showing the root it bakes to while empty.
        private void AddRoleWithHint(VisualElement parent, string fieldName)
        {
            var prop = this.serializedObject.FindProperty(fieldName);
            var authoring = (TargetsAuthoring)this.target;

            var field = new PropertyField(prop);
            parent.Add(field);

            // The auto-baked root as a ◎ button: click to open that GameObject's Properties window (Alt+P).
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
                    hint.tooltip = $"Empty → the baker assigns the hierarchy root “{resolvedRoot.name}”. Click to open it.";
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
