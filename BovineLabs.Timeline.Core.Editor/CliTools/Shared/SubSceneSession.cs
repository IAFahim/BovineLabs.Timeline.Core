using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// The safe SubScene bracket — the single biggest source of corruption and additive leaks in
    /// the exec workflow, solved once. Capture the parent scene, open the target SubScene
    /// additively (auto-detected from the active scene's SubScene component, or an explicit path),
    /// make it active, do work, then on Dispose restore: close the sub if WE opened it and leave
    /// the editor on the parent scene Single — never on an additive/subscene setup.
    ///
    /// SubScenes are detected by component TYPE NAME via SerializedObject, so there is no hard
    /// dependency on Unity.Scenes / Unity.Entities (same approach as scene_structure).
    /// </summary>
    internal sealed class SubSceneSession : IDisposable
    {
        public string SubscenePath { get; private set; }
        public string ParentPath { get; private set; }
        public Scene Subscene { get; private set; }

        /// <summary>Non-null when the session could not be opened (play mode, no subscene, etc.).</summary>
        public object Error { get; private set; }

        private bool openedByUs;
        private bool disposed;

        private SubSceneSession() { }

        public static SubSceneSession Open(string subscenePathOrNull)
        {
            var s = new SubSceneSession();
            try { s.OpenInternal(subscenePathOrNull); }
            catch (ToolException e) { s.Error = ToolEnvelope.FromException(e); }
            catch (Exception ex)
            {
                // OpenScene/SetActiveScene can throw non-ToolException (corrupt/locked scene asset).
                // If we already opened the subscene additively, close it so we never leak it.
                if (s.openedByUs && s.Subscene.IsValid() && s.Subscene.isLoaded)
                    EditorSceneManager.CloseScene(s.Subscene, true);
                if (!string.IsNullOrEmpty(s.ParentPath))
                {
                    var current = EditorSceneManager.GetActiveScene();
                    if (current.path != s.ParentPath)
                        EditorSceneManager.OpenScene(s.ParentPath, OpenSceneMode.Single);
                }
                s.openedByUs = false;
                s.Error = ToolEnvelope.Error("BAD_VALUE", ex.Message);
            }
            return s;
        }

        private void OpenInternal(string subscenePath)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Error = ToolEnvelope.Error("PLAY_MODE_BLOCKED",
                    "Editor is in play mode; scene/asset authoring is blocked. Stop play mode first.");
                return;
            }

            var active = EditorSceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                Error = ToolEnvelope.Error("MISSING_PREREQUISITE", "No active scene.");
                return;
            }
            ParentPath = active.path;

            if (string.IsNullOrEmpty(subscenePath))
                subscenePath = DetectSubscenePath(active);
            if (string.IsNullOrEmpty(subscenePath))
            {
                Error = ToolEnvelope.Error("MISSING_PREREQUISITE",
                    "No SubScene found in the active scene (and none specified).");
                return;
            }
            SubscenePath = subscenePath;

            var existing = EditorSceneManager.GetSceneByPath(subscenePath);
            if (existing.IsValid() && existing.isLoaded)
            {
                Subscene = existing;
                openedByUs = false;
            }
            else
            {
                Subscene = EditorSceneManager.OpenScene(subscenePath, OpenSceneMode.Additive);
                openedByUs = true;
            }

            if (!Subscene.IsValid())
            {
                Error = ToolEnvelope.Error("NOT_FOUND", $"Could not open subscene '{subscenePath}'.");
                return;
            }

            EditorSceneManager.SetActiveScene(Subscene);
        }

        /// <summary>Resolve a GameObject by hierarchy path within the subscene roots (finds inactive too).</summary>
        public GameObject Find(string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath) || !Subscene.IsValid()) return null;
            var parts = hierarchyPath.Split('/');
            foreach (var root in Subscene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                if (parts.Length == 1) return root;
                var rest = string.Join("/", parts, 1, parts.Length - 1);
                var child = root.transform.Find(rest); // Find walks inactive children
                if (child != null) return child.gameObject;
            }
            return null;
        }

        public void Save()
        {
            if (Subscene.IsValid())
                EditorSceneManager.SaveScene(Subscene);

            // A scene save does not re-convert the live world for ASSET edits (e.g. a clip's serialized fields):
            // the live-conversion change tracker only watches objects inside the open authoring scene, so an
            // AssetDatabase.SaveAssets() on a referenced .playable/clip never marks the baker's DependsOn dirty.
            // Force the open SubScene(s) to re-convert so the live ECS world reflects the edit. See RebakeUtil.
            RebakeUtil.ReimportOpenSubScenes();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (openedByUs)
            {
                if (Subscene.IsValid() && Subscene.isLoaded)
                    EditorSceneManager.CloseScene(Subscene, true);

                var current = EditorSceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(ParentPath) && current.path != ParentPath)
                    EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);
            }
            else
            {
                // We didn't open it — leave the user's scene setup intact, just restore the active scene.
                var parent = EditorSceneManager.GetSceneByPath(ParentPath);
                if (parent.IsValid() && parent.isLoaded)
                    EditorSceneManager.SetActiveScene(parent);
            }
        }

        /// <summary>Find the SceneAsset path referenced by the first SubScene component in the scene.</summary>
        public static string DetectSubscenePath(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = DetectInTransform(root.transform);
                if (!string.IsNullOrEmpty(found)) return found;
            }
            return null;
        }

        private static string DetectInTransform(Transform t)
        {
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c.GetType().Name == "SubScene")
                {
                    var path = ResolveSceneAssetPath(c);
                    if (!string.IsNullOrEmpty(path)) return path;
                }
            }
            for (int i = 0; i < t.childCount; i++)
            {
                var found = DetectInTransform(t.GetChild(i));
                if (!string.IsNullOrEmpty(found)) return found;
            }
            return null;
        }

        private static string ResolveSceneAssetPath(Component subSceneComponent)
        {
            var so = new SerializedObject(subSceneComponent);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference &&
                    it.objectReferenceValue != null &&
                    it.objectReferenceValue.GetType().Name == "SceneAsset")
                {
                    return AssetDatabase.GetAssetPath(it.objectReferenceValue);
                }
            }
            return null;
        }
    }
}
