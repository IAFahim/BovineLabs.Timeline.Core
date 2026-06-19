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
    /// Draws every <see cref="Target" /> enum field with a ◎ button that opens (Alt+P style) the GameObject the
    /// chosen role resolves to — read from the <see cref="TargetsAuthoring" /> on the same object, or, on a Timeline
    /// clip, from the object the track is bound to. Owner/Source fall back to the hierarchy root (mirroring the
    /// baker); Self is the object itself. So a designer picking "Owner" sees *which* GameObject that is.
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
            var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            EditorGUI.PropertyField(popupRect, property, label);

            var go = ResolveGameObject(property, out var role);
            var disabledTip = role == Target.None ? "No role selected." : $"'{role}' has no resolvable GameObject here.";
            EditorInspect.OpenButton(buttonRect, go, disabledTip);

            EditorGUI.EndProperty();
        }

        private static Target Current(SerializedProperty property)
        {
            var values = (Target[])Enum.GetValues(typeof(Target));
            var idx = property.enumValueIndex;
            return idx >= 0 && idx < values.Length ? values[idx] : Target.None;
        }

        // Map the selected role to its GameObject via the nearest TargetsAuthoring, matching the baker's fallbacks.
        // On a component: the sibling/parent TargetsAuthoring. On a Timeline clip: the object the track is bound to.
        private static GameObject ResolveGameObject(SerializedProperty property, out Target role)
        {
            role = Current(property);
            if (role == Target.None)
                return null;

            var component = property.serializedObject.targetObject as Component;
            if (component == null)
            {
                TimelineBinding.TryGetBoundComponent(property, out component);
            }

            if (component == null)
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
