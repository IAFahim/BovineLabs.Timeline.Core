using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor
{
    /// <summary>
    /// Decorates the built-in PlayableDirector inspector (keeps Unity's binding UI) with two designer panels:
    /// which Timeline packages this director's timeline pulls tracks/clips from (with versions), and the full
    /// ScriptableObject sub-asset inventory (every track/clip, pingable).
    /// </summary>
    [CustomEditor(typeof(PlayableDirector))]
    public sealed class DirectorInventoryEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor defaultEditor;
        private bool showPackages = true;
        private bool showAssets;

        // Cached SerializedObjects for the expanded sub-assets' link fields.
        private readonly Dictionary<UnityEngine.Object, SerializedObject> serialized = new();
        private readonly HashSet<UnityEngine.Object> expanded = new();

        private void OnEnable()
        {
            // Wrap Unity's internal DirectorEditor so we keep the stock inspector (bindings, playback, etc.).
            var directorEditorType = Type.GetType("UnityEditor.DirectorEditor, UnityEditor.CoreModule");
            if (directorEditorType != null)
            {
                defaultEditor = CreateEditor(targets, directorEditorType);
            }
        }

        private void OnDisable()
        {
            if (defaultEditor != null)
            {
                DestroyImmediate(defaultEditor);
            }

            serialized.Clear();
        }

        public override void OnInspectorGUI()
        {
            if (defaultEditor != null)
            {
                defaultEditor.OnInspectorGUI();
            }
            else
            {
                base.OnInspectorGUI();
            }

            var timeline = (target as PlayableDirector)?.playableAsset as TimelineAsset;
            if (timeline == null)
            {
                return;
            }

            EditorGUILayout.Space();

            var packages = CollectPackages(timeline);
            showPackages = EditorGUILayout.Foldout(showPackages, $"Timeline packages ({packages.Count})", true);
            if (showPackages)
            {
                DrawPackages(packages);
            }

            var subAssets = CollectSubAssets(timeline);
            showAssets = EditorGUILayout.Foldout(showAssets, $"ScriptableObjects in timeline ({subAssets.Count})", true);
            if (showAssets)
            {
                DrawAssets(subAssets);
            }
        }

        private static List<PackageUse> CollectPackages(TimelineAsset timeline)
        {
            var byKey = new Dictionary<string, PackageUse>();

            void Tally(Type type)
            {
                var asm = type.Assembly;
                var pi = UnityEditor.PackageManager.PackageInfo.FindForAssembly(asm);
                var key = pi != null ? pi.name : asm.GetName().Name;
                if (!byKey.TryGetValue(key, out var use))
                {
                    use = new PackageUse
                    {
                        Display = pi != null ? pi.displayName : asm.GetName().Name,
                        Version = pi != null ? pi.version : "project",
                    };
                }

                use.Count++;
                byKey[key] = use;
            }

            foreach (var track in timeline.GetOutputTracks())
            {
                Tally(track.GetType());
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset != null)
                    {
                        Tally(clip.asset.GetType());
                    }
                }
            }

            return byKey.Values.OrderByDescending(u => u.Count).ThenBy(u => u.Display).ToList();
        }

        private static void DrawPackages(List<PackageUse> packages)
        {
            if (packages.Count == 0)
            {
                EditorGUILayout.LabelField("  (empty timeline — no tracks)");
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var use in packages)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(use.Display, GUILayout.MinWidth(140));
                        EditorGUILayout.LabelField(use.Version, EditorStyles.miniLabel, GUILayout.Width(80));
                        EditorGUILayout.LabelField($"×{use.Count}", EditorStyles.miniLabel, GUILayout.Width(40));
                    }
                }
            }
        }

        private static List<UnityEngine.Object> CollectSubAssets(TimelineAsset timeline)
        {
            var path = AssetDatabase.GetAssetPath(timeline);
            if (string.IsNullOrEmpty(path))
            {
                return new List<UnityEngine.Object>();
            }

            // Every track / clip / marker in a timeline is a ScriptableObject sub-asset of the .playable file.
            // Skip the TimelineAsset root itself (that's the thing we're already looking at).
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .Where(o => o != null && o is not TimelineAsset)
                .OrderBy(o => o.GetType().Name)
                .ThenBy(o => o.name)
                .ToList();
        }

        private void DrawAssets(List<UnityEngine.Object> assets)
        {
            if (assets.Count == 0)
            {
                EditorGUILayout.LabelField("  (timeline is not a saved asset, or has no tracks)");
                return;
            }

            foreach (var o in assets)
            {
                // Native component-style header with a foldout; expand = the object's full, editable inspector inline.
                var open = EditorGUILayout.InspectorTitlebar(expanded.Contains(o), o, true);
                if (open)
                {
                    expanded.Add(o);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawLinks(o);
                    }
                }
                else
                {
                    expanded.Remove(o);
                }
            }
        }

        // Just the ScriptableObject-reference fields ("placement links") of the sub-asset — editable, so a designer
        // can swap a schema/link/action fast without the whole clip inspector. Uses the custom drawers.
        private void DrawLinks(UnityEngine.Object o)
        {
            var so = GetSerialized(o);
            so.Update();

            var any = false;
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script")
                {
                    continue;
                }

                // Only asset links (ScriptableObjects). Skip scene-object references and unset non-SO fields.
                if (it.objectReferenceValue is not ScriptableObject)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(it, true);
                any = true;
            }

            if (!any)
            {
                EditorGUILayout.LabelField("  (no scriptable-object links)");
            }

            so.ApplyModifiedProperties();
        }

        private SerializedObject GetSerialized(UnityEngine.Object o)
        {
            if (!serialized.TryGetValue(o, out var so) || so == null || so.targetObject == null)
            {
                so = new SerializedObject(o);
                serialized[o] = so;
            }

            return so;
        }

        private struct PackageUse
        {
            public string Display;
            public string Version;
            public int Count;
        }
    }
}
