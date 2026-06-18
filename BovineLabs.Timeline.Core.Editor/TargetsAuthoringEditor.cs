// <copyright file="TargetsAuthoringEditor.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core.Editor
{
    using BovineLabs.Reaction.Authoring.Core;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Inspector for <see cref="TargetsAuthoring" /> that reveals the baker's hidden fallback: an empty
    /// <c>Owner</c>/<c>Source</c> bakes to the hierarchy root (<see cref="TargetsAuthoring" />'s Baker
    /// <c>GetEntityOrDefaultRoot</c>), not to nothing. Empty fields show a slight inline hint of what they
    /// actually resolve to, so "None" stops lying. <c>Target</c>/<c>Custom</c> have no fallback, so they're
    /// left untouched.
    /// </summary>
    [CustomEditor(typeof(TargetsAuthoring))]
    public sealed class TargetsAuthoringEditor : UnityEditor.Editor
    {
        // Width reserved on the right for the object-picker dot, so the hint never sits over it.
        private const float PickerDotWidth = 18f;

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            var root = ((TargetsAuthoring)this.target).transform.root.gameObject;

            DrawWithRootHint("Owner", this.serializedObject.FindProperty("Owner"), root);
            DrawWithRootHint("Source", this.serializedObject.FindProperty("Source"), root);
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("Target"));
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("Custom"));
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("Initialize"), true);

            this.serializedObject.ApplyModifiedProperties();
        }

        // Draw the field normally, then — if empty — overlay a dim right-aligned hint of the baked root.
        // The hint is a non-interactive label beside the left-aligned "None (Game Object)" text, so the
        // underlying ObjectField (and its picker dot) keep working. Never writes to the property.
        private static void DrawWithRootHint(string label, SerializedProperty prop, GameObject root)
        {
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.PropertyField(rect, prop, new GUIContent(label));

            if (prop.objectReferenceValue != null || root == null)
            {
                return;
            }

            var valueX = rect.x + EditorGUIUtility.labelWidth + 2f;
            var hintRect = new Rect(valueX, rect.y, rect.xMax - valueX - PickerDotWidth, rect.height);

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Italic,
            };

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(
                hintRect,
                new GUIContent($"→ {root.name} (auto · root)", $"Empty → bakes to the hierarchy root '{root.name}'."),
                style);
            GUI.color = prev;
        }
    }
}
