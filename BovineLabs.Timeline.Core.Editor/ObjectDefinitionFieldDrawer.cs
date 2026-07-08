using System.IO;
using BovineLabs.Nerve.Authoring.ObjectManagement;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor
{
    [CustomPropertyDrawer(typeof(ObjectDefinition), true)]
    public sealed class ObjectDefinitionFieldDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 52f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var row1 = new Rect(position.x, position.y, position.width, line);
            var row2 = new Rect(position.x, position.y + line + spacing, position.width, line);

            EditorGUI.BeginChangeCheck();
            var newDef = (ObjectDefinition)EditorGUI.ObjectField(row1, label, property.objectReferenceValue,
                typeof(ObjectDefinition), false);
            if (EditorGUI.EndChangeCheck()) property.objectReferenceValue = newDef;

            var def = property.objectReferenceValue as ObjectDefinition;
            var currentPrefab = def != null ? def.Prefab : null;

            var prefabRect = new Rect(row2.x, row2.y, row2.width - ButtonWidth - 2f, line);
            var openRect = new Rect(row2.xMax - ButtonWidth, row2.y, ButtonWidth, line);

            var prefabLabel = new GUIContent(
                "Prefab",
                "The prefab this ObjectDefinition spawns. Drop a prefab here to auto-assign its ObjectDefinition (creates one if needed).");

            EditorGUI.BeginChangeCheck();
            var dropped =
                (GameObject)EditorGUI.ObjectField(prefabRect, prefabLabel, currentPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && dropped != null && dropped != currentPrefab)
                AssignFromPrefab(property, dropped);

            using (new EditorGUI.DisabledScope(currentPrefab == null))
            {
                if (GUI.Button(openRect,
                        new GUIContent("Open", "Open this prefab in Prefab Mode (keeps your current selection).")) &&
                    currentPrefab != null) AssetDatabase.OpenAsset(currentPrefab);
            }

            EditorGUI.EndProperty();
        }

        private static void AssignFromPrefab(SerializedProperty property, GameObject prefab)
        {
            var auth = prefab.GetComponent<ObjectDefinitionAuthoring>();
            if (auth != null && auth.Definition != null)
            {
                property.objectReferenceValue = auth.Definition;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Create ObjectDefinition",
                    $"'{prefab.name}' has no ObjectDefinition.\n\nCreate one (asset + prefab back-link) and assign it to this field?",
                    "Create",
                    "Cancel"))
                return;

            var created = CreateDefinitionForPrefab(prefab);
            if (created != null)
            {
                property.objectReferenceValue = created;
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        private static ObjectDefinition CreateDefinitionForPrefab(GameObject prefab)
        {
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError(
                    $"[ObjectDefinition] '{prefab.name}' is not a prefab asset; cannot create an ObjectDefinition.");
                return null;
            }

            var dir = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            var defPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{prefab.name}.asset");

            var def = ScriptableObject.CreateInstance<ObjectDefinition>();
            def.name = prefab.name;

            var so = new SerializedObject(def);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("id").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(def, defPath);

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var auth = contents.GetComponent<ObjectDefinitionAuthoring>();
                if (auth == null) auth = contents.AddComponent<ObjectDefinitionAuthoring>();

                auth.Definition = def;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return def;
        }
    }
}