// <copyright file="TargetFieldDrawer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core.Editor
{
    using System;
    using BovineLabs.Reaction.Authoring.Core;
    using BovineLabs.Reaction.Data.Core;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Draws every <see cref="Target" /> enum field with a small <b>ping</b> button that highlights the GameObject
    /// the chosen role resolves to — read straight from the <see cref="TargetsAuthoring" /> on the same object
    /// (Owner/Source fall back to the hierarchy root, mirroring the baker; Self is the object itself). So a designer
    /// picking "Owner" can immediately see *which* GameObject that is, instead of mentally tracing the wiring.
    /// </summary>
    [CustomPropertyDrawer(typeof(Target))]
    public sealed class TargetFieldDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 24f;

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var popupRect = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
            var pingRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            EditorGUI.PropertyField(popupRect, property, label);

            var go = ResolveGameObject(property, out var role);
            using (new EditorGUI.DisabledScope(go == null))
            {
                var tooltip = go != null
                    ? $"Ping '{go.name}' — the GameObject '{role}' resolves to."
                    : role == Target.None
                        ? "No role selected."
                        : $"'{role}' has no GameObject assigned on this object's TargetsAuthoring.";

                if (GUI.Button(pingRect, new GUIContent("◎", tooltip)) && go != null)
                {
                    EditorGUIUtility.PingObject(go);
                }
            }

            EditorGUI.EndProperty();
        }

        private static Target Current(SerializedProperty property)
        {
            var values = (Target[])Enum.GetValues(typeof(Target));
            var idx = property.enumValueIndex;
            return idx >= 0 && idx < values.Length ? values[idx] : Target.None;
        }

        // Map the selected role to its GameObject via the nearest TargetsAuthoring, matching the baker's fallbacks.
        private static GameObject ResolveGameObject(SerializedProperty property, out Target role)
        {
            role = Current(property);

            if (property.serializedObject.targetObject is not Component component || role == Target.None)
                return null;

            if (role == Target.Self)
                return component.gameObject;

            var targets = component.GetComponent<TargetsAuthoring>();
            if (targets == null)
            {
                targets = component.GetComponentInParent<TargetsAuthoring>(true);
            }

            var root = component.transform.root.gameObject;

            return role switch
            {
                // Owner/Source default to the hierarchy root when unset (see TargetsAuthoring.Baker.GetEntityOrDefaultRoot).
                Target.Owner => targets != null && targets.Owner != null ? targets.Owner : root,
                Target.Source => targets != null && targets.Source != null ? targets.Source : root,
                Target.Target => targets != null ? targets.Target : null,
                Target.Custom => targets != null ? targets.Custom : null,
                _ => null,
            };
        }
    }
}
